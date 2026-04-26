namespace BrewAlert.UI.Services;

using System.IO;
using System.Text.Json;
using BrewAlert.UI.Constants;

/// <summary>
/// Reads and writes user preferences to %AppData%\BrewAlert\preferences.json.
/// Uses a read-merge-write pattern protected by a SemaphoreSlim so concurrent
/// async callers never interleave their read-modify-write cycles.
/// </summary>
public sealed class PreferencesService : IPreferencesService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

    public async Task SaveNotificationProviderAsync(string provider, CancellationToken ct = default)
    {
        await _fileSemaphore.WaitAsync(ct);
        try
        {
            var prefs = await ReadAsync(ct);
            prefs.NotificationProvider = provider;
            await WriteAsync(prefs, ct);
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
            var prefs = await ReadAsync(ct);
            prefs.Language = language;
            await WriteAsync(prefs, ct);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    private static async Task<PreferencesData> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(BrewAlertPaths.Preferences))
            return new PreferencesData();

        try
        {
            var json = await File.ReadAllTextAsync(BrewAlertPaths.Preferences, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = new PreferencesData();

            if (root.TryGetProperty("BrewAlert", out var ba))
            {
                if (ba.TryGetProperty("Language", out var lang))
                    data.Language = lang.GetString() ?? AppLanguage.English;
                if (ba.TryGetProperty("Notifications", out var notif) &&
                    notif.TryGetProperty("Provider", out var prov))
                    data.NotificationProvider = prov.GetString() ?? "Console";
            }

            return data;
        }
        catch (JsonException)
        {
            // Corrupted JSON — reset to defaults so the next write repairs the file.
            return new PreferencesData();
        }
        // FileNotFoundException, UnauthorizedAccessException, etc. propagate so the
        // caller can surface a meaningful error rather than silently losing data.
    }

    private static async Task WriteAsync(PreferencesData prefs, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(BrewAlertPaths.Preferences)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(new
        {
            BrewAlert = new
            {
                Language = prefs.Language,
                Notifications = new { Provider = prefs.NotificationProvider }
            }
        }, WriteOptions);

        await File.WriteAllTextAsync(BrewAlertPaths.Preferences, json, ct);
    }

    private sealed class PreferencesData
    {
        public string Language { get; set; } = AppLanguage.English;
        public string NotificationProvider { get; set; } = "Console";
    }
}
