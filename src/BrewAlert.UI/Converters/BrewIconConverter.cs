using Avalonia.Data.Converters;
using Avalonia.Media;
using BrewAlert.UI.Media;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>
/// Maps a stored profile icon (an emoji string) to a font-independent vector
/// <see cref="Geometry"/> for on-device display. Unknown emoji fall back to a generic cup,
/// so a profile with an unexpected icon still renders a shape rather than tofu on the Pi.
/// Empty/missing icons return <see langword="null"/> (the Path draws nothing).
/// </summary>
public sealed class BrewIconConverter : IValueConverter
{
    public static readonly BrewIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icon = value as string;
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        return icon switch
        {
            "☕" => BrewIconGeometry.Coffee,
            "♨" or "🍵" => BrewIconGeometry.Tea,
            "🫖" => BrewIconGeometry.Teapot,
            _ => BrewIconGeometry.Cup,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
