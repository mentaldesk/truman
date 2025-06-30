using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;

namespace Truman.JobRunner;

public class ArticleAnalyser(
    IHttpClientFactory httpClientFactory,
    ILogger<ArticleAnalyser> logger,
    IDbContextFactory<TrumanDbContext> contextFactory)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IDbContextFactory<TrumanDbContext> _contextFactory = contextFactory;

    public async Task RunAsync()
    {
        logger.LogInformation("ArticleAnalyser job ran (stub).");
        await Task.CompletedTask;
    }
} 