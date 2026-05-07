namespace BrewAlert.Core.Services;

using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Orchestrates brew profile CRUD operations through <see cref="IProfileRepository"/>.
/// Provides default profiles when the repository is empty.
/// </summary>
public sealed class BrewProfileService(IProfileRepository repository)
{
    // Fixed GUIDs so defaults can be tracked by identity, not by name.
    // Prevents duplicates when a user renames a default profile.
    public static IReadOnlyList<BrewProfile> DefaultProfiles =>
    [
        new() { Id = new Guid("00000000-0000-0000-0000-000000000001"), Name = "Çay",          Type = BrewType.Tea,    BrewDuration = TimeSpan.FromMinutes(15), Icon = "♨",  Description = "Klasik Türk çayı" },
        new() { Id = new Guid("00000000-0000-0000-0000-000000000002"), Name = "Filtre Kahve", Type = BrewType.Coffee, BrewDuration = TimeSpan.FromMinutes(4),  Icon = "☕", Description = "Filtre kahve"     },
    ];

    /// <summary>
    /// Returns all profiles. Seeds any missing defaults on every call so new
    /// defaults added in future releases appear on existing installations.
    /// Checks by stable ID to prevent duplicates when defaults are renamed.
    /// </summary>
    public async Task<IReadOnlyList<BrewProfile>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        var profiles = await repository.GetAllAsync(ct);
        var existingIds = profiles.Select(p => p.Id).ToHashSet();
        var toSeed = DefaultProfiles.Where(d => !existingIds.Contains(d.Id)).ToList();

        if (toSeed.Count == 0)
            return profiles;

        // Migration path for installations predating stable default IDs:
        // if a profile with the same name already exists under a random GUID, replace
        // it in-place with the canonical stable-ID version to avoid duplicate entries.
        // FirstOrDefault is used instead of ToDictionary to tolerate duplicate names
        // that can exist due to manual JSON editing or legacy data.
        var result = profiles.ToList();

        foreach (var def in toSeed)
        {
            var legacy = profiles.FirstOrDefault(p => string.Equals(p.Name, def.Name, StringComparison.Ordinal));
            if (legacy is not null)
            {
                var migrated = new BrewProfile
                {
                    Id           = def.Id,
                    Name         = legacy.Name,
                    Type         = legacy.Type,
                    BrewDuration = legacy.BrewDuration,
                    Icon         = legacy.Icon,
                    Description  = legacy.Description,
                    CreatedAtUtc = legacy.CreatedAtUtc,
                };
                await repository.SaveAsync(migrated, ct);
                await repository.DeleteAsync(legacy.Id, ct);
                result[result.IndexOf(legacy)] = migrated;
            }
            else
            {
                await repository.SaveAsync(def, ct);
                result.Add(def);
            }
        }

        return result;
    }

    public Task<BrewProfile?> GetProfileAsync(Guid id, CancellationToken ct = default) =>
        repository.GetByIdAsync(id, ct);

    public Task SaveProfileAsync(BrewProfile profile, CancellationToken ct = default) =>
        repository.SaveAsync(profile, ct);

    public Task DeleteProfileAsync(Guid id, CancellationToken ct = default) =>
        repository.DeleteAsync(id, ct);
}
