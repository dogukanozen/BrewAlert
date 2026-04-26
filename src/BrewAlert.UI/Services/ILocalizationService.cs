namespace BrewAlert.UI.Services;

/// <summary>Provides localized strings and language switching at runtime.</summary>
public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string Get(string key);
    void SetLanguage(string language);
    event Action<string>? LanguageChanged;
}
