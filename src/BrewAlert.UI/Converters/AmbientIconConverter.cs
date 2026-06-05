using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrewAlert.UI.Converters;

/// <summary>
/// Maps a semantic icon key ("coffee", "tea", "teapot", "steam", "sparkle", "droplet",
/// "cup") to a vector <see cref="Geometry"/> drawn as a stroked <c>Path</c>.
/// <para>
/// The ambient overlay used to render brew icons as emoji (☕ 🍵 🫖 …). Raspberry Pi OS
/// ships no emoji font, so every glyph fell back to the "missing glyph" box (the three-bar
/// tofu). These line-art geometries carry no font dependency, so they render identically on
/// Windows and the Pi. Parsed geometries are cached and shared across every Path — Avalonia
/// geometries are immutable once parsed, so sharing one instance is safe.
/// </para>
/// </summary>
public sealed class AmbientIconConverter : IValueConverter
{
    public static readonly AmbientIconConverter Instance = new();

    // Line-art icons authored in a 0..24 viewport. Rendered stroked (Fill="None"); host the
    // Path in a Viewbox so the stroke scales uniformly with the requested display size.
    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        // Mug + handle + three rising steam strokes.
        ["coffee"] = "M5,9 H17 V16 A3,3 0 0 1 14,19 H8 A3,3 0 0 1 5,16 Z " +
                     "M17,10 H19 A3,3 0 0 1 19,16 H17 M8,2 V5 M11,2 V5 M14,2 V5",
        // Cup with saucer + handle + two steam curls.
        ["tea"] = "M5,9 H16 V14 A5.5,5.5 0 0 1 5,14 Z M16,10 H18 A2.5,2.5 0 0 1 18,15 H16 " +
                  "M3,20 H18 M9,2 C9,4 11,4 11,6 M13,3 C13,4.5 14.5,4.5 14.5,6",
        // Dome-bodied pot with spout, handle and lid knob.
        ["teapot"] = "M5,13 A7,5 0 0 1 19,13 V14 A3,3 0 0 1 16,17 H8 A3,3 0 0 1 5,14 Z " +
                     "M19,11 H22 A1.5,1.5 0 0 1 22,14 M5,13 A2,2 0 0 1 5,9 M10,8 H14 M12,8 V6",
        // Three wind/steam strokes.
        ["steam"] = "M3,8 H13 A3,3 0 1 0 10,5 M3,13 H17 A3,3 0 1 1 14,16 M3,18 H11",
        // Rounded four-point sparkle.
        ["sparkle"] = "M12,3 C12.5,8 13,9 18,12 C13,15 12.5,16 12,21 C11.5,16 11,15 6,12 C11,9 11.5,8 12,3 Z",
        // Teardrop.
        ["droplet"] = "M12,3 C12,3 6,11 6,15 A6,6 0 0 0 18,15 C18,11 12,3 12,3 Z",
        // Generic cup (custom brews) — reuse the mug silhouette without steam.
        ["cup"] = "M5,9 H17 V16 A3,3 0 0 1 14,19 H8 A3,3 0 0 1 5,16 Z M17,10 H19 A3,3 0 0 1 19,16 H17",
    };

    private static readonly Dictionary<string, Geometry> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key) || !Paths.TryGetValue(key, out var data))
            return null;

        if (!Cache.TryGetValue(key, out var geometry))
        {
            geometry = Geometry.Parse(data);
            Cache[key] = geometry;
        }
        return geometry;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
