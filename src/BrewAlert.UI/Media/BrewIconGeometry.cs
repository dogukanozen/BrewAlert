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

    /// <summary>Contactless RFID badge with signal arcs.</summary>
    public static Geometry RfidCard { get; } = Geometry.Parse(
        "M3,6 H19 A2,2 0 0 1 21,8 V16 A2,2 0 0 1 19,18 H3 A2,2 0 0 1 1,16 V8 A2,2 0 0 1 3,6 Z " +
        "M5,11 H9 M5,14 H11 " +
        "M13,10 A3,3 0 0 1 13,14 M15.5,8.5 A5.5,5.5 0 0 1 15.5,15.5");

    /// <summary>Upward arrow outline for virtual keyboard Shift.</summary>
    public static Geometry Shift { get; } = Geometry.Parse(
        "M12,4 L4,12 H9 V20 H15 V12 H20 Z");

    /// <summary>Left-pointing delete key with cross for virtual keyboard Backspace.</summary>
    public static Geometry Backspace { get; } = Geometry.Parse(
        "M9,6 H20 A1,1 0 0 1 21,7 V17 A1,1 0 0 1 20,18 H9 L3,12 Z " +
        "M12,9 L17,15 M17,9 L12,15");

    /// <summary>Person silhouette for brewer attribution.</summary>
    public static Geometry User { get; } = Geometry.Parse(
        "M12,5 A3.5,3.5 0 1 0 12,12 A3.5,3.5 0 1 0 12,5 Z " +
        "M5,20 C5,16.5 8.1,14.5 12,14.5 C15.9,14.5 19,16.5 19,20");
}
