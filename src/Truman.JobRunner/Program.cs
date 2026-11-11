using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Anthropic;
using Microsoft.Extensions.AI;
using System.Net.Http.Headers;
using Truman.JobRunner.Jev;
using Polly;
using Truman.Data;
using Truman.JobRunner;
using DotNetEnv;
using DotNetEnv.Configuration;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#endif

// AddSentry and SentryAIOptions.Experimental are flagged experimental by the Sentry SDK.
#pragma warning disable SENTRY0001
try
{
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
        // After ConfigureLogging: a later ClearProviders() removes Sentry's logger providers. https://github.com/getsentry/sentry-dotnet/issues/5755
        .UseSentry(options =>
        {
#if DEBUG
            options.Debug = true;
#endif
            // 1.0 unlike the API: a handful of runs a day, so a sampled-out run is one we cannot investigate.
            options.TracesSampleRate = 1.0;
            options.CaptureFailedRequests = true;
            options.SendDefaultPii = true;
            options.StackTraceMode = StackTraceMode.Enhanced;
            options.EnableLogs = true;
        })
        .AddSentryMetricsExporter()
        .ConfigureServices((context, services) =>
        {
            // Add database context factory instead of scoped DbContext
            var connectionString = context.Configuration.GetPostgresConnectionString();
            var model = context.Configuration["AI:Model"]
                        ?? throw new InvalidOperationException("AI Model not found in configuration.");
            
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
                    sp.GetRequiredService<IChatClient>(),
                    sp.GetRequiredService<JevArticleScorer>(),
                    model
                )
            );
            services.AddSingleton<ArticleUpdater>();
            services.AddSingleton<DbMigrator>();

            services
                .AddHttpClient("RssClient")
                .ConfigureHttpClient(c =>
                {
                    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TrumanBot/1.0)");
                    c.DefaultRequestHeaders.Accept.ParseAdd(
                        "application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.8");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All
                });

            // The Anthropic SDK retries 429s and 5xxs itself, so there is no resilience pipeline here.
            var apiKey = context.Configuration["AI:ApiKey"]
                         ?? throw new InvalidOperationException("AI API key not found in configuration.");

            services.AddSingleton(_ => new AnthropicClient { ApiKey = apiKey });

            services.AddSingleton<IChatClient>(sp => sp
                .GetRequiredService<AnthropicClient>()
                .AsIChatClient(defaultModelId: model)
                .WithSentry());

            var jevApiKey = context.Configuration["Jev:ApiKey"]
                            ?? throw new InvalidOperationException("Jev API key not found in configuration.");

            services.AddHttpClient(JevClient.HttpClientName, httpClient =>
            {
                httpClient.BaseAddress = new Uri("https://api.typesafe.ai/");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jevApiKey);
            });

            services.AddSingleton(sp => new JevClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(JevClient.HttpClientName)));
            services.AddSingleton<JevArticleScorer>();
        })
        .Build();

    // Runs SentryHostedService, which initialises the SDK and closes it on shutdown.
    await host.StartAsync();

    var arguments = JobRunnerArguments.Parse(args);

    if (arguments.RunMigrations)
    {
        var migrator = host.Services.GetRequiredService<DbMigrator>();
        await migrator.RunAsync();
    }

    var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

    if (arguments.IsScheduled)
    {
        var updater = host.Services.GetRequiredService<ArticleUpdater>();

        await updater.RunScheduledAsync(stopping);
    }
    else if (arguments.Fetch || arguments.Analyse)
    {
        var updater = host.Services.GetRequiredService<ArticleUpdater>();
        await updater.RunManualAsync(arguments.Fetch, arguments.Analyse, arguments.Limit, stopping);
    }

    await host.StopAsync();
}
catch (Exception e)
{
    SentrySdk.CaptureException(e);
    // This is required to force an error code to be returned to k8s if an exception occurs... Otherwise, the CronJob
    // in k8s doesn't know the job has failed.
    Environment.Exit(1); 
}
#pragma warning restore SENTRY0001
