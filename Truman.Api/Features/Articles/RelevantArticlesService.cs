using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

public class RelevantArticlesService : IRelevantArticlesService
{
    private const string DefaultPresenter = "Default";
    private readonly TrumanDbContext _dbContext;
    private readonly ILogger<RelevantArticlesService> _logger;
    
    // Weighting constants for relevance calculation
    private const double ValueAlignmentWeight = 0.4;
    private const double TagAlignmentWeight = 0.4;
    private const double SentimentDeltaWeight = 0.2;
    private const double ExponentialDecayBase = 1.5;

    public RelevantArticlesService(TrumanDbContext dbContext, ILogger<RelevantArticlesService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RelevantArticlesResponse> GetRelevantArticlesAsync(RelevantArticlesRequest request, string? userEmail)
    {
        _logger.LogInformation("Getting relevant articles for user {UserEmail} with {ValueCount} values and minimum sentiment {MinSentiment}", 
            userEmail, request.SelectedValues.Count, request.MinimumSentiment);

        // Calculate user value weights using exponential decay
        var userValueWeights = CalculateUserValueWeights(request.SelectedValues);

        // Calculate user tag weights using ranking system
        var userTagWeights = await CalculateUserTagWeights(userEmail);

        // Get today's date for filtering (using UTC to avoid timezone issues with PostgreSQL)
        var earliest = DateTime.UtcNow.Date.AddDays(-1);

        // Get all articles that meet the minimum sentiment threshold and are from today
        var articles = await _dbContext.Articles
            .Include(a => a.ArticlePresenters)
                .ThenInclude(ap => ap.Presenter)
            .Where(a => a.Sentiment >= request.MinimumSentiment)
            .Where(a => a.CreatedAt >= earliest)
            .ToListAsync();

        _logger.LogInformation("Found {ArticleCount} articles from yesterday and today with minimum sentiment {MinSentiment}", 
            articles.Count, request.MinimumSentiment);

        // Calculate relevance scores for each article
        var articlesWithScores = articles.Select(article => new
        {
            Article = article,
            RelevanceScore = CalculateRelevanceScore(article, userValueWeights, userTagWeights, request.MinimumSentiment)
        })
        .OrderByDescending(x => x.RelevanceScore);

        // Convert to response DTOs
       
        // Find a presenter whose name starts with the requested presenter
        var relevantArticles = articlesWithScores.Select(x => new RelevantArticle
        {
            Id = x.Article.Id,
            Link = x.Article.Link,
            Title = GetPresenterTitle(x.Article.ArticlePresenters, request.Presenter),
            Tldr = GetPresenterTldr(x.Article.ArticlePresenters, request.Presenter),
            Content = GetPresenterContent(x.Article.ArticlePresenters, request.Presenter),
            Sentiment = x.Article.Sentiment,
            Tags = x.Article.Tags,
            RelevanceScore = x.RelevanceScore,
            CreatedAt = x.Article.CreatedAt
        }).ToList();

        return new RelevantArticlesResponse
        {
            Articles = relevantArticles
        };
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

    private async Task<Dictionary<string, double>> CalculateUserTagWeights(string? userEmail)
    {
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogDebug("No user email available, skipping tag preference calculation");
            return new Dictionary<string, double>();
        }

        try
        {
            // Get user profile with tag preferences
            var userProfile = await _dbContext.UserProfiles
                .Include(u => u.TagPreferences)
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (userProfile == null)
            {
                _logger.LogDebug("User profile not found for email: {UserEmail}", userEmail);
                return new Dictionary<string, double>();
            }

            // Get tag preferences with weight > 0 (not banned)
            var tagPreferences = userProfile.TagPreferences
                .Where(tp => tp.Weight > 0)
                .OrderByDescending(tp => tp.Weight)
                .ToList();

            if (!tagPreferences.Any())
            {
                _logger.LogDebug("No tag preferences found for user: {UserEmail}", userEmail);
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
            _logger.LogError(ex, "Error calculating user tag weights for user: {UserEmail}", userEmail);
            return new Dictionary<string, double>();
        }
    }

    private double CalculateRelevanceScore(Article article, Dictionary<string, double> userValueWeights, Dictionary<string, double> userTagWeights, int minimumSentiment)
    {
        // Calculate value alignment score (weighted dot product)
        var valueAlignmentScore = CalculateValueAlignmentScore(article, userValueWeights);
        
        // Calculate tag alignment score
        var tagAlignmentScore = CalculateTagAlignmentScore(article, userTagWeights);
        
        // Calculate sentiment delta
        var sentimentDelta = article.Sentiment - minimumSentiment;
        
        // Calculate overall relevance score
        var relevanceScore = ValueAlignmentWeight * valueAlignmentScore + 
                           TagAlignmentWeight * tagAlignmentScore + 
                           SentimentDeltaWeight * sentimentDelta;
        
        _logger.LogDebug("Article {ArticleId}: valueAlignment={ValueAlignment}, tagAlignment={TagAlignment}, sentimentDelta={SentimentDelta}, relevance={Relevance}", 
            article.Id, valueAlignmentScore, tagAlignmentScore, sentimentDelta, relevanceScore);
        
        return relevanceScore;
    }

    private double CalculateValueAlignmentScore(Article article, Dictionary<string, double> userValueWeights)
    {
        var totalScore = 0.0;
        if (!userValueWeights.Any())
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
        if (!userTagWeights.Any() || article.Tags == null || article.Tags.Length == 0)
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

    private string GetPresenterContent(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
    {
        return articlePresenters.ByLabel(requestedPresenter)?.Content 
               ?? articlePresenters.ByLabel(DefaultPresenter)?.Content
               ?? string.Empty;
    }

    private string GetPresenterTitle(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
    {
        return articlePresenters.ByLabel(requestedPresenter)?.Title 
               ?? articlePresenters.ByLabel(DefaultPresenter)?.Title 
               ?? string.Empty;
    }

    private string GetPresenterTldr(ICollection<ArticlePresenter> articlePresenters, string requestedPresenter)
    {
        return articlePresenters.ByLabel(requestedPresenter)?.Tldr 
               ?? articlePresenters.ByLabel(DefaultPresenter)?.Tldr 
               ?? string.Empty;
    }
}