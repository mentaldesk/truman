using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentry;
using Truman.Data;
using Truman.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Messages;
using Truman.JobRunner.Jev;

namespace Truman.JobRunner;

public readonly record struct AnalysisOutcome(int Attempted, int Succeeded, int Failed);

#pragma warning disable SKEXP0070

public class ArticleAnalyser
{
    private readonly ILogger<ArticleAnalyser> _logger;
    private readonly IDbContextFactory<TrumanDbContext> _contextFactory;
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly JevArticleScorer _scorer;
    private readonly string _tagsInstructions;
    private readonly string _contentInstructions;
    private readonly OutputConfig _tagsOutput;
    private readonly OutputConfig _contentOutput;

    public ArticleAnalyser(ILogger<ArticleAnalyser> logger,
        IDbContextFactory<TrumanDbContext> contextFactory,
        AnthropicClient client,
        JevArticleScorer scorer,
        string model)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _client = client;
        _scorer = scorer;
        _model = model;

        _tagsInstructions = LoadInstructions("TagsInstructions.md");
        _contentInstructions = LoadInstructions("ContentInstructions.md");

        _tagsOutput = new OutputConfig
        {
            Format = new JsonOutputFormat { Schema = LoadSchema("TagsSchema.json") }
        };
        _contentOutput = new OutputConfig
        {
            Format = new JsonOutputFormat { Schema = LoadSchema("PresenterContentSchema.json") }
        };

