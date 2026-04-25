using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BrewAlert.Core;
using BrewAlert.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BrewAlert.Infrastructure.Persistence;

public sealed class JsonPreferencesService(ILogger<JsonPreferencesService> logger) : IPreferencesService
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path = BrewAlertConstants.PreferencesPath;

    public async Task<string?> GetNotificationProviderAsync()
    {
        var root = await ReadRootAsync();
        return root?["BrewAlert"]?["Notifications"]?["Provider"]?.GetValue<string>();
    }

    public async Task SaveNotificationProviderAsync(string provider)
    {
        await _lock.WaitAsync();
        try
        {
            var root = await ReadRootAsync() ?? new JsonObject();
            
            var brewAlert = (root["BrewAlert"] as JsonObject) ?? (root["BrewAlert"] = new JsonObject());
            var notifications = (brewAlert["Notifications"] as JsonObject) ?? (brewAlert["Notifications"] = new JsonObject());
            notifications["Provider"] = provider;

            var dir = Path.GetDirectoryName(_path)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_path, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<JsonObject?> ReadRootAsync()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse preferences file. Creating backup.");
            var backupPath = _path + ".bak";
            try { File.Copy(_path, backupPath, true); } catch { /* ignore */ }
            return null;
        }
    }
}
