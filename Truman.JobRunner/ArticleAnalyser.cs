using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Truman.Data.Entities;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Truman.JobRunner;

#pragma warning disable SKEXP0070

public class ArticleAnalyser
{
    private readonly ILogger<ArticleAnalyser> _logger;
    private readonly IDbContextFactory<TrumanDbContext> _contextFactory;
    private readonly Kernel _kernel;
    private readonly string _analysisInstructions;
    private readonly GeminiPromptExecutionSettings _analyserPromptSettings;
    private readonly GeminiPromptExecutionSettings _contentPromptSettings;
    private readonly IConfiguration _configuration;

    public ArticleAnalyser(ILogger<ArticleAnalyser> logger,
        IDbContextFactory<TrumanDbContext> contextFactory,
        Kernel kernel,
        IConfiguration configuration)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _kernel = kernel;
        _configuration = configuration;

        var instructionsPath = Path.Combine(AppContext.BaseDirectory, "AnalyserInstructions.md");
        _analysisInstructions = File.ReadAllText(instructionsPath);

        var analyserSchemaPath = Path.Combine(AppContext.BaseDirectory, "AnalyserSchema.json");
        var analyserSchema = JsonDocument.Parse(File.ReadAllText(analyserSchemaPath)).RootElement;
        _analyserPromptSettings = new GeminiPromptExecutionSettings
        {
            MaxTokens = MaxTokens, 
            ResponseMimeType = "application/json",
            ResponseSchema = analyserSchema
        };

