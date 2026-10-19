using Avalonia.Media;

namespace BrewAlert.UI.Media;

/// <summary>
/// Font-independent vector geometries for the brew icons shown in the app UI.
/// <para>
/// Profile icons are stored as emoji (☕ ♨ 🫖 …) and render fine in Teams and the console,
/// but Raspberry Pi OS ships no emoji font, so on the device screen they fell back to the
/// missing-glyph box (the three-bar tofu). These line-art paths carry no font dependency and
/// render identically everywhere. Parsed once and shared — Avalonia geometries are immutable
/// after parsing, so a single instance can back many controls.
/// </para>
/// </summary>
public static class BrewIconGeometry
{
    public static Geometry Arrow { get; } = Geometry.Parse("M5,19 L19,5 M5,5 H19 V19");
    public static Geometry Plus { get; } = Geometry.Parse("M12,4 V20 M4,12 H20");
    public static Geometry Pause { get; } = Geometry.Parse("M8,5 V19 M16,5 V19");
    public static Geometry Play { get; } = Geometry.Parse("M7,4 L20,12 L7,20 Z");
    public static Geometry Moon { get; } = Geometry.Parse("M20,15 A9,9 0 1 1 9,3 A7,7 0 0 0 20,15 Z");
    // Authored in a 0..24 viewport, drawn stroked (Fill="None"); host in a Viewbox so the
    // stroke scales uniformly to the requested display size.

    /// <summary>Mug with handle and three rising steam strokes.</summary>
    public static Geometry Coffee { get; } = Geometry.Parse(
        "M5,9 H17 V16 A3,3 0 0 1 14,19 H8 A3,3 0 0 1 5,16 Z " +
        "M17,10 H19 A3,3 0 0 1 19,16 H17 M8,2 V5 M11,2 V5 M14,2 V5");

    /// <summary>Cup with saucer, handle and two steam curls.</summary>
    public static Geometry Tea { get; } = Geometry.Parse(
        "M5,9 H16 V14 A5.5,5.5 0 0 1 5,14 Z M16,10 H18 A2.5,2.5 0 0 1 18,15 H16 " +
        "M3,20 H18 M9,2 C9,4 11,4 11,6 M13,3 C13,4.5 14.5,4.5 14.5,6");

    /// <summary>Dome-bodied pot with spout, handle and lid knob.</summary>
    public static Geometry Teapot { get; } = Geometry.Parse(
        "M5,13 A7,5 0 0 1 19,13 V14 A3,3 0 0 1 16,17 H8 A3,3 0 0 1 5,14 Z " +
        "M19,11 H22 A1.5,1.5 0 0 1 22,14 M5,13 A2,2 0 0 1 5,9 M10,8 H14 M12,8 V6");

    /// <summary>Generic cup (unknown/custom icons) — the mug silhouette without steam.</summary>
    public static Geometry Cup { get; } = Geometry.Parse(
        "M5,9 H17 V16 A3,3 0 0 1 14,19 H8 A3,3 0 0 1 5,16 Z M17,10 H19 A3,3 0 0 1 19,16 H17");

    /// <summary>Settings cog: hub circle, eight teeth and a centre hole.</summary>
    public static Geometry Gear { get; } = Geometry.Parse(
        "M7,12 A5,5 0 1 0 17,12 A5,5 0 1 0 7,12 Z " +
        "M17,12 H20 M15.54,8.46 L17.66,6.34 M12,7 V4 M8.46,8.46 L6.34,6.34 " +
        "M7,12 H4 M8.46,15.54 L6.34,17.66 M12,17 V20 M15.54,15.54 L17.66,17.66 " +
        "M10,12 A2,2 0 1 0 14,12 A2,2 0 1 0 10,12 Z");
}
