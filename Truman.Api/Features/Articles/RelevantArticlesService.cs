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
    private const double ValueAlignmentWeight = 0.7;
    private const double SentimentDeltaWeight = 0.3;
    private const double ExponentialDecayBase = 1.5;

    public RelevantArticlesService(TrumanDbContext dbContext, ILogger<RelevantArticlesService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RelevantArticlesResponse> GetRelevantArticlesAsync(RelevantArticlesRequest request)
    {
        _logger.LogInformation("Getting relevant articles for user with {ValueCount} values and minimum sentiment {MinSentiment}", 
            request.SelectedValues.Count, request.MinimumSentiment);

        // Calculate user value weights using exponential decay
        var userValueWeights = CalculateUserValueWeights(request.SelectedValues);

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
            RelevanceScore = CalculateRelevanceScore(article, userValueWeights, request.MinimumSentiment)
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

    private double CalculateRelevanceScore(Article article, Dictionary<string, double> userValueWeights, int minimumSentiment)
    {
        // Calculate value alignment score (weighted dot product)
        var valueAlignmentScore = CalculateValueAlignmentScore(article, userValueWeights);
        
        // Calculate sentiment delta
        var sentimentDelta = article.Sentiment - minimumSentiment;
        
        // Calculate overall relevance score
        var relevanceScore = ValueAlignmentWeight * valueAlignmentScore + SentimentDeltaWeight * sentimentDelta;
        
        _logger.LogDebug("Article {ArticleId}: valueAlignment={ValueAlignment}, sentimentDelta={SentimentDelta}, relevance={Relevance}", 
            article.Id, valueAlignmentScore, sentimentDelta, relevanceScore);
        
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
    
static class ArticlePresenterExtensions
{
    public static ArticlePresenter? ByLabel(this ICollection<ArticlePresenter> articlePresenters, string presenter) =>
        articlePresenters.FirstOrDefault(
            ap => ap.Presenter.Label.Equals(presenter, StringComparison.OrdinalIgnoreCase));
}