        var contentSchemaPath = Path.Combine(AppContext.BaseDirectory, "ContentSchema.json");
        var contentSchema = JsonDocument.Parse(File.ReadAllText(contentSchemaPath)).RootElement;
        _contentPromptSettings = new GeminiPromptExecutionSettings
        {
            MaxTokens = MaxTokens, 
            ResponseMimeType = "application/json",
            ResponseSchema = contentSchema
        };
    }

    const int MaxTokens = 10_000;

    public async Task RunAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var pending = await db.RssItems
            .Where(x => x.TimeAnalysed == null)
            .ToListAsync();
        _logger.LogInformation("Found {PendingCount} RssItems to be analysed", pending.Count);
        if (pending.Count == 0)
        {
            return;
        }
        await AnalyzePendingArticles(pending, db);
    }

    private async Task<ArticleData?> ProcessArticleAnalysis(ChatMessageContent result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.Metadata!.ReadValue<GeminiFinishReason>("FinishReason") is { } finishReason && finishReason.Label != "STOP")
        {
            await RecordFailure(rssItem, $"GeminiFinishReason == {finishReason}", db);
            return null;
        }
        if (result.Content is not { } responseContent)
        {
            await RecordFailure(rssItem, "No content returned", db);
            return null;
        }
        _logger.LogInformation("Analysis results for {Link}:", rssItem.Link);
        _logger.LogInformation("{Response}", responseContent);
        try
        {
            var articleData = JsonSerializer.Deserialize<ArticleData>(result.Content);
            if (articleData == null)
            {
                await RecordFailure(rssItem, "Deserialization returned null", db);
                return null;
            }

            return articleData;
        }
        catch (JsonException ex)
        {
            await RecordFailure(rssItem, $"JSON deserialization failed: {ex.Message}", db);
            _logger.LogError(ex, "JSON deserialization exception for {Link}. JSON: {JsonResponse}", rssItem.Link, responseContent);
            return null;
        }
    }

    private async Task<string?> ProcessArticleContent(ChatMessageContent result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.Metadata!.ReadValue<GeminiFinishReason>("FinishReason") is { } finishReason && finishReason.Label != "STOP")
        {
            await RecordFailure(rssItem, $"GeminiFinishReason == {finishReason}", db);
            return null;
        }
        if (result.Content is not { } responseContent)
        {
            await RecordFailure(rssItem, "No content returned", db);
            return null;
        }
        try
        {
            var presenterContent = JsonSerializer.Deserialize<ArticleContent>(responseContent);
            if (presenterContent == null)
            {
                await RecordFailure(rssItem, "Deserialization returned null", db);
                return null;
            }

            return presenterContent.Content;
        }
        catch (JsonException ex)
        {
            await RecordFailure(rssItem, $"JSON deserialization failed: {ex.Message}", db);
            _logger.LogError(ex, "JSON deserialization exception for {Link}. JSON: {JsonResponse}", rssItem.Link, responseContent);
            return null;
        }                
    }

    private async Task AnalyzePendingArticles(List<RssItem> pending, TrumanDbContext db)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var presenterStyles = _configuration.GetSection("PresenterStyles").Get<List<string>>() ?? new List<string> { "A typical British news anchor" };
        int count = 0;
        foreach (var rssItem in pending)
        {
            count++;

            var chatHistory = new ChatHistory(_analysisInstructions);
            chatHistory.AddUserMessage($"The article to be analysed is: {rssItem.Link}");
            var analyserResponse = await chatService.GetChatMessageContentAsync(chatHistory, _analyserPromptSettings, _kernel);
            if (await ProcessArticleAnalysis(analyserResponse, rssItem, db) is not { } articleData)
            {
                continue;
            }

            foreach (var presenter in presenterStyles)
            {
                var presenterChatHistory = new ChatHistory("Look on the bright side and emphasise what good can be found.");
                presenterChatHistory.AddUserMessage($"Rewrite the article at {rssItem.Link} in no more than 400 words, in the style of {presenter}.");
                var presenterResult = await chatService.GetChatMessageContentAsync(presenterChatHistory,
                    _contentPromptSettings, _kernel);
                if (await ProcessArticleContent(presenterResult, rssItem, db) is { } content)
                {
                    articleData.PresenterContents[presenter] = content;
                }
            }

            if (!ValidateArticleData(articleData, out var validationErrors))
            {
                await RecordFailure(rssItem, $"Validation failed: {string.Join(", ", validationErrors)}", db);
                _logger.LogError("ArticleData validation failed for {Link}. Data: {@ArticleData}", rssItem.Link, articleData);
                continue;
            }

            _logger.LogInformation("Successfully deserialized and aggregated ArticleData for {Link}", rssItem.Link);
            await SaveAnalysisResults(rssItem, articleData, db);
        }
        _logger.LogInformation("Analysis {Count} articles - job complete", count);
    }

    private async Task SaveAnalysisResults(RssItem rssItem, ArticleData articleData, TrumanDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Create a new Article record
            var article = new Article
            {
                Link = articleData.Link,
                Title = articleData.Title,
                Tldr = articleData.Tldr,
                PresenterContents = articleData.PresenterContents,
                Sentiment = articleData.Sentiment,
                Tags = articleData.Tags,
                Freedom = articleData.Freedom,
                Independence = articleData.Independence,
                SelfRespect = articleData.SelfRespect,
                SelfActualization = articleData.SelfActualization,
                Creativity = articleData.Creativity,
                Honesty = articleData.Honesty,
                Compassion = articleData.Compassion,
                Loyalty = articleData.Loyalty,
                Justice = articleData.Justice,
                Responsibility = articleData.Responsibility,
                Security = articleData.Security,
                Equality = articleData.Equality,
                Tradition = articleData.Tradition,
                Obedience = articleData.Obedience,
                Success = articleData.Success,
                Ambition = articleData.Ambition,
                Discipline = articleData.Discipline,
                Knowledge = articleData.Knowledge,
                OpenMindedness = articleData.OpenMindedness,
                PeaceOfMind = articleData.PeaceOfMind,
                Pleasure = articleData.Pleasure,
                Connection = articleData.Connection,
                Adventure = articleData.Adventure,
                RssItemId = rssItem.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            db.Articles.Add(article);
            
            // Mark RssItem as analysed
            rssItem.TimeAnalysed = DateTimeOffset.UtcNow;
            
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Successfully saved analysis for {Link}", rssItem.Link);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await RecordFailure(rssItem, $"Database save failed: {ex.Message}", db);
        }
    }

    private async Task RecordFailure(RssItem rssItem, string reason, TrumanDbContext db)
    {
        _logger.LogError("Article analysis failed for {Link}: {Reason}", rssItem.Link, reason);
        
        // Mark the RssItem as analysed to prevent infinite retry loops
        rssItem.TimeAnalysed = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private bool ValidateArticleData(ArticleData? articleData, out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        
        if (articleData == null)
        {
            validationErrors.Add("JSON deserialization returned null");
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(articleData.Link))
            validationErrors.Add("Link is null or empty");
        if (string.IsNullOrWhiteSpace(articleData.Title))
            validationErrors.Add("Title is null or empty");
        if (string.IsNullOrWhiteSpace(articleData.Tldr))
            validationErrors.Add("Tldr is null or empty");
        if (string.IsNullOrWhiteSpace(articleData.PresenterContents.Values.FirstOrDefault()))
            validationErrors.Add("Content is null or empty");
        
        return !validationErrors.Any();
    }
}

#pragma warning restore SKEXP0070