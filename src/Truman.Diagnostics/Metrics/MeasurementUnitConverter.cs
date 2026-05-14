using Microsoft.Extensions.Logging;

namespace Truman.Diagnostics.Metrics;

/// <seealso href="https://ucum.org/ucum"/>
/// <seealso href="https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics"/>
/// <seealso href="https://develop.sentry.dev/sdk/foundations/state-management/scopes/attributes/#units"/>
public sealed class MeasurementUnitConverter
{
    private readonly HashSet<string> _unsupported = new(StringComparer.Ordinal);

    public MeasurementUnit Convert(string? unit, ILogger logger)
    {
        return unit switch
        {
            "s" => MeasurementUnit.Duration.Second,
            "ms" => MeasurementUnit.Duration.Millisecond,
            "By" => MeasurementUnit.Information.Byte,
            null => default,
            _ => Unknown(unit, logger),
        };
    }

    private MeasurementUnit Unknown(string unit, ILogger logger)
    {
        if (_unsupported.Add(unit))
        {
            logger.LogError("Instrument unit {Unit} not supported", unit);
        }

        return default;
    }
}