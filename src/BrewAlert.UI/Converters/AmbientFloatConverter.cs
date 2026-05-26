using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>Maps animation phase (0–7) to a gentle vertical offset for Pi-friendly motion.</summary>
public sealed class AmbientFloatConverter : IValueConverter
{
    public static readonly AmbientFloatConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int phase) return 0.0;
        var lane = int.TryParse(parameter?.ToString(), out var l) ? l : 0;
        var angle = (phase + lane * 2) * Math.PI / 4;
        return Math.Sin(angle) * 14;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
