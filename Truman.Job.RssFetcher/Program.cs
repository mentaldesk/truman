using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truman.Data;
using Truman.Job.RssFetcher;

#if DEBUG
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#endif

var host = Host.CreateDefaultBuilder(args)
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
        services.AddSingleton<IRssFetcher, RssFetcher>();
    })
    .Build();

var service = host.Services.GetRequiredService<IRssFetcher>();
await service.RunAsync();