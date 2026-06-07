using System;
using System.Globalization;
using BrewAlert.UI.Converters;
using Xunit;

namespace BrewAlert.UI.Tests;

public class AmbientDriftConverterTests
{
    private static object? Convert(object? value, string? parameter) =>
        AmbientDriftConverter.Instance.Convert(value, typeof(double), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_NonInteger_ReturnsZero()
    {
        Assert.Equal(0.0, Convert("not-an-int", "0:14"));
        Assert.Equal(0.0, Convert(null, "0:14"));
    }

    [Theory]
    [InlineData(0, "0:40", 0.0)]   // sin(0) * 40
    [InlineData(2, "0:40", 40.0)]  // sin(pi/2) * 40
    [InlineData(4, "0:40", 0.0)]   // sin(pi) * 40 ≈ 0
    [InlineData(6, "0:40", -40.0)] // sin(3pi/2) * 40
    public void Convert_AppliesSineCurveScaledByAmplitude(int phase, string parameter, double expected)
    {
        var result = Assert.IsType<double>(Convert(phase, parameter));
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void Convert_LaneShiftsThePhase()
    {
        // lane 1 advances the phase by 2 steps: phase 0 behaves like phase 2 (peak).
        var result = Assert.IsType<double>(Convert(0, "1:34"));
        Assert.Equal(34.0, result, precision: 6);
    }

    [Fact]
    public void Convert_NullParameter_UsesLaneZeroAmplitude14()
    {
        var result = Assert.IsType<double>(Convert(2, null));
        Assert.Equal(14.0, result, precision: 6);
    }

    [Fact]
    public void Convert_AmplitudeParsedWithInvariantCulture()
    {
        var result = Assert.IsType<double>(Convert(2, "0:1.5"));
        Assert.Equal(1.5, result, precision: 6);
    }

    [Fact]
    public void Convert_LaneOnlyParameter_MatchesLegacyFloatBehaviour()
    {
        // The retired AmbientFloatConverter used a fixed amplitude of 14, so a
        // "lane:14" parameter must reproduce its output exactly.
        for (var phase = 0; phase < 8; phase++)
        {
            var legacy = Math.Sin((phase + 3 * 2) * Math.PI / 4) * 14;
            var actual = Assert.IsType<double>(Convert(phase, "3:14"));
            Assert.Equal(legacy, actual, precision: 6);
        }
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            AmbientDriftConverter.Instance.ConvertBack(0.0, typeof(int), null, CultureInfo.InvariantCulture));
    }
}
