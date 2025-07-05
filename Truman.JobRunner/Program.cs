using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Truman.Data;
using Truman.JobRunner;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#endif

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient();
        
        // Add database context factory instead of scoped DbContext
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            
        // Debug: Print current environment
        Console.WriteLine($"Current environment: {context.HostingEnvironment.EnvironmentName}");
            
        services.AddDbContextFactory<TrumanDbContext>(options =>
            options.UseNpgsql(connectionString));
            
        // Register as Singleton since this is a console app with a single operation
        services.AddSingleton<RssFetcher>();
        services.AddSingleton<ArticleAnalyser>();

        // Register Semantic Kernel
        var aiModel = context.Configuration["AI:Model"] 
            ?? throw new InvalidOperationException("AI Model not found in configuration.");
        var apiKey = context.Configuration["AI:ApiKey"] 
            ?? throw new InvalidOperationException("AI API key not found in configuration.");
#pragma warning disable SKEXP0070
        services.AddTransient<Kernel>(sp => Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(aiModel, apiKey)
            .Build()
        );
#pragma warning restore SKEXP0070
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
    Console.WriteLine("Usage: dotnet run -- --fetch | --analyse");
    Environment.Exit(1);
}