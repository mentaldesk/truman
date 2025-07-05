using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Truman.Data.Entities;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.Google;

namespace Truman.JobRunner;

#pragma warning disable SKEXP0070

public class ArticleAnalyser
{
    private readonly ILogger<ArticleAnalyser> _logger;
    private readonly IDbContextFactory<TrumanDbContext> _contextFactory;
    private readonly Kernel _kernel;
    private readonly string _instructions;
    private readonly GeminiPromptExecutionSettings _promptSettings;

    public ArticleAnalyser(ILogger<ArticleAnalyser> logger,
        IDbContextFactory<TrumanDbContext> contextFactory,
        Kernel kernel)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _kernel = kernel;

        var instructionsPath = Path.Combine(AppContext.BaseDirectory, "AnalyserInstructions.md");
        _instructions = File.ReadAllText(instructionsPath);
        
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "AnalyserSchema.json");
        var jsonSchemaText = File.ReadAllText(schemaPath);
        var jsonSchema = System.Text.Json.JsonDocument.Parse(jsonSchemaText).RootElement;
        _promptSettings = new GeminiPromptExecutionSettings
        {
            MaxTokens = MaxTokens, 
            ResponseMimeType = "application/json",
            ResponseSchema = jsonSchema
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

    private async Task AnalyzePendingArticles(List<RssItem> pending, TrumanDbContext db)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        int count = 0;
        
        foreach (var rssItem in pending)
        {
            count++;
            if (count > 1) break;

            var chatHistory = new ChatHistory(_instructions);
            chatHistory.AddUserMessage($"The article to be analysed is: {rssItem.Link}");
            var result = await chatService.GetChatMessageContentAsync(chatHistory, _promptSettings, _kernel);
            _logger.LogInformation("PromptTokenCount to analyse {Link}: {Tokens}", rssItem.Link, result.Metadata.ReadValue<int>("PromptTokenCount"));
            _logger.LogInformation("TotalTokenCount to analyse {Link}: {Tokens}", rssItem.Link, result.Metadata.ReadValue<int>("TotalTokenCount"));

            if (result.Metadata!.ReadValue<GeminiFinishReason>("FinishReason") is { } finishReason && finishReason.Label != "STOP")
            {
                RecordFailure(rssItem, $"GeminiFinishReason == {finishReason}");
                continue;
            }

            if (result.Content is not { } responseContent)
            {
                RecordFailure(rssItem, "No content returned");
                continue;
            }
            
            // Log the full response
            _logger.LogInformation("Analysis results for {Link}:", rssItem.Link);
            _logger.LogInformation("{Response}", responseContent);
        }
        
        _logger.LogInformation("Analysis {Count} articles - job complete", count);
    }

    private void RecordFailure(RssItem rssItem, string reason)
    {
        _logger.LogError("Article analysis failed for {Link}: {Reason}", rssItem.Link, reason);
        // TODO: We should still mark the article as analysed so that we don't retry it 
    }
} 
#pragma warning restore SKEXP0070