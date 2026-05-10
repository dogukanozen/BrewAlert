using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class BrewHistoryServiceTests
{
    [Fact]
    public async Task BrewCompleted_AppendsEntryAndRaisesHistoryUpdated()
    {
        var timer = new FakeTimer();
        var repo = new InMemoryHistoryRepository();
        var service = new BrewHistoryService(timer, repo, NullLogger<BrewHistoryService>.Instance);

        BrewHistoryEntry? raised = null;
        service.HistoryUpdated += (_, e) => raised = e;

        var profile = new BrewProfile { Name = "Yeşil Çay", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(3), Icon = "🍵" };
        var session = new BrewSession
        {
            Profile = profile,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-3),
            EndsAtUtc = DateTime.UtcNow,
            Remaining = TimeSpan.Zero,
            State = BrewSessionState.Completed,
        };

        timer.Raise(new BrewCompletedEvent(session));

        // Persistence is fire-and-forget; wait briefly for the in-memory repo to record it.
        for (int i = 0; i < 50 && repo.Entries.Count == 0; i++) await Task.Delay(10);

        Assert.Single(repo.Entries);
        Assert.Equal("Yeşil Çay", repo.Entries[0].ProfileName);
        Assert.Equal(BrewType.Tea, repo.Entries[0].Type);
        Assert.Equal("🍵", repo.Entries[0].Icon);
        Assert.NotNull(raised);
        Assert.Equal(repo.Entries[0].Id, raised!.Id);

        service.Dispose();
    }

    [Fact]
    public void Dispose_UnsubscribesFromTimer()
    {
        var timer = new FakeTimer();
        var repo = new InMemoryHistoryRepository();
        var service = new BrewHistoryService(timer, repo, NullLogger<BrewHistoryService>.Instance);

        service.Dispose();

        var profile = new BrewProfile { Name = "X", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(1) };
        var session = new BrewSession { Profile = profile, EndsAtUtc = DateTime.UtcNow };
        timer.Raise(new BrewCompletedEvent(session));

        Assert.Empty(repo.Entries);
    }

    private sealed class FakeTimer : IBrewTimerService
    {
        public event EventHandler<TimeSpan>? TimerTick;
        public event EventHandler<BrewCompletedEvent>? BrewCompleted;
        public event EventHandler<BrewStartedEvent>? BrewStarted;
        public event EventHandler<BrewCancelledEvent>? BrewCancelled;

        public BrewSession Start(BrewProfile profile) => throw new NotSupportedException();
        public void Cancel(Guid sessionId) { }
        public void Pause(Guid sessionId) { }
        public void Resume(Guid sessionId) { }
        public BrewSession? GetActiveSession() => null;

        public void Raise(BrewCompletedEvent e) => BrewCompleted?.Invoke(this, e);

        // Suppress unused-event warnings.
        public void Touch() { TimerTick?.Invoke(this, default); BrewStarted?.Invoke(this, null!); BrewCancelled?.Invoke(this, null!); }
    }

    private sealed class InMemoryHistoryRepository : IBrewHistoryRepository
    {
        public List<BrewHistoryEntry> Entries { get; } = new();

        public Task AppendAsync(BrewHistoryEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BrewHistoryEntry>> GetRecentAsync(int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BrewHistoryEntry>>(Entries.OrderByDescending(e => e.CompletedAtUtc).Take(limit).ToList());

        public Task<IReadOnlyList<BrewHistoryEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BrewHistoryEntry>>(Entries.OrderByDescending(e => e.CompletedAtUtc).ToList());
    }
}
