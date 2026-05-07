namespace BrewAlert.Core.Services;

using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Orchestrates brew profile CRUD operations through <see cref="IProfileRepository"/>.
/// Provides default profiles when the repository is empty.
/// </summary>
public sealed class BrewProfileService(IProfileRepository repository)
{
    public static IReadOnlyList<BrewProfile> DefaultProfiles =>
    [
        new() { Name = "Çay",    Type = BrewType.Tea,    BrewDuration = TimeSpan.FromMinutes(15), Icon = "♨", Description = "Klasik Türk çayı" },
        new() { Name = "Filtre Kahve", Type = BrewType.Coffee, BrewDuration = TimeSpan.FromMinutes(4), Icon = "☕", Description = "Filtre kahve" },
    ];

    /// <summary>
    /// Returns all profiles. Seeds any missing defaults on every call so new
    /// defaults added in future releases appear on existing installations.
    /// </summary>
    public async Task<IReadOnlyList<BrewProfile>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        var profiles = await repository.GetAllAsync(ct);
        var existingNames = profiles.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var missing = DefaultProfiles.Where(d => !existingNames.Contains(d.Name)).ToList();
        if (missing.Count > 0)
        {
            foreach (var p in missing)
                await repository.SaveAsync(p, ct);
            profiles = await repository.GetAllAsync(ct);
        }
        return profiles;
    }

    public Task<BrewProfile?> GetProfileAsync(Guid id, CancellationToken ct = default) =>
        repository.GetByIdAsync(id, ct);

    public Task SaveProfileAsync(BrewProfile profile, CancellationToken ct = default) =>
        repository.SaveAsync(profile, ct);

    public Task DeleteProfileAsync(Guid id, CancellationToken ct = default) =>
        repository.DeleteAsync(id, ct);
}
