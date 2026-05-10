namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Models;

/// <summary>Subscribes to brew completion events and persists them.</summary>
public interface IBrewHistoryService
{
    /// <summary>Fired after a completed brew has been persisted.</summary>
    event EventHandler<BrewHistoryEntry>? HistoryUpdated;

    Task<IReadOnlyList<BrewHistoryEntry>> GetRecentAsync(int limit, CancellationToken ct = default);
}