        return;
        string LoadInstructions(string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));
    }

    private static string? ExtractText(Message message)
    {
        var text = string.Concat(message.Content.Select(block => block.Value is TextBlock textBlock ? textBlock.Text : string.Empty));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static Dictionary<string, JsonElement> LoadSchema(string fileName)
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName)))!;
        StripUnsupportedKeywords(schema);
        using var document = JsonDocument.Parse(schema.ToJsonString());
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    // Structured outputs reject these; each property's description already states its range.
    // https://platform.claude.com/docs/en/build-with-claude/structured-outputs
    private static readonly string[] UnsupportedSchemaKeywords =
        ["minimum", "maximum", "multipleOf", "minLength", "maxLength", "maxItems"];

    private static void StripUnsupportedKeywords(JsonNode node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var keyword in UnsupportedSchemaKeywords)
                {
                    jsonObject.Remove(keyword);
                }

                // minItems is accepted only as 0 or 1.
                if (jsonObject["minItems"]?.GetValue<int>() is { } minItems && minItems > 1)
                {
                    jsonObject.Remove("minItems");
                }
                foreach (var property in jsonObject.ToList())
                {
                    if (property.Value is { } value)
                    {
                        StripUnsupportedKeywords(value);
                    }
                }
                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    if (item is { } value)
                    {
                        StripUnsupportedKeywords(value);
                    }
                }
                break;
        }
    }

    const int MaxTokens = 10_000;

    public async Task<AnalysisOutcome> RunAsync(int? limit = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var pending = await db.RssItems
            .Where(x => x.TimeAnalysed == null)
            .ToListAsync();
        _logger.LogInformation("Found {PendingCount} RssItems to be analysed", pending.Count);
        if (pending.Count == 0)
        {
            return new AnalysisOutcome(0, 0, 0);
        }
        if (limit.HasValue)
        {
            _logger.LogInformation("Limiting analysis to {Limit} articles", limit.Value);
            pending = pending.Take(limit.Value).ToList();
        }
        return await AnalyzePendingArticles(pending, db);
    }

    private sealed class TagsData
    {
        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }
    }

    private async Task<string[]?> ProcessArticleTags(Message result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.StopReason is { } stopReason && stopReason != StopReason.EndTurn)
        {
            await RecordFailure(rssItem, $"stop_reason == {stopReason}", db, stage: "article-tags");
            return null;
        }
        if (ExtractText(result) is not { } responseContent)
        {
            await RecordFailure(rssItem, "No content returned", db, stage: "article-tags");
            return null;
        }
        try
        {
            if (JsonSerializer.Deserialize<TagsData>(responseContent)?.Tags is not { } tags)
            {
                await RecordFailure(rssItem, "Deserialization returned null", db, stage: "article-tags");
                return null;
            }

            return tags;
        }
        catch (JsonException ex)
        {
            await RecordFailure(rssItem, $"JSON deserialization failed: {ex.Message}", db, stage: "article-tags", ex, responseContent);
            _logger.LogError(ex, "JSON deserialization exception for {Link}. JSON: {JsonResponse}", rssItem.Link, responseContent);
            return null;
        }
    }

    // Round-tripping reuses ArticleData's JsonPropertyName map instead of restating 24 assignments.
    private static ArticleData BuildArticleData(RssItem rssItem, IReadOnlyDictionary<string, int> scores, string[] tags)
    {
        var payload = new Dictionary<string, object?>(scores.Count + 2)
        {
            ["link"] = rssItem.Link,
            ["tags"] = tags,
        };
        foreach (var (name, score) in scores)
        {
            payload[name] = score;
        }

        return JsonSerializer.Deserialize<ArticleData>(JsonSerializer.Serialize(payload))
               ?? throw new InvalidOperationException($"Could not assemble article data for {rssItem.Link}.");
    }

    private async Task<PresenterContentData?> ProcessArticleContent(Message result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.StopReason is { } stopReason && stopReason != StopReason.EndTurn)
        {
            await RecordFailure(rssItem, $"stop_reason == {stopReason}", db, stage: "presenter-content");
            return null;
        }
        if (ExtractText(result) is not { } responseContent)
        {
            await RecordFailure(rssItem, "No content returned", db, stage: "presenter-content");
            return null;
        }
        try
        {
            var presenterContent = JsonSerializer.Deserialize<PresenterContentData>(responseContent);
            if (presenterContent == null)
            {
                await RecordFailure(rssItem, "Deserialization returned null", db, stage: "presenter-content");
                return null;
            }

            return presenterContent;
        }
        catch (JsonException ex)
        {
            await RecordFailure(rssItem, $"JSON deserialization failed: {ex.Message}", db, stage: "presenter-content", ex, responseContent);
            _logger.LogError(ex, "JSON deserialization exception for {Link}. JSON: {JsonResponse}", rssItem.Link, responseContent);
            return null;
        }                
    }

    private static string DescribeArticle(RssItem rssItem)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rssItem.Title))
        {
            parts.Add($"Title: {rssItem.Title}");
        }
        parts.Add($"Link: {rssItem.Link}");
        if (!string.IsNullOrWhiteSpace(rssItem.Description))
        {
            parts.Add(string.Empty);
            parts.Add(rssItem.Description);
        }
        return string.Join("\n", parts);
    }

    private async Task<AnalysisOutcome> AnalyzePendingArticles(List<RssItem> pending, TrumanDbContext db)
    {

        var presenters = await db.Presenters.OrderBy(p => p.Id).ToListAsync();
        if (presenters.Count == 0)
        {
            _logger.LogWarning("No presenters configured - skipping analysis. Configure presenters via the admin UI.");
            return new AnalysisOutcome(0, 0, 0);
        }

        var attempted = 0;
        var succeeded = 0;
        foreach (var rssItem in pending)
        {
            attempted++;
            try
            {
                var scores = await _scorer.ScoreAsync(DescribeArticle(rssItem));

                var tagsResponse = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = _model,
                    MaxTokens = MaxTokens,
                    System = _tagsInstructions,
                    Messages = [new UserMessageParam { Content = $"The article to be tagged is:\n\n{DescribeArticle(rssItem)}" }],
                    OutputConfig = _tagsOutput,
                });
                if (await ProcessArticleTags(tagsResponse, rssItem, db) is not { } tags)
                {
                    continue;
                }

                var articleData = BuildArticleData(rssItem, scores, tags);

                var presenterContents = new Dictionary<int, PresenterContentData>();
                foreach (var presenter in presenters)
                {
                    var presenterResult = await _client.Messages.Create(new MessageCreateParams
                    {
                        Model = _model,
                        MaxTokens = MaxTokens,
                        System = _contentInstructions,
                        Messages = [new UserMessageParam { Content = $"Rewrite the following article in the style of {presenter.PresenterStyle}.\n\n{DescribeArticle(rssItem)}" }],
                        OutputConfig = _contentOutput,
                        });
                    if (await ProcessArticleContent(presenterResult, rssItem, db) is { } presenterContent)
                    {
                        presenterContents[presenter.Id] = presenterContent;
                    }
                }

                if (!ValidateArticleData(articleData, presenterContents, out var validationErrors))
                {
                    await RecordFailure(rssItem, $"Validation failed: {string.Join(", ", validationErrors)}", db, stage: "validation");
                    _logger.LogError("ArticleData validation failed for {Link}. Data: {@ArticleData}", rssItem.Link, articleData);
                    continue;
                }

                _logger.LogInformation("Successfully deserialized and aggregated ArticleData for {Link}", rssItem.Link);
                if (await SaveAnalysisResults(rssItem, articleData, presenterContents, db))
                {
                    succeeded++;
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                _logger.LogWarning(ex, "Skipping article {Link} due to unhandled exception", rssItem.Link);
                await RecordFailure(rssItem, $"Unhandled exception: {ex.Message}", db, stage: "analysis-loop", exception: ex);
            }
        }
        var outcome = new AnalysisOutcome(attempted, succeeded, attempted - succeeded);
        _logger.LogInformation(
            "Analysis complete: {Attempted} attempted, {Succeeded} succeeded, {Failed} failed",
            outcome.Attempted, outcome.Succeeded, outcome.Failed);
        return outcome;
    }

    private async Task<bool> SaveAnalysisResults(RssItem rssItem, ArticleData articleData, Dictionary<int, PresenterContentData> presenterContents, TrumanDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Create a new Article record
            var article = new Article
            {
                Link = articleData.Link,
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
            await db.SaveChangesAsync(); // Save to get the article ID
            
            // Save presenter content using presenter IDs as keys
            foreach (var (presenterId, content) in presenterContents)
            {
                var articlePresenter = new ArticlePresenter
                {
                    ArticleId = article.Id,
                    PresenterId = presenterId,
                    Title = content.Title,
                    Tldr = content.Tldr,
                    Content = content.Content
                };
                
                db.ArticlePresenters.Add(articlePresenter);
            }
            
            // Mark RssItem as analysed
            rssItem.TimeAnalysed = DateTimeOffset.UtcNow;
            
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Successfully saved analysis for {Link}", rssItem.Link);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await RecordFailure(rssItem, $"Database save failed: {ex.Message}", db, stage: "save-results", exception: ex);
            return false;
        }
    }

    private async Task RecordFailure(RssItem rssItem, string reason, TrumanDbContext db, string? stage = null, Exception? exception = null, string? responseContent = null)
    {
        _logger.LogError("Article analysis failed for {Link}: {Reason}", rssItem.Link, reason);

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("analysis.link", rssItem.Link);
            scope.SetTag("analysis.stage", stage ?? "unknown");
            scope.SetExtra("analysis.reason", reason);
            if (responseContent is not null)
            {
                scope.SetExtra("analysis.response_content", responseContent);
            }
        });

        if (exception is not null)
        {
            SentrySdk.CaptureException(exception);
        }
        else
        {
            SentrySdk.CaptureMessage($"Article analysis failed at {stage ?? "unknown"}: {reason}", SentryLevel.Error);
        }
        
        // Mark the RssItem as analysed to prevent infinite retry loops
        rssItem.TimeAnalysed = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private bool ValidateArticleData(ArticleData? articleData, Dictionary<int, PresenterContentData> presenterContents, out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        
        if (articleData == null)
        {
            validationErrors.Add("JSON deserialization returned null");
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(articleData.Link))
            validationErrors.Add("Link is null or empty");
        if (!presenterContents.Any())
            validationErrors.Add("No presenter content generated");
        
        foreach (var (presenterId, content) in presenterContents)
        {
            if (string.IsNullOrWhiteSpace(content.Title))
                validationErrors.Add($"Title is null or empty for presenter {presenterId}");
            if (string.IsNullOrWhiteSpace(content.Tldr))
                validationErrors.Add($"Tldr is null or empty for presenter {presenterId}");
            if (string.IsNullOrWhiteSpace(content.Content))
                validationErrors.Add($"Content is null or empty for presenter {presenterId}");
        }
        
        return !validationErrors.Any();
    }
}

#pragma warning restore SKEXP0070