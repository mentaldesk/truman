using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Truman.Diagnostics.Metrics;

public sealed class SentryMetricsExporter : IDisposable
{
    public static IDisposable Start(IServiceProvider services)
    {
        var exporter = new SentryMetricsExporter(services);
        exporter.Start();
        return exporter;
    }

    private readonly MeterListener _listener;
    private readonly ILogger<SentryMetricsExporter> _logger;

    internal SentryMetricsExporter(IServiceProvider services)
    {
        _listener = new MeterListener();
        _logger = services.GetRequiredService<ILogger<SentryMetricsExporter>>();
    }

    internal void Start()
    {
        _listener.InstrumentPublished = static (instrument, listener) =>
        {
            listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<byte>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<short>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<float>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<decimal>(OnUnsupportedMeasurementRecorded);

        _listener.Start();
    }
    
    private void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) where T : struct
    {
        TagList attributes = [];

        if (instrument.Meter.Tags is not null)
        {
            foreach (var tag in instrument.Meter.Tags)
            {
                if (tag.Value is not null)
                {
                    attributes.Add(tag);
                }
            }
        }

        if (instrument.Tags is not null)
        {
            foreach (var tag in instrument.Tags)
            {
                if (tag.Value is not null)
                {
                    attributes.Add(tag);
                }
            }
        }

        if (!tags.IsEmpty)
        {
            foreach (var tag in tags)
            {
                if (tag.Value is not null)
                {
                    attributes.Add(tag);
                }
            }
        }

        if (instrument is Counter<T> or UpDownCounter<T>)
        {
            SentrySdk.Metrics.EmitCounter(instrument.Name, measurement, attributes);
        }
        else if (instrument is Gauge<T>)
        {
            var unit = MeasurementUnitFactory.From(instrument.Unit, _logger);
            SentrySdk.Metrics.EmitGauge(instrument.Name, measurement, unit, attributes);
        }
        else if (instrument is Histogram<T>)
        {
            var unit = MeasurementUnitFactory.From(instrument.Unit, _logger);
            SentrySdk.Metrics.EmitDistribution(instrument.Name, measurement, unit, attributes);
        }
        else
        {
            _logger.LogError("Instrument type {Instrument} not supported", instrument.GetType());
        }
    }

    private void OnUnsupportedMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) where T : struct
    {
        _logger.LogError("Measurement type {Measurement} not supported", typeof(T));
    }

    internal void Stop()
    {
        Dispose();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}

file static class MeasurementUnitFactory
{
    /// <seealso href="https://ucum.org/ucum"/>
    /// <seealso href="https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics"/>
    /// <seealso href="https://develop.sentry.dev/sdk/foundations/state-management/scopes/attributes/#units"/>
    internal static MeasurementUnit From(string? unit, ILogger logger)
    {
        return unit switch
        {
            "s" => MeasurementUnit.Duration.Second,
            "By" => MeasurementUnit.Information.Byte,
            null => default,
            _ => Default(unit, logger),
        };

        static MeasurementUnit Default(string unit, ILogger logger)
        {
            logger.LogError("Instrument unit {Unit} not supported", unit);
            return default;
        }
    }
}