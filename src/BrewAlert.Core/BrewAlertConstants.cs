namespace BrewAlert.Core;

using System;
using System.IO;

public static class BrewAlertConstants
{
    public static string PreferencesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BrewAlert",
        "preferences.json");
}
