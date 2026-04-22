using BrewAlert.UI.Converters;
using System.Globalization;
using Xunit;

namespace BrewAlert.UI.Tests;

public class TimeSpanToStringConverterTests
{
    private readonly TimeSpanToStringConverter _converter = new();

    [Theory]
    [InlineData(0, 5, 23, "05:23")]
    [InlineData(0, 0, 5, "00:05")]
    [InlineData(1, 2, 3, "1:02:03")]
    [InlineData(10, 0, 0, "10:00:00")]
    public void Convert_ReturnsFormattedString(int hours, int minutes, int seconds, string expected)
    {
        // Arrange
        var ts = new TimeSpan(hours, minutes, seconds);

        // Act
        var result = _converter.Convert(ts, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ReturnsDefault_WhenValueIsNotTimeSpan()
    {
        // Act
        var result = _converter.Convert("not a timespan", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("00:00", result);
    }
}
