using Avalonia;
using Avalonia.Media;
using BrewAlert.UI.Services;

namespace BrewAlert.UI.ViewModels;

/// <summary>Cached neutral brushes: palette rotation never allocates per-frame brushes.</summary>
public sealed class AmbientPalette
{
    public IBrush Background { get; }
    public IBrush GlowTop { get; }
    public IBrush GlowBottom { get; }
    public IBrush Halo { get; }
    private AmbientPalette(string background, string ink, byte intensity)
    {
        Background = new SolidColorBrush(Color.Parse(background));
        var color = Color.Parse(ink);
        GlowTop = Radial(color, intensity);
        GlowBottom = Radial(color, (byte)(intensity * 0.7));
        Halo = Radial(color, (byte)(intensity * 0.9));
    }
    private static IBrush Radial(Color ink, byte alpha) => new RadialGradientBrush
    {
        Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(alpha, ink.R, ink.G, ink.B), 0),
            new GradientStop(Color.FromArgb(0, ink.R, ink.G, ink.B), 1),
        },
    };
    public static IReadOnlyList<AmbientPalette> All { get; } = new[]
    {
        new AmbientPalette("#0d0e0f", "#dedfda", 24),
        new AmbientPalette("#111213", "#dedfda", 18),
        new AmbientPalette("#151617", "#dedfda", 14),
    };
    public static IReadOnlyList<AmbientPalette> Porcelain { get; } = new[]
    {
        new AmbientPalette("#eeede8", "#242624", 18),
        new AmbientPalette("#eae9e4", "#242624", 14),
        new AmbientPalette("#f1f0eb", "#242624", 12),
    };
    public static IReadOnlyList<AmbientPalette> ForAppearance(BrewTheme theme) =>
        theme == BrewTheme.Porcelain ? Porcelain : All;
}
