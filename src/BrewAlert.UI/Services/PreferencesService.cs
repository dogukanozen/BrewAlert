namespace BrewAlert.UI.Services;

using System.Text.Json;
using System.Text.Json.Nodes;
using BrewAlert.UI.Constants;

/// <summary>Update only the chosen setting, preserving other sections and future fields.</summary>
public sealed class PreferencesService : IPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
    private readonly string _filePath;

    public PreferencesService(string? filePath = null) => _filePath = filePath ?? BrewAlertPaths.Preferences;
    public Task SaveNotificationProviderAsync(string provider, CancellationToken ct = default) =>
        UpdateAsync(root => Section(root, "Notifications")["Provider"] = provider, ct);
    public Task SaveLanguageAsync(string language, CancellationToken ct = default) =>
        UpdateAsync(root => root["Language"] = language, ct);
    public Task SaveWebhookUrlAsync(string url, CancellationToken ct = default) =>
        UpdateAsync(root => Section(Section(root, "Notifications"), "Teams")["WebhookUrl"] = url?.Trim() ?? "", ct);
    public Task SaveAppearanceAsync(AppearanceOptions appearance, CancellationToken ct = default) =>
        UpdateAsync(root =>
        {
            var section = Section(root, "Appearance");
            var values = JsonSerializer.SerializeToNode(appearance.Normalized())!.AsObject();
            foreach (var pair in values) section[pair.Key] = pair.Value?.DeepClone();
        }, ct);

    private async Task UpdateAsync(Action<JsonObject> update, CancellationToken ct)
    {
        await _fileSemaphore.WaitAsync(ct);
        try
        {
            var root = await ReadRootAsync(ct);
            update(Section(root, "BrewAlert"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            var tempPath = _filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, root.ToJsonString(JsonOptions), ct);
                File.Move(tempPath, _filePath, true);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch { /* A cleanup failure must not hide the original write error. */ }
            }
        }
        finally { _fileSemaphore.Release(); }
    }

    private async Task<JsonObject> ReadRootAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new JsonObject();
        try
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(_filePath, ct)) as JsonObject
                ?? throw new JsonException("Preferences must be a JSON object.");
            ValidateObject(root, "BrewAlert");
            if (root["BrewAlert"] is JsonObject app)
            {
                ValidateObject(app, "Appearance");
                ValidateObject(app, "Notifications");
                if (app["Notifications"] is JsonObject notifications) ValidateObject(notifications, "Teams");
            }
            return root;
        }
        catch (JsonException)
        {
            try { File.Copy(_filePath, _filePath + ".bak", true); }
            catch { /* Keep recovery available even if a backup cannot be written. */ }
            return new JsonObject();
        }
    }
    private static void ValidateObject(JsonObject parent, string key)
    {
        if (parent[key] is not null and not JsonObject) throw new JsonException($"{key} must be an object.");
    }
    private static JsonObject Section(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject child) return child;
        var section = new JsonObject();
        parent[key] = section;
        return section;
    }
}
