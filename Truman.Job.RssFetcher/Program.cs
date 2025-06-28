using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truman.Job.RssFetcher;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton<IRssFetcher, RssFetcher>();
    })
    .Build();

var service = host.Services.GetRequiredService<IRssFetcher>();
await service.RunAsync();