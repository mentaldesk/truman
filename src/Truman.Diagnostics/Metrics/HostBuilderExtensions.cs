using Microsoft.Extensions.DependencyInjection;
using Truman.Diagnostics.Metrics;

namespace Microsoft.Extensions.Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder AddSentryMetricsExporter(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddHostedService<SentryMetricsExporterHostedService>();
        });

        return hostBuilder;
    }
}