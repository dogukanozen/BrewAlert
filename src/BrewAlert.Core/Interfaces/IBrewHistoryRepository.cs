namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Models;

/// <summary>Persists completed brew sessions.</summary>
public interface IBrewHistoryRepository
{
    Task AppendAsync(BrewHistoryEntry entry, CancellationToken ct = default);

    /// <summary>Returns the newest <paramref name="limit"/> entries, newest first.</summary>
    Task<IReadOnlyList<BrewHistoryEntry>> GetRecentAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<BrewHistoryEntry>> GetAllAsync(CancellationToken ct = default);
}
