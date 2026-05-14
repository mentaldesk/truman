using Microsoft.Extensions.Logging.Testing;
using Truman.Diagnostics.Metrics;

namespace Truman.Diagnostics.Tests.Metrics;

public class MeasurementUnitConverterTests : IDisposable
{
    private readonly FakeLogger<MeasurementUnitConverterTests> _logger;

    public MeasurementUnitConverterTests()
    {
        _logger = new FakeLogger<MeasurementUnitConverterTests>();
    }

    public void Dispose()
    {
        Assert.True(_logger.Collector.Count == 0, $"Snapshot of {nameof(FakeLogCollector)} not asserted.");
    }

    [Fact]
    public void Convert_NullUnit_ToDefaultMeasurementUnit()
    {
        var converter = new MeasurementUnitConverter();

        var actual = converter.Convert(null, _logger);
        MeasurementUnit expected = default;

        Assert.Equal(expected, actual);
        Assert.Empty(_logger.Collector.GetSnapshot(true));
    }

    [Theory]
    [InlineData("s", MeasurementUnit.Duration.Second)]
    [InlineData("ms", MeasurementUnit.Duration.Millisecond)]
    [InlineData("By", MeasurementUnit.Information.Byte)]
    public void Convert_NonNullUnit_ToNonDefaultMeasurementUnit(string unit, MeasurementUnit expected)
    {
        var converter = new MeasurementUnitConverter();

        var actual = converter.Convert(unit, _logger);

        Assert.Equal(expected, actual);
        Assert.Empty(_logger.Collector.GetSnapshot(true));
    }

    [Fact]
    public void Convert_UnknownUnit_LogError()
    {
        var converter = new MeasurementUnitConverter();

        var actual = converter.Convert("Unknown", _logger);
        MeasurementUnit expected = default;

        Assert.Equal(expected, actual);
        Assert.Collection(_logger.Collector.GetSnapshot(true),
            element => Assert.Equal("Instrument unit Unknown not supported", element.Message));
    }

    [Fact]
    public void Convert_UnknownUnit_LogErrorOncePerUnit()
    {
        var converter = new MeasurementUnitConverter();

        _ = converter.Convert("Unknown", _logger);
        _ = converter.Convert("N/A", _logger);
        _ = converter.Convert("Unknown", _logger);

        Assert.Collection(_logger.Collector.GetSnapshot(true),
            element => Assert.Equal("Instrument unit Unknown not supported", element.Message),
                        element => Assert.Equal("Instrument unit N/A not supported", element.Message));
    }
}
