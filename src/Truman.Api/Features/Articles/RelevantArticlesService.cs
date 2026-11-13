using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

public class RelevantArticlesService : IRelevantArticlesService
{
    private readonly TrumanDbContext _dbContext;
    private readonly ILogger<RelevantArticlesService> _logger;
    
    // Weighting constants for relevance calculation
    private const double ValueAlignmentWeight = 0.4;
    private const double TagAlignmentWeight = 0.4;
    private const double SentimentDeltaWeight = 0.2;
    private const double ExponentialDecayBase = 1.5;
    private const int ValueMatchThreshold = 7;

    public RelevantArticlesService(TrumanDbContext dbContext, ILogger<RelevantArticlesService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RelevantArticlesResponse> GetRelevantArticlesAsync(RelevantArticlesRequest request, UserProfile userProfile)
    {
        // Parse the selected values from JSON string
        var selectedValues = ParseSelectedValues(userProfile.SelectedValues);
        
        _logger.LogInformation("Getting relevant articles for user {UserEmail} with {ValueCount} values and minimum sentiment {MinSentiment}", 
            userProfile.Email, selectedValues.Count, userProfile.Mood);

        // Get today's date for filtering (using UTC to avoid timezone issues with PostgreSQL)
        var earliest = DateTime.UtcNow.Date.AddDays(-1);

        // Get banned tags for filtering
        var bannedTags = userProfile.TagPreferences?
            .Where(tp => tp.Weight == 0)
            .Select(tp => tp.Tag)
            .ToList() ?? [];

        // Get all articles that meet the minimum sentiment threshold and are from today
        // Exclude articles that contain any banned tags
        var articles = await _dbContext.Articles
            .Include(a => a.ArticlePresenters)
                .ThenInclude(ap => ap.Presenter)
            .Include(a => a.RssItem)
                .ThenInclude(r => r.Feed)
            .Where(a => a.Sentiment >= userProfile.Mood)
            .Where(a => a.CreatedAt >= earliest)
            .ToListAsync();

        // Filter out articles with banned tags (case-insensitive comparison)
        if (bannedTags.Count > 0)
        {
            articles = articles.Where(a => 
                !a.Tags.Any(articleTag => 
                    bannedTags.Any(bannedTag => 
                        string.Equals(articleTag, bannedTag, StringComparison.OrdinalIgnoreCase)
                    )
                )
            ).ToList();
        }

        _logger.LogInformation("Found {ArticleCount} articles from yesterday and today with minimum sentiment {MinSentiment}", 
            articles.Count, userProfile.Mood);

        var savedIds = await SavedArticleIdsAsync(userProfile, articles.Select(a => a.Id));

        var relevantArticles = Score(articles, request, userProfile, savedIds)
            .OrderByDescending(a => a.RelevanceScore)
            .ToList();

        return new RelevantArticlesResponse
        {
            Articles = relevantArticles
        };
    }

    public async Task<RelevantArticlesResponse> GetSavedArticlesAsync(RelevantArticlesRequest request, UserProfile userProfile)
    {
        var saved = await _dbContext.SavedStories
            .Where(s => s.UserProfileId == userProfile.Id)
            .Include(s => s.Article.ArticlePresenters)
                .ThenInclude(ap => ap.Presenter)
            .Include(s => s.Article.RssItem)
                .ThenInclude(r => r.Feed)
            .OrderByDescending(s => s.SavedAt)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
        var articles = saved.Select(s => s.Article).ToList();

        return new RelevantArticlesResponse
        {
            Articles = Score(articles, request, userProfile, articles.Select(a => a.Id).ToHashSet()).ToList()
        };
    }

    private async Task<HashSet<int>> SavedArticleIdsAsync(UserProfile userProfile, IEnumerable<int> articleIds)
    {
        var ids = articleIds.ToList();
        var saved = await _dbContext.SavedStories
            .Where(s => s.UserProfileId == userProfile.Id && ids.Contains(s.ArticleId))
            .Select(s => s.ArticleId)
            .ToListAsync();
        return saved.ToHashSet();
    }

    private IEnumerable<RelevantArticle> Score(IEnumerable<Article> articles, RelevantArticlesRequest request, UserProfile userProfile, HashSet<int> savedIds)
    {
        var selectedValues = ParseSelectedValues(userProfile.SelectedValues);
        var userValueWeights = CalculateUserValueWeights(selectedValues);
        var userTagWeights = CalculateUserTagWeights(userProfile);

        return articles.Select(article =>
        {
            var parts = CalculateRelevanceParts(article, userValueWeights, userTagWeights, userProfile.Mood);
            return new RelevantArticle
            {
                Id = article.Id,
                Link = article.Link,
                Title = GetPresenterTitle(article.ArticlePresenters, request.Presenter),
                Tldr = GetPresenterTldr(article.ArticlePresenters, request.Presenter),
                Content = GetPresenterContent(article.ArticlePresenters, request.Presenter),
                Sentiment = article.Sentiment,
                Tags = article.Tags,
                RelevanceScore = parts.Values + parts.Tags + parts.Tone,
                Why = Explain(article, parts, selectedValues, userTagWeights, userProfile.Mood),
                CreatedAt = article.CreatedAt,
                Publisher = article.RssItem.Feed?.Name,
                PublishedAt = article.RssItem.PubDate,
                IsSaved = savedIds.Contains(article.Id)
            };
        });
    }

    private List<string> ParseSelectedValues(string selectedValuesJson)
    {
        try
        {
            if (string.IsNullOrEmpty(selectedValuesJson))
                return new List<string>();
            
            // Parse the JSON array of strings
            var values = System.Text.Json.JsonSerializer.Deserialize<List<string>>(selectedValuesJson);
            return values ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse selected values JSON: {SelectedValuesJson}", selectedValuesJson);
            return new List<string>();
        }
    }

    private Dictionary<string, double> CalculateUserValueWeights(List<string> selectedValues)
    {
        var weights = new Dictionary<string, double>();
        
        for (int i = 0; i < selectedValues.Count; i++)
        {
            var valueId = selectedValues[i];
            // Exponential decay: weight = 1 / (base ^ position). Increasing the base will decrease the weight faster,
            // placing more emphasis on the values at the top of the stack rank.
            var weight = 1.0 / Math.Pow(ExponentialDecayBase, i);
            weights[valueId] = weight;
        }

        _logger.LogDebug("Calculated user value weights: {@Weights}", weights);
        return weights;
    }

    private Dictionary<string, double> CalculateUserTagWeights(UserProfile userProfile)
    {
        try
        {
            // Get tag preferences with weight > 0 (not banned)
            var tagPreferences = userProfile.TagPreferences
                .Where(tp => tp.Weight > 0)
                .OrderByDescending(tp => tp.Weight)
                .ToList();

            if (tagPreferences.Count == 0)
            {
                _logger.LogDebug("No favorite tag preferences found for user: {UserEmail}", userProfile.Email);
                return new Dictionary<string, double>();
            }

            // Group by weight to determine ranks
            var weightGroups = tagPreferences
                .GroupBy(tp => tp.Weight)
                .OrderByDescending(g => g.Key)
                .ToList();

            var tagWeights = new Dictionary<string, double>();
            var currentRank = 1;

            foreach (var weightGroup in weightGroups)
            {
                foreach (var preference in weightGroup)
                {
                    // Calculate weight using exponential decay based on rank
                    var weight = 1.0 / Math.Pow(ExponentialDecayBase, currentRank - 1);
                    tagWeights[preference.Tag] = weight;
                }
                currentRank++;
            }

            _logger.LogDebug("Calculated user tag weights for {TagCount} tags: {@TagWeights}", tagWeights.Count, tagWeights);
            return tagWeights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating user tag weights for user: {UserEmail}", userProfile.Email);
            return new Dictionary<string, double>();
        }
    }

    private (double Values, double Tags, double Tone) CalculateRelevanceParts(Article article, Dictionary<string, double> userValueWeights, Dictionary<string, double> userTagWeights, int minimumSentiment)
    {
        var valueAlignmentScore = CalculateValueAlignmentScore(article, userValueWeights);
        var tagAlignmentScore = CalculateTagAlignmentScore(article, userTagWeights);
        var sentimentDelta = article.Sentiment - minimumSentiment;

        _logger.LogDebug("Article {ArticleId}: valueAlignment={ValueAlignment}, tagAlignment={TagAlignment}, sentimentDelta={SentimentDelta}", 
            article.Id, valueAlignmentScore, tagAlignmentScore, sentimentDelta);

        return (ValueAlignmentWeight * valueAlignmentScore,
                TagAlignmentWeight * tagAlignmentScore,
                SentimentDeltaWeight * sentimentDelta);
    }

    private RelevanceBreakdown Explain(Article article, (double Values, double Tags, double Tone) parts, List<string> selectedValues, Dictionary<string, double> userTagWeights, int mood)
    {
        var total = parts.Values + parts.Tags + parts.Tone;
        var articleTags = (article.Tags ?? [])
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .ToHashSet();

        return new RelevanceBreakdown
        {
            ValuesShare = total > 0 ? parts.Values / total : 0,
            TagsShare = total > 0 ? parts.Tags / total : 0,
            ToneShare = total > 0 ? parts.Tone / total : 0,
            MatchedValues = selectedValues
                .Where(v => GetArticleValueScore(article, v) >= ValueMatchThreshold)
                .ToArray(),
            MatchedTags = userTagWeights
                .OrderByDescending(tw => tw.Value)
                .Select(tw => tw.Key)
                .Where(tag => articleTags.Contains(tag.ToLowerInvariant()))
                .ToArray(),
            Mood = mood
        };
    }

    private double CalculateValueAlignmentScore(Article article, Dictionary<string, double> userValueWeights)
    {
        var totalScore = 0.0;
        if (userValueWeights.Count == 0)
        {
            return totalScore;
        }

        foreach (var (valueId, weight) in userValueWeights)
        {
            var articleValueScore = GetArticleValueScore(article, valueId);
            totalScore += weight * articleValueScore;
        }

        // So that "more values" doesn't always mean "more relevant", we divide by the number of values
        return totalScore / userValueWeights.Count;
    }

    private double CalculateTagAlignmentScore(Article article, Dictionary<string, double> userTagWeights)
    {
        var totalScore = 0.0;
        if (userTagWeights.Count == 0 || article.Tags == null || article.Tags.Length == 0)
        {
            return totalScore;
        }

        // Normalize article tags for comparison
        var articleTags = article.Tags
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .ToHashSet();

        var matchedTags = 0;
        foreach (var (tag, weight) in userTagWeights)
        {
            var normalizedTag = tag.ToLowerInvariant();
            if (articleTags.Contains(normalizedTag))
            {
                totalScore += weight;
                matchedTags++;
            }
        }

        // If no tags matched, return 0
        if (matchedTags == 0)
        {
            return 0.0;
        }

        // Normalize by the number of matched tags to avoid bias towards articles with many tags
        return totalScore / matchedTags;
    }

    private int GetArticleValueScore(Article article, string valueId)
    {
        return valueId.ToLowerInvariant() switch
        {
            "freedom" => article.Freedom,
            "independence" => article.Independence,
            "self-respect" => article.SelfRespect,
            "self-actualization" => article.SelfActualization,
            "creativity" => article.Creativity,
            "honesty" => article.Honesty,
            "compassion" => article.Compassion,
            "loyalty" => article.Loyalty,
            "justice" => article.Justice,
            "responsibility" => article.Responsibility,
            "security" => article.Security,
            "equality" => article.Equality,
            "tradition" => article.Tradition,
            "obedience" => article.Obedience,
            "success" => article.Success,
            "ambition" => article.Ambition,
            "discipline" => article.Discipline,
            "knowledge" => article.Knowledge,
            "open-mindedness" => article.OpenMindedness,
            "peace-of-mind" => article.PeaceOfMind,
            "pleasure" => article.Pleasure,
            "connection" => article.Connection,
            "adventure" => article.Adventure,
            _ => 0 // Default to 0 for unknown values
        };
    }

    private static ArticlePresenter? PickForArticle(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
    {
        return articlePresenters.ByLabel(requestedPresenter)
               ?? articlePresenters.OrderBy(ap => ap.Id).FirstOrDefault();
    }

    private string GetPresenterContent(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
        => PickForArticle(articlePresenters, requestedPresenter)?.Content ?? string.Empty;

    private string GetPresenterTitle(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
        => PickForArticle(articlePresenters, requestedPresenter)?.Title ?? string.Empty;

    private string GetPresenterTldr(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
        => PickForArticle(articlePresenters, requestedPresenter)?.Tldr ?? string.Empty;
}