using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>
/// Maps the animation phase (0–7) to a gentle scale factor (~0.96–1.04) so the hero icon
/// "breathes". Applied as a <c>ScaleTransform</c> (render-only) so the centred clock/text
/// layout never shifts.
/// </summary>
public sealed class AmbientBreatheConverter : IValueConverter
{
    public static readonly AmbientBreatheConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int phase) return 1.0;
        var angle = phase * Math.PI / 4;
        return 1.0 + 0.045 * Math.Sin(angle);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
