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
        using var service = new BrewHistoryService(timer, repo, NullLogger<BrewHistoryService>.Instance);

        var raised = new TaskCompletionSource<BrewHistoryEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.HistoryUpdated += (_, e) => raised.TrySetResult(e);

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

        var entry = await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(repo.Entries);
        Assert.Equal("Yeşil Çay", repo.Entries[0].ProfileName);
        Assert.Equal(BrewType.Tea, repo.Entries[0].Type);
        Assert.Equal("🍵", repo.Entries[0].Icon);
        Assert.Equal(repo.Entries[0].Id, entry.Id);
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
#pragma warning disable CS0067 // unused — interface contract
        public event EventHandler<BrewTimerTickEvent>? TimerTick;
        public event EventHandler<BrewStartedEvent>? BrewStarted;
        public event EventHandler<BrewCancelledEvent>? BrewCancelled;
#pragma warning restore CS0067
        public event EventHandler<BrewCompletedEvent>? BrewCompleted;

        public BrewSession Start(BrewProfile profile) => throw new NotSupportedException();
        public void Cancel(Guid sessionId) { }
        public void Pause(Guid sessionId) { }
        public void Resume(Guid sessionId) { }
        public IReadOnlyList<BrewSession> GetActiveSessions() => Array.Empty<BrewSession>();

        public void Raise(BrewCompletedEvent e) => BrewCompleted?.Invoke(this, e);
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
