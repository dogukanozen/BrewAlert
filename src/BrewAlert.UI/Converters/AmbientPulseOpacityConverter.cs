using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

public sealed class AmbientPulseOpacityConverter : IValueConverter
{
    public static readonly AmbientPulseOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int phase) return 0.35;
        var angle = phase * Math.PI / 4;
        return 0.22 + 0.18 * (0.5 + 0.5 * Math.Sin(angle));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
