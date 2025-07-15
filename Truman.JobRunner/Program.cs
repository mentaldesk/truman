using System.Net;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly;
using Truman.Data;
using Truman.JobRunner;
using DotNetEnv;
using DotNetEnv.Configuration;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#endif

#pragma warning disable SKEXP0070
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // Add dotnet-env configuration source to load from .env file
        config.AddDotNetEnv(".env", LoadOptions.TraversePath());
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        // Add database context factory instead of scoped DbContext
        var connectionString = context.Configuration.GetPostgresConnectionString();
            
        // Debug: Print current environment
        Console.WriteLine($"Current environment: {context.HostingEnvironment.EnvironmentName}");
            
        services.AddDbContextFactory<TrumanDbContext>(options =>
            options.UseNpgsql(connectionString));
            
        // Register as Singleton since this is a console app with a single operation
        services.AddSingleton<RssFetcher>();
        services.AddSingleton<ArticleAnalyser>();

        services
            .AddHttpClient("GeminiClient")
            .RedactLoggedHeaders(["Authorization"])
            .AddResilienceHandler("gemini-pipeline", static pipeline =>
            {
                // Retry with exponential backoff on 429 responses
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMinutes(1),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                });

                // Add timeout per request
                pipeline.AddTimeout(TimeSpan.FromSeconds(10));
            });        
        
        // Register Semantic Kernel
        var aiModel = context.Configuration["AI:Model"] 
            ?? throw new InvalidOperationException("AI Model not found in configuration.");
        var apiKey = context.Configuration["AI:ApiKey"] 
            ?? throw new InvalidOperationException("AI API key not found in configuration.");
           
        // var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        services.AddTransient<Kernel>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("GeminiClient");

            return Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion(aiModel, apiKey, httpClient: httpClient)
                .Build();
        });
    })
    .Build();

if (args.Contains("--fetch"))
{
    var fetcher = host.Services.GetRequiredService<RssFetcher>();
    await fetcher.RunAsync();
}
else if (args.Contains("--analyse"))
{
    var analyser = host.Services.GetRequiredService<ArticleAnalyser>();
    await analyser.RunAsync();
}
else
{
    // By default, we fetch and analyse
    var fetcher = host.Services.GetRequiredService<RssFetcher>();
    await fetcher.RunAsync();

    var analyser = host.Services.GetRequiredService<ArticleAnalyser>();
    await analyser.RunAsync();
}
#pragma warning restore SKEXP0070
