namespace BrewAlert.Infrastructure.Persistence;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Subscribes to <see cref="IBrewTimerService.BrewCompleted"/> and persists each completed
/// session via <see cref="IBrewHistoryRepository"/>. Raises <see cref="HistoryUpdated"/>
/// after persistence so the UI can refresh.
/// </summary>
public sealed class BrewHistoryService : IBrewHistoryService, IDisposable
{
    private readonly IBrewTimerService _timer;
    private readonly IBrewHistoryRepository _repository;
    private readonly ILogger<BrewHistoryService> _logger;

    public event EventHandler<BrewHistoryEntry>? HistoryUpdated;

    public BrewHistoryService(
        IBrewTimerService timer,
        IBrewHistoryRepository repository,
        ILogger<BrewHistoryService> logger)
    {
        _timer = timer;
        _repository = repository;
        _logger = logger;
        _timer.BrewCompleted += OnBrewCompleted;
    }

    public Task<IReadOnlyList<BrewHistoryEntry>> GetRecentAsync(int limit, CancellationToken ct = default)
        => _repository.GetRecentAsync(limit, ct);

    private void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        var session = e.Session;
        var duration = session.EndsAtUtc - session.StartedAtUtc;
        var entry = new BrewHistoryEntry(
            Id: session.Id,
            CompletedAtUtc: DateTime.UtcNow,
            ProfileName: session.Profile.Name,
            Type: session.Profile.Type,
            Icon: session.Profile.Icon,
            DurationSeconds: (int)Math.Max(0, duration.TotalSeconds));

        _ = PersistAsync(entry);
    }

    private async Task PersistAsync(BrewHistoryEntry entry)
    {
        try
        {
            await _repository.AppendAsync(entry);
            HistoryUpdated?.Invoke(this, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist brew history entry {EntryId}.", entry.Id);
        }
    }

    public void Dispose()
    {
        _timer.BrewCompleted -= OnBrewCompleted;
    }
}
