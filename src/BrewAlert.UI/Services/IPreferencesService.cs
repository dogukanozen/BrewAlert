namespace BrewAlert.UI.Services;

/// <summary>Persists user preferences (notification provider, language) to disk.</summary>
public interface IPreferencesService
{
    Task SaveNotificationProviderAsync(string provider, CancellationToken ct = default);
    Task SaveLanguageAsync(string language, CancellationToken ct = default);
}
