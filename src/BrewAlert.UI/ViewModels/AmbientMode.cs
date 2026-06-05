namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Distinct ambient idle animations. One is picked at random each rotation period so the
/// screen changes its <em>motion</em>, not just its palette.
/// </summary>
public enum AmbientMode
{
    /// <summary>Info card with floating icons, breathing hero, drifting rings and steam.</summary>
    Card,

    /// <summary>Retro DVD-screensaver logo bouncing edge to edge.</summary>
    Dvd,

    /// <summary>Brew icons orbiting the clock.</summary>
    Orbit
}
