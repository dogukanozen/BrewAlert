using BrewAlert.Core.Models;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// One card in the ambient overlay rotation.
/// <para>
/// <see cref="Entry"/> is set for time-dependent freshness cards so the view model
/// can re-render the relative-time label and freshness mood against the current clock
/// on every apply — otherwise "3 min ago" would freeze at build time. It is
/// <see langword="null"/> for static cards (welcome, no-brews, today-count).
/// </para>
/// </summary>
public sealed record AmbientSceneItem(
    string Icon,
    string Headline,
    string Subheadline,
    string Mood,
    BrewHistoryEntry? Entry = null);
