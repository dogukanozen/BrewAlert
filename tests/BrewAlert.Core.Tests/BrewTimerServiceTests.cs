using BrewAlert.Core.Events;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewTimerServiceTests : IDisposable
{
    private readonly BrewTimerService _sut;

    public BrewTimerServiceTests()
    {
        _sut = new BrewTimerService();
    }

    private static BrewProfile CreateProfile(string name = "Test", int seconds = 5) => new()
    {
        Name = name,
        BrewDuration = TimeSpan.FromSeconds(seconds),
        Type = BrewType.Tea
    };

    [Fact]
    public void Start_ShouldCreateSessionAndReturnIt()
    {
        var profile = CreateProfile();
        var session = _sut.Start(profile);

        Assert.NotNull(session);
        Assert.Equal(profile, session.Profile);
        Assert.Equal(BrewSessionState.Running, session.State);
        Assert.Contains(_sut.GetActiveSessions(), s => s.Id == session.Id);
    }

    [Fact]
    public void Start_TwiceConcurrently_BothSessionsAreActive()
    {
        var coffee = _sut.Start(CreateProfile("Coffee", 60));
        var tea = _sut.Start(CreateProfile("Tea", 90));

        var active = _sut.GetActiveSessions();
        Assert.Equal(2, active.Count);
        Assert.Contains(active, s => s.Id == coffee.Id);
        Assert.Contains(active, s => s.Id == tea.Id);
    }

    [Fact]
    public void Pause_ShouldUpdateOnlyTargetedSession()
    {
        var a = _sut.Start(CreateProfile("A", 60));
        var b = _sut.Start(CreateProfile("B", 60));

        _sut.Pause(a.Id);

        var active = _sut.GetActiveSessions();
        Assert.Equal(BrewSessionState.Paused, active.First(s => s.Id == a.Id).State);
        Assert.Equal(BrewSessionState.Running, active.First(s => s.Id == b.Id).State);
    }

    [Fact]
    public void Resume_ShouldUpdateState()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Pause(session.Id);
        _sut.Resume(session.Id);

        Assert.Equal(BrewSessionState.Running, _sut.GetActiveSessions().Single(s => s.Id == session.Id).State);
    }

    [Fact]
    public void Cancel_ShouldRemoveOnlyTargetedSession()
    {
        var a = _sut.Start(CreateProfile("A", 60));
        var b = _sut.Start(CreateProfile("B", 60));

        _sut.Cancel(a.Id);

        var active = _sut.GetActiveSessions();
        Assert.Single(active);
        Assert.Equal(b.Id, active[0].Id);
    }

    [Fact]
    public void Start_ShouldRaiseBrewStartedEvent()
    {
        BrewStartedEvent? startedEvent = null;
        _sut.BrewStarted += (_, e) => startedEvent = e;

        _sut.Start(CreateProfile());

        Assert.NotNull(startedEvent);
    }

    [Fact]
    public async Task Timer_ShouldComplete_WhenDurationElapses()
    {
        var tcs = new TaskCompletionSource<BrewCompletedEvent>();
        _sut.BrewCompleted += (_, e) => tcs.TrySetResult(e);

        _sut.Start(CreateProfile(seconds: 2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await tcs.Task.WaitAsync(cts.Token);

        Assert.Equal(BrewSessionState.Completed, result.Session.State);
    }

    [Fact]
    public async Task TimerTick_CarriesSessionIdSoConcurrentListenersCanFilter()
    {
        var a = _sut.Start(CreateProfile("A", 60));
        var b = _sut.Start(CreateProfile("B", 60));

        var seen = new HashSet<Guid>();
        var tcs = new TaskCompletionSource();
        _sut.TimerTick += (_, e) =>
        {
            lock (seen)
            {
                seen.Add(e.SessionId);
                if (seen.Count == 2) tcs.TrySetResult();
            }
        };

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(a.Id, seen);
        Assert.Contains(b.Id, seen);
    }

    [Fact]
    public void GetActiveSessions_WhenNone_ShouldReturnEmpty()
    {
        Assert.Empty(_sut.GetActiveSessions());
    }

    // Invariant (AGENT.md §4.2): events fire OUTSIDE the lock.
    // See original test for the cross-thread-probe reasoning.
    private const int LockProbeTimeoutMs = 1000;

    [Fact]
    public void BrewStarted_IsFiredOutsideLock()
    {
        Exception? failure = null;
        IReadOnlyList<BrewSession>? observed = null;
        _sut.BrewStarted += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSessions());
            if (!probe.Wait(LockProbeTimeoutMs))
                failure = new TimeoutException("BrewStarted fired inside the lock — cross-thread probe deadlocked.");
            else
                observed = probe.Result;
        };

        var session = _sut.Start(CreateProfile());

        Assert.Null(failure);
        Assert.NotNull(observed);
        Assert.Contains(observed!, s => s.Id == session.Id);
    }

    [Fact]
    public void BrewCancelled_IsFiredOutsideLock()
    {
        var session = _sut.Start(CreateProfile());
        Exception? failure = null;
        IReadOnlyList<BrewSession>? observed = null;
        _sut.BrewCancelled += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSessions());
            if (!probe.Wait(LockProbeTimeoutMs))
                failure = new TimeoutException("BrewCancelled fired inside the lock — cross-thread probe deadlocked.");
            else
                observed = probe.Result;
        };

        _sut.Cancel(session.Id);

        Assert.Null(failure);
        Assert.NotNull(observed);
        Assert.DoesNotContain(observed!, s => s.Id == session.Id);
    }

    [Fact]
    public async Task BrewCompleted_IsFiredOutsideLock()
    {
        var probeResult = new TaskCompletionSource<IReadOnlyList<BrewSession>?>();
        var probeTimedOut = new TaskCompletionSource<bool>();
        _sut.BrewCompleted += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSessions());
            if (!probe.Wait(LockProbeTimeoutMs))
                probeTimedOut.TrySetResult(true);
            else
                probeResult.TrySetResult(probe.Result);
        };

        _sut.Start(CreateProfile(seconds: 1));

        var finished = await Task.WhenAny(probeResult.Task, probeTimedOut.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(probeResult.Task, finished);
        Assert.Empty((await probeResult.Task)!);
    }

    public void Dispose() => _sut.Dispose();
}
