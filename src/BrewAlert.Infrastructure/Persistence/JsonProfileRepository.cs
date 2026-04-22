namespace BrewAlert.Infrastructure.Persistence;

using System.Text.Json;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Persists brew profiles to a local JSON file.
/// Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class JsonProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<JsonProfileRepository> _logger;

    public JsonProfileRepository(ILogger<JsonProfileRepository> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public async Task<IReadOnlyList<BrewProfile>> GetAllAsync(CancellationToken ct = default)
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

    public async Task<BrewProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var profiles = await GetAllAsync(ct);
        return profiles.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveAsync(BrewProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _semaphore.WaitAsync(ct);
        try
        {
            var profiles = await ReadFileAsync(ct);
            var list = profiles.ToList();
            var index = list.FindIndex(p => p.Id == profile.Id);

            if (index >= 0)
                list[index] = profile;
            else
                list.Add(profile);

            await WriteFileAsync(list, ct);
            _logger.LogDebug("Saved profile {ProfileId} ({ProfileName}).", profile.Id, profile.Name);
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
            var profiles = await ReadFileAsync(ct);
            var list = profiles.Where(p => p.Id != id).ToList();
            await WriteFileAsync(list, ct);
            _logger.LogDebug("Deleted profile {ProfileId}.", id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<BrewProfile>> ReadFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath, ct);
        return JsonSerializer.Deserialize<List<BrewProfile>>(json, JsonOptions) ?? [];
    }

    private async Task WriteFileAsync(List<BrewProfile> profiles, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    private static string GetDefaultFilePath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrewAlert");
        return Path.Combine(appData, "profiles.json");
    }
}
