using Microsoft.Extensions.Hosting;

namespace Truman.Diagnostics.Metrics;

internal sealed class SentryMetricsExporterHostedService : BackgroundService
{
    private readonly SentryMetricsExporter _exporter;

    public SentryMetricsExporterHostedService(IServiceProvider services)
    {
        _exporter = new SentryMetricsExporter(services);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _exporter.StartAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _exporter.Stop();
    }
}