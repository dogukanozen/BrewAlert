using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>
/// Converts a <see cref="TimeSpan"/> to a human-readable countdown string (e.g., "05:23").
/// </summary>
public sealed class TimeSpanToStringConverter : IValueConverter
{
    public static readonly TimeSpanToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            return ts.TotalHours >= 1
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"mm\:ss");
        }
        return "00:00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
