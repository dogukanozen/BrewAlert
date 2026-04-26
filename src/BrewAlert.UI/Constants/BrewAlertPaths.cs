namespace BrewAlert.UI.Constants;

/// <summary>
/// Central definition of all file paths used by BrewAlert.
/// Both App.axaml.cs and SettingsViewModel read preferences from the same location.
/// </summary>
public static class BrewAlertPaths
{
    private static readonly string AppDataDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BrewAlert");

    /// <summary>User preferences file (provider, language). Written by Settings screen.</summary>
    public static string Preferences { get; } = System.IO.Path.Combine(AppDataDir, "preferences.json");
}
