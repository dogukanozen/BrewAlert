namespace BrewAlert.UI.Services;

/// <summary>Persists user preferences (notification provider, language, webhook URL) to disk.</summary>
public interface IPreferencesService
{
    Task SaveNotificationProviderAsync(string provider, CancellationToken ct = default);
    Task SaveLanguageAsync(string language, CancellationToken ct = default);
    Task SaveWebhookUrlAsync(string url, CancellationToken ct = default);
}
