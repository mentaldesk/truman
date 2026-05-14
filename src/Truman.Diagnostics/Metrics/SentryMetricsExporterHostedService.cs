using Microsoft.Extensions.Hosting;

namespace Truman.Diagnostics.Metrics;

internal sealed class SentryMetricsExporterHostedService : IHostedService
{
    private readonly SentryMetricsExporter _exporter;

    public SentryMetricsExporterHostedService(IServiceProvider services)
    {
        _exporter = new SentryMetricsExporter(services);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _exporter.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _exporter.Stop();
        return Task.CompletedTask;
    }
}