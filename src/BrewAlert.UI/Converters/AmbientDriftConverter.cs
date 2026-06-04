using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>
/// Maps the animation phase (0–7) to a sine offset for slow, Pi-friendly drifting of
/// background blobs and floating glyphs. Parameter is <c>"lane:amplitude"</c> — the lane
/// phase-shifts each element so they don't all bob in unison, the amplitude (px) controls
/// travel. Binding both <c>X</c> and <c>Y</c> with different lanes yields an orbit-like path.
/// </summary>
public sealed class AmbientDriftConverter : IValueConverter
{
    public static readonly AmbientDriftConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int phase) return 0.0;
        var (lane, amplitude) = Parse(parameter);
        var angle = (phase + lane * 2) * Math.PI / 4;
        return Math.Sin(angle) * amplitude;
    }

    private static (int lane, double amplitude) Parse(object? parameter)
    {
        var text = parameter?.ToString();
        if (string.IsNullOrEmpty(text)) return (0, 14);
        var parts = text.Split(':');
        var lane = parts.Length > 0 && int.TryParse(parts[0], out var l) ? l : 0;
        var amplitude = parts.Length > 1
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            ? a : 14;
        return (lane, amplitude);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
