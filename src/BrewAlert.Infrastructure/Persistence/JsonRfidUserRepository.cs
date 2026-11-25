namespace BrewAlert.Infrastructure.Persistence;

using System.Text.Json;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Persists registered RFID users to a local JSON file with atomic writes.
/// Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class JsonRfidUserRepository : IRfidUserRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<JsonRfidUserRepository> _logger;

    public JsonRfidUserRepository(ILogger<JsonRfidUserRepository> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public async Task<IReadOnlyList<RfidUser>> GetAllAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await ReadFileAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<RfidUser?> GetByCardIdAsync(string cardId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return null;
        var trimmed = cardId.Trim();
        var users = await GetAllAsync(ct);
        return users.FirstOrDefault(u =>
            !string.IsNullOrWhiteSpace(u.CardId) &&
            string.Equals(u.CardId.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(RfidUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _semaphore.WaitAsync(ct);
        try
        {
            var list = await ReadFileAsync(ct);
            var normalizedCard = string.IsNullOrWhiteSpace(user.CardId) ? null : user.CardId.Trim();
            user.CardId = normalizedCard;

            var index = list.FindIndex(u => u.Id == user.Id);
            if (index < 0 && normalizedCard is not null)
            {
                index = list.FindIndex(u =>
                    !string.IsNullOrWhiteSpace(u.CardId) &&
                    string.Equals(u.CardId.Trim(), normalizedCard, StringComparison.OrdinalIgnoreCase));
            }

            if (index >= 0)
                list[index] = user;
            else
                list.Add(user);

            // Ensure a reassigned card cannot remain attached to another operator.
            if (normalizedCard is not null)
            {
                var keptId = user.Id;
                foreach (var other in list)
                {
                    if (other.Id != keptId &&
                        !string.IsNullOrWhiteSpace(other.CardId) &&
                        string.Equals(other.CardId.Trim(), normalizedCard, StringComparison.OrdinalIgnoreCase))
                    {
                        other.CardId = null;
                    }
                }
            }

            await WriteFileAsync(list, ct);
            _logger.LogDebug("Saved RFID user {UserId} ({UserName}).", user.Id, user.Name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var list = (await ReadFileAsync(ct)).Where(u => u.Id != id).ToList();
            await WriteFileAsync(list, ct);
            _logger.LogDebug("Deleted RFID user {UserId}.", id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<RfidUser>> ReadFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath, ct);
        try
        {
            return JsonSerializer.Deserialize<List<RfidUser>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "RFID users file {FilePath} is corrupt; backed up to .bak and starting fresh.", _filePath);
            try { File.Copy(_filePath, _filePath + ".bak", true); }
            catch (IOException copyEx) { _logger.LogWarning(copyEx, "Could not back up corrupt RFID users file."); }
            return [];
        }
    }

    private async Task WriteFileAsync(List<RfidUser> users, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(users, JsonOptions);
        var tempPath = _filePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _filePath, true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (IOException) { }
        }
    }

    private static string GetDefaultFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrewAlert");
        return Path.Combine(appData, "rfid-users.json");
    }
}
