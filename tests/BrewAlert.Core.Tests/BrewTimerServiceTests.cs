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

    public void Dispose() => _sut.Dispose();
}
