using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// One background colour theme for the ambient overlay. Brushes are built once and
/// cached so the view model can hand the <em>same</em> instance back on every scene
/// apply: that lets <c>ObservableProperty</c> dedup the 500 ms animation-tick repaints
/// (no churn) while the view's <c>BrushTransition</c> only crossfades when the palette
/// genuinely changes — i.e. when the scene rotates. Pi-friendly: brushes are static, the
/// view animates with transforms/opacity only.
/// </summary>
public sealed class AmbientPalette
{
    public IBrush Background { get; }
    public IBrush GlowTop { get; }
    public IBrush GlowBottom { get; }
    public IBrush Halo { get; }

    private AmbientPalette(
        string top, string mid, string bottom,
        string glowTop, string glowBottom, string halo)
    {
        Background = VerticalGradient(Color.Parse(top), Color.Parse(mid), Color.Parse(bottom));
        GlowTop = Radial(Color.Parse(glowTop));
        GlowBottom = Radial(Color.Parse(glowBottom));
        Halo = Radial(Color.Parse(halo), midStop: true);
    }

    private static IBrush VerticalGradient(Color top, Color mid, Color bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(top, 0),
            new GradientStop(mid, 0.55),
            new GradientStop(bottom, 1),
        },
    };

    private static IBrush Radial(Color core, bool midStop = false)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
        };
        brush.GradientStops.Add(new GradientStop(core, 0));
        if (midStop)
            brush.GradientStops.Add(new GradientStop(Fade(core, 0x33), 0.55));
        brush.GradientStops.Add(new GradientStop(Fade(core, 0x00), 1));
        return brush;
    }

    private static Color Fade(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>
    /// Rotation of dark, kiosk-safe ambient themes. The overlay walks this list one
    /// palette per scene so the background never sits on the same colours for long.
    /// </summary>
    public static IReadOnlyList<AmbientPalette> All { get; } = new[]
    {
        // Indigo amber (the original calm coffee mood)
        new AmbientPalette("#1d1d33", "#15152a", "#0b0b1a", "#66e07c24", "#55f0a500", "#80f0a500"),
        // Twilight rose
        new AmbientPalette("#2a1726", "#1d1020", "#110813", "#66ff6b9d", "#55ff9e7a", "#80ff6b9d"),
        // Deep forest tea
        new AmbientPalette("#122a22", "#0d1f1a", "#07120f", "#663ddc84", "#5500c2a8", "#803ddc84"),
        // Ocean night
        new AmbientPalette("#102338", "#0b1828", "#060f1a", "#664aa8ff", "#5522d3ee", "#804aa8ff"),
        // Plum ember
        new AmbientPalette("#271433", "#1b0e26", "#100716", "#66b06bff", "#55ff8a5b", "#80b06bff"),
        // Sunset clay
        new AmbientPalette("#331b14", "#26130e", "#160a07", "#66ff8a3d", "#55ffd166", "#80ff8a3d"),
    };
}
