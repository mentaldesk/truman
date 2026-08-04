using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly;
using Truman.Data;
using Truman.Diagnostics.Metrics;
using Truman.JobRunner;
using DotNetEnv;
using DotNetEnv.Configuration;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#endif

#pragma warning disable SKEXP0070
try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            // Add dotnet-env configuration source to load from .env file
            config.AddDotNetEnv(".env", LoadOptions.TraversePath());
        })
        .ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();
            logging.AddSentry(options =>
            {
#if DEBUG                
                options.Debug = true;
#endif
                options.Dsn = context.Configuration["Sentry:Dsn"];
                // Sentry.AspNetCore bridges IWebHostEnvironment to the Sentry environment for us.
                // Sentry.Extensions.Logging has no equivalent, so a generic-host app resolves it
                // itself. Mirror the precedence Sentry.AspNetCore applies, so the API and the
                // JobRunner always report the same environment:
                //   1. SENTRY_ENVIRONMENT, used verbatim
                //   2. otherwise DOTNET_ENVIRONMENT, via IHostEnvironment, lower-cased to Sentry's
                //      convention for the three standard names and passed through for custom ones
                // There is no third case: .NET already defaults EnvironmentName to Production.
                var hostEnvironment = context.HostingEnvironment;
                var sentryEnvironment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT");
                if (string.IsNullOrWhiteSpace(sentryEnvironment))
                {
                    sentryEnvironment =
                        hostEnvironment.IsProduction() ? "production" :
                        hostEnvironment.IsStaging() ? "staging" :
                        hostEnvironment.IsDevelopment() ? "development" :
                        hostEnvironment.EnvironmentName;
                }
                options.Environment = sentryEnvironment;
                // Deliberately 1.0, unlike the API. This is a scheduled batch job with bounded
                // volume — a handful of runs a day, not a request per user — so full sampling
                // costs almost nothing and a sampled-out run is one we cannot investigate.
                options.TracesSampleRate = 1.0;
                options.CaptureFailedRequests = true;
                options.SendDefaultPii = true;
                options.StackTraceMode = StackTraceMode.Enhanced;
            });
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
            services.AddSingleton<ArticleAnalyser>(sp =>
                new ArticleAnalyser(
                    sp.GetRequiredService<ILogger<ArticleAnalyser>>(),
                    sp.GetRequiredService<IDbContextFactory<TrumanDbContext>>(),
                    sp.GetRequiredService<Kernel>()
                )
            );
            services.AddSingleton<DbMigrator>();

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
                    pipeline.AddTimeout(TimeSpan.FromSeconds(30));
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

    using var exporter = SentryMetricsExporter.Start(host.Services);

    int? limit = null;
    var limitIndex = Array.IndexOf(args, "--limit");
    if (limitIndex >= 0 && limitIndex + 1 < args.Length && int.TryParse(args[limitIndex + 1], out var parsedLimit))
    {
        limit = parsedLimit;
    }

    var runMigrations = args.Contains("--run-migrations");
    var runFetch = args.Contains("--fetch");
    var runAnalyse = args.Contains("--analyse");

    if (runMigrations)
    {
        var migrator = host.Services.GetRequiredService<DbMigrator>();
        await migrator.RunAsync();
    }

    var shouldRunDefaultFlow = !runMigrations && !runFetch && !runAnalyse;
    var shouldCreateCheckIn = runFetch || runAnalyse || shouldRunDefaultFlow;

    SentryId? checkInId = null;
    if (shouldCreateCheckIn)
    {
        checkInId = SentrySdk.CaptureCheckIn("update-articles", CheckInStatus.InProgress);
    }

    try
    {
        if (runFetch)
        {
            var fetcher = host.Services.GetRequiredService<RssFetcher>();
            await fetcher.RunAsync();
        }

        if (runAnalyse)
        {
            var analyser = host.Services.GetRequiredService<ArticleAnalyser>();
            await analyser.RunAsync(limit);
        }

        if (shouldRunDefaultFlow)
        {
            // By default, we fetch and analyse
            var fetcher = host.Services.GetRequiredService<RssFetcher>();
            await fetcher.RunAsync();

            var analyser = host.Services.GetRequiredService<ArticleAnalyser>();
            await analyser.RunAsync(limit);
        }

        if (checkInId.HasValue)
        {
            SentrySdk.CaptureCheckIn("update-articles", CheckInStatus.Ok, checkInId.Value);
        }
    }
    catch
    {
        if (checkInId.HasValue)
        {
            SentrySdk.CaptureCheckIn("update-articles", CheckInStatus.Error, checkInId.Value);
        }
        throw;
    }
}
catch (Exception e)
{
    SentrySdk.CaptureException(e);
    // This is required to force an error code to be returned to k8s if an exception occurs... Otherwise, the CronJob
    // in k8s doesn't know the job has failed.
    Environment.Exit(1); 
}
#pragma warning restore SKEXP0070
