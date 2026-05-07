namespace BrewAlert.UI.Services;

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrewAlert.UI.Constants;

public sealed class PreferencesService : IPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
    private readonly string _filePath;

    public PreferencesService(string? filePath = null)
    {
        _filePath = filePath ?? BrewAlertPaths.Preferences;
    }

    public async Task SaveNotificationProviderAsync(string provider, CancellationToken ct = default)
    {
        await _fileSemaphore.WaitAsync(ct);
        try
        {
            var root = await ReadRootAsync(ct);
            root.BrewAlert.Notifications.Provider = provider;
            await WriteRootAsync(root, ct);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task SaveLanguageAsync(string language, CancellationToken ct = default)
    {
        await _fileSemaphore.WaitAsync(ct);
        try
        {
            var root = await ReadRootAsync(ct);
            root.BrewAlert.Language = language;
            await WriteRootAsync(root, ct);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task SaveWebhookUrlAsync(string url, CancellationToken ct = default)
    {
        await _fileSemaphore.WaitAsync(ct);
        try
        {
            var root = await ReadRootAsync(ct);
            root.BrewAlert.Notifications.Teams.WebhookUrl = url?.Trim() ?? string.Empty;
            await WriteRootAsync(root, ct);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    private async Task<PreferencesRoot> ReadRootAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return new PreferencesRoot();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<PreferencesRoot>(json, JsonOptions) ?? new PreferencesRoot();
        }
        catch (JsonException)
        {
            return new PreferencesRoot();
        }
    }

    private async Task WriteRootAsync(PreferencesRoot root, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(root, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    private sealed class PreferencesRoot
    {
        [JsonPropertyName("BrewAlert")]
        public BrewAlertSection BrewAlert { get; set; } = new();
    }

    private sealed class BrewAlertSection
    {
        [JsonPropertyName("Language")]
        public string Language { get; set; } = AppLanguage.English;

        [JsonPropertyName("Notifications")]
        public NotificationsSection Notifications { get; set; } = new();
    }

    private sealed class NotificationsSection
    {
        [JsonPropertyName("Provider")]
        public string Provider { get; set; } = "Console";

        [JsonPropertyName("Teams")]
        public TeamsSection Teams { get; set; } = new();
    }

    private sealed class TeamsSection
    {
        [JsonPropertyName("WebhookUrl")]
        public string WebhookUrl { get; set; } = string.Empty;
    }
}
