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
        new() { Name = "Turkish Tea",   Type = BrewType.Tea,    BrewDuration = TimeSpan.FromMinutes(8),  Icon = "🍵", Description = "Classic Turkish tea" },
        new() { Name = "Green Tea",     Type = BrewType.Tea,    BrewDuration = TimeSpan.FromMinutes(3),  Icon = "🍵", Description = "Light and delicate" },
        new() { Name = "Black Tea",     Type = BrewType.Tea,    BrewDuration = TimeSpan.FromMinutes(5),  Icon = "🍵", Description = "Bold and strong" },
        new() { Name = "French Press",  Type = BrewType.Coffee, BrewDuration = TimeSpan.FromMinutes(4),  Icon = "☕", Description = "Full-bodied coffee" },
        new() { Name = "Pour Over",     Type = BrewType.Coffee, BrewDuration = TimeSpan.FromMinutes(3),  Icon = "☕", Description = "Clean and bright" },
        new() { Name = "Espresso",      Type = BrewType.Coffee, BrewDuration = TimeSpan.FromSeconds(25), Icon = "☕", Description = "Quick and intense" },
    ];

    /// <summary>
    /// Returns all profiles. Seeds defaults if the repository is empty.
    /// </summary>
    public async Task<IReadOnlyList<BrewProfile>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        var profiles = await repository.GetAllAsync(ct);
        if (profiles.Count == 0)
        {
            foreach (var p in DefaultProfiles)
            {
                await repository.SaveAsync(p, ct);
            }
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
