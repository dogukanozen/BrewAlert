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
    Liquid,

    /// <summary>Radial light streaks accelerating through a star tunnel.</summary>
    Hyperspace,

    /// <summary>Expanding scan rings and rotating targeting sweeps.</summary>
    Radar,

    /// <summary>Warm luminous particles wandering across the backdrop.</summary>
    Fireflies,

    /// <summary>Counter-rotating triangular prisms with neon echoes.</summary>
    Prism,

    /// <summary>Six fine, continuous silk-like lines moving beneath the clock.</summary>
    Silk,

    /// <summary>Layered woven lines, independent of the selected colour theme.</summary>
    SilkWeave,

    /// <summary>Three dots slowly orbiting concentric ellipses.</summary>
    OrbitalDots,

    /// <summary>A moon above gently drifting rows of semicircular waves.</summary>
    MoonWaves
}
