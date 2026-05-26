namespace BrewAlert.UI.ViewModels;

/// <summary>One card in the ambient overlay rotation.</summary>
public sealed record AmbientSceneItem(
    string Icon,
    string Headline,
    string Subheadline,
    string Mood);
