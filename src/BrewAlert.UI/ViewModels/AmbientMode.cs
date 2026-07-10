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
    Orbit,

    /// <summary>Layered luminous ribbons drifting behind the clock.</summary>
    Aurora,

    /// <summary>Coffee, tea, and steam glyphs falling at different depths.</summary>
    BrewRain,

    /// <summary>Counter-rotating neon geometry folding around the clock.</summary>
    Kaleidoscope,

    /// <summary>Bright brew-coloured streaks crossing the backdrop.</summary>
    Meteor,

    /// <summary>Deep liquid bands and bubbles rolling beneath the clock.</summary>
    Liquid
}
