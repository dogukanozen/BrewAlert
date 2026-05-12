using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using NSubstitute;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewTimerServiceTests : IDisposable
{
    private readonly BrewTimerService _sut;

    public BrewTimerServiceTests()
    {
        _sut = new BrewTimerService();
    }

    private BrewProfile CreateProfile(string name = "Test", int seconds = 5) => new()
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
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ShouldThrowException()
    {
        _sut.Start(CreateProfile());
        Assert.Throws<InvalidOperationException>(() => _sut.Start(CreateProfile()));
    }

    [Fact]
    public void Pause_ShouldUpdateState()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Pause(session.Id);

        var active = _sut.GetActiveSession();
        Assert.NotNull(active);
        Assert.Equal(BrewSessionState.Paused, active.State);
    }

    [Fact]
    public void Resume_ShouldUpdateState()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Pause(session.Id);
        _sut.Resume(session.Id);

        var active = _sut.GetActiveSession();
        Assert.NotNull(active);
        Assert.Equal(BrewSessionState.Running, active.State);
    }

    [Fact]
    public void Cancel_ShouldClearSession()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Cancel(session.Id);

        Assert.Null(_sut.GetActiveSession());
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

        // Use WaitAsync with timeout instead of flaky WhenAny(Delay)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await tcs.Task.WaitAsync(cts.Token);
        
        Assert.Equal(BrewSessionState.Completed, result.Session.State);
    }

    [Fact]
    public void GetActiveSession_WhenNone_ShouldReturnNull()
    {
        Assert.Null(_sut.GetActiveSession());
    }

    // Invariant (AGENT.md §4.2): events fire OUTSIDE the lock. Each handler below
    // probes the same lock from a *different* thread. C# locks are re-entrant on
    // the same thread, so a handler running on the firing thread would still get
    // through even if the event were fired inside the lock — a same-thread probe
    // would mask the regression. A separate-thread probe must acquire the lock
    // independently; if the event were fired inside the lock the firing thread
    // would still be holding it while waiting on the probe, hence deadlock and
    // the bounded Wait() returns false.
    private const int LockProbeTimeoutMs = 1000;

    [Fact]
    public void BrewStarted_IsFiredOutsideLock()
    {
        Exception? failure = null;
        BrewSession? observed = null;
        _sut.BrewStarted += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSession());
            if (!probe.Wait(LockProbeTimeoutMs))
                failure = new TimeoutException("BrewStarted fired inside the lock — cross-thread probe deadlocked.");
            else
                observed = probe.Result;
        };

        var session = _sut.Start(CreateProfile());

        Assert.Null(failure);
        Assert.NotNull(observed);
        Assert.Equal(session.Id, observed!.Id);
    }

    [Fact]
    public void BrewCancelled_IsFiredOutsideLock()
    {
        var session = _sut.Start(CreateProfile());
        Exception? failure = null;
        BrewSession? observed = session;
        _sut.BrewCancelled += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSession());
            if (!probe.Wait(LockProbeTimeoutMs))
                failure = new TimeoutException("BrewCancelled fired inside the lock — cross-thread probe deadlocked.");
            else
                observed = probe.Result;
        };

        _sut.Cancel(session.Id);

        Assert.Null(failure);
        Assert.Null(observed);
    }

    [Fact]
    public async Task BrewCompleted_IsFiredOutsideLock()
    {
        var probeResult = new TaskCompletionSource<BrewSession?>();
        var probeTimedOut = new TaskCompletionSource<bool>();
        _sut.BrewCompleted += (_, _) =>
        {
            var probe = Task.Run(() => _sut.GetActiveSession());
            if (!probe.Wait(LockProbeTimeoutMs))
                probeTimedOut.TrySetResult(true);
            else
                probeResult.TrySetResult(probe.Result);
        };

        _sut.Start(CreateProfile(seconds: 1));

        var finished = await Task.WhenAny(probeResult.Task, probeTimedOut.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(probeResult.Task, finished);
        Assert.Null(await probeResult.Task);
    }

    public void Dispose() => _sut.Dispose();
}
