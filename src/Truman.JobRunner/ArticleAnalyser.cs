using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentry;
using Truman.Data;
using Truman.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Truman.JobRunner.Jev;

namespace Truman.JobRunner;

public class ArticleAnalyser
{
    private readonly ILogger<ArticleAnalyser> _logger;
    private readonly IDbContextFactory<TrumanDbContext> _contextFactory;
    private readonly IChatClient _chatClient;
    private readonly JevArticleScorer _scorer;
    private readonly string _analysisInstructions;
    private readonly string _contentInstructions;
    private readonly ChatOptions _analysisOptions;
    private readonly ChatOptions _contentOptions;

    public ArticleAnalyser(ILogger<ArticleAnalyser> logger,
        IDbContextFactory<TrumanDbContext> contextFactory,
        IChatClient chatClient,
        JevArticleScorer scorer,
        string model)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _chatClient = chatClient;
        _scorer = scorer;

        _analysisInstructions = LoadInstructions("AnalysisInstructions.md");
        _contentInstructions = LoadInstructions("ContentInstructions.md");

        _analysisOptions = BuildOptions(model, "ArticleAnalysisSchema.json", "article_analysis");
        _contentOptions = BuildOptions(model, "PresenterContentSchema.json", "presenter_content");

        return;
        string LoadInstructions(string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));
    }

    private static ChatOptions BuildOptions(string model, string schemaFile, string schemaName) => new()
    {
        ModelId = model,
        MaxOutputTokens = MaxTokens,
        Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
        ResponseFormat = ChatResponseFormat.ForJsonSchema(LoadSchema(schemaFile), schemaName),
    };

    private static JsonElement LoadSchema(string fileName)
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName)))!;
        StripUnsupportedKeywords(schema);
        using var document = JsonDocument.Parse(schema.ToJsonString());
        return document.RootElement.Clone();
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

    public async Task RunAsync(int? limit = null)
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
        if (limit.HasValue)
        {
            _logger.LogInformation("Limiting analysis to {Limit} articles", limit.Value);
            pending = pending.Take(limit.Value).ToList();
        }
        await AnalyzePendingArticles(pending, db);
    }

    private sealed class ArticleAnalysisData
    {
        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }

        [JsonPropertyName("claim")]
        public string? Claim { get; set; }

        [JsonPropertyName("support")]
        public string[]? Support { get; set; }

        [JsonPropertyName("padding")]
        public string[]? Padding { get; set; }

        [JsonPropertyName("headline_gap")]
        public string? HeadlineGap { get; set; }

        [JsonPropertyName("source_depth")]
        public string? SourceDepth { get; set; }
    }

    private async Task<ArticleAnalysisData?> ProcessArticleAnalysis(ChatResponse result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.FinishReason is { } finishReason && finishReason != ChatFinishReason.Stop)
        {
            await RecordFailure(rssItem, $"finish_reason == {finishReason}", db, stage: "article-analysis");
            return null;
        }
        if (NonEmptyText(result) is not { } responseContent)
        {
            await RecordFailure(rssItem, "No content returned", db, stage: "article-analysis");
            return null;
        }
        try
        {
            if (JsonSerializer.Deserialize<ArticleAnalysisData>(responseContent) is not { Tags: not null } analysis)
            {
                await RecordFailure(rssItem, "Deserialization returned null", db, stage: "article-analysis");
                return null;
            }

            return analysis;
        }
        catch (JsonException ex)
        {
            await RecordFailure(rssItem, $"JSON deserialization failed: {ex.Message}", db, stage: "article-analysis", ex, responseContent);
            _logger.LogError(ex, "JSON deserialization exception for {Link}. JSON: {JsonResponse}", rssItem.Link, responseContent);
            return null;
        }
    }

    // A missing claim is not fatal: the presenter falls back to summarising the article unaided.
    private static string? DescribeStructure(ArticleAnalysisData analysis)
    {
        if (string.IsNullOrWhiteSpace(analysis.Claim))
        {
            return null;
        }

        var lines = new List<string>
        {
            "Structural analysis of the article:",
            $"- Claim: {analysis.Claim}",
            $"- Support: {Join(analysis.Support) ?? "(none identified)"}",
        };
        if (Join(analysis.Padding) is { } padding)
        {
            lines.Add($"- Padding, do not spend words on: {padding}");
        }
        if (!string.IsNullOrWhiteSpace(analysis.HeadlineGap))
        {
            lines.Add($"- Headline relation to claim: {analysis.HeadlineGap}");
        }
        if (!string.IsNullOrWhiteSpace(analysis.SourceDepth))
        {
            lines.Add($"- Source depth: {analysis.SourceDepth}");
        }
        return string.Join("\n", lines);

        static string? Join(string[]? values)
        {
            var joined = string.Join("; ", (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrEmpty(joined) ? null : joined;
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

    private async Task<PresenterContentData?> ProcessArticleContent(ChatResponse result, RssItem rssItem, TrumanDbContext db)
    {
        if (result.FinishReason is { } finishReason && finishReason != ChatFinishReason.Stop)
        {
            await RecordFailure(rssItem, $"finish_reason == {finishReason}", db, stage: "presenter-content");
            return null;
        }
        if (NonEmptyText(result) is not { } responseContent)
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

    private static string? NonEmptyText(ChatResponse response) =>
        string.IsNullOrWhiteSpace(response.Text) ? null : response.Text;

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

    private async Task AnalyzePendingArticles(List<RssItem> pending, TrumanDbContext db)
    {

        var presenters = await db.Presenters.OrderBy(p => p.Id).ToListAsync();
        if (presenters.Count == 0)
        {
            _logger.LogWarning("No presenters configured - skipping analysis. Configure presenters via the admin UI.");
            return;
        }

        int count = 0;
        foreach (var rssItem in pending)
        {
            // Without an ambient transaction, each LLM call below would become its own.
            using var sentryScope = SentrySdk.PushScope();
            var transaction = SentrySdk.StartTransaction("analyse-article", "gen_ai.pipeline");
            SentrySdk.ConfigureScope(configured => configured.Transaction = transaction);
            transaction.SetTag("analysis.link", rssItem.Link);

            try
            {
                count++;

                var scores = await _scorer.ScoreAsync(DescribeArticle(rssItem));

                var analysisResponse = await _chatClient.GetResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, _analysisInstructions),
                        new ChatMessage(ChatRole.User, $"The article to be analysed is:\n\n{DescribeArticle(rssItem)}"),
                    ],
                    _analysisOptions);
                if (await ProcessArticleAnalysis(analysisResponse, rssItem, db) is not { Tags: { } tags } analysis)
                {
                    continue;
                }

                var articleData = BuildArticleData(rssItem, scores, tags);
                var structure = DescribeStructure(analysis);
                var presenterSource = structure is null
                    ? DescribeArticle(rssItem)
                    : $"{structure}\n\n{DescribeArticle(rssItem)}";

                var presenterContents = new Dictionary<int, PresenterContentData>();
                foreach (var presenter in presenters)
                {
                    var presenterResult = await _chatClient.GetResponseAsync(
                        [
                            new ChatMessage(ChatRole.System, _contentInstructions),
                            new ChatMessage(ChatRole.User, $"Rewrite the following article in the style of {presenter.PresenterStyle}.\n\n{presenterSource}"),
                        ],
                        _contentOptions);
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
                await SaveAnalysisResults(rssItem, articleData, presenterContents, db);
            }
            catch (Exception ex)
            {
                transaction.Finish(ex);
                SentrySdk.CaptureException(ex);
                _logger.LogWarning(ex, "Skipping article {Link} due to unhandled exception", rssItem.Link);
                await RecordFailure(rssItem, $"Unhandled exception: {ex.Message}", db, stage: "analysis-loop", exception: ex);
            }
            finally
            {
                transaction.Finish(); // Idempotent; the catch above may already have.
            }
        }
        _logger.LogInformation("Analysis {Count} articles - job complete", count);
    }

    private async Task SaveAnalysisResults(RssItem rssItem, ArticleData articleData, Dictionary<int, PresenterContentData> presenterContents, TrumanDbContext db)
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
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await RecordFailure(rssItem, $"Database save failed: {ex.Message}", db, stage: "save-results", exception: ex);
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
