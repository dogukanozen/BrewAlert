using System.Globalization;

namespace BrewAlert.UI.Services;

/// <summary>Provides localized strings and language switching at runtime.</summary>
public interface ILocalizationService
{
    string CurrentLanguage { get; }

    /// <summary>
    /// CultureInfo matching the active app language (not the OS culture). Use this for
    /// any culture-sensitive formatting — weekday names, month names, decimal separators —
    /// so the UI follows the user's BrewAlert language even when the OS is set to something else.
    /// </summary>
    CultureInfo CurrentCulture { get; }

    string Get(string key);
    void SetLanguage(string language);
    event Action<string>? LanguageChanged;
}
