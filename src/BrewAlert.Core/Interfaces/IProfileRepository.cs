namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Models;

/// <summary>
/// Persists and retrieves brew profiles.
/// </summary>
public interface IProfileRepository
{
    Task<IReadOnlyList<BrewProfile>> GetAllAsync(CancellationToken ct = default);
    Task<BrewProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(BrewProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
