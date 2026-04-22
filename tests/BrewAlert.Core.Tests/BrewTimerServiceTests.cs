using BrewAlert.Core.Events;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewTimerServiceTests : IDisposable
{
    private readonly BrewTimerService _sut = new();

    private static BrewProfile CreateProfile(int seconds = 3) => new()
    {
        Name = "Test Brew",
        Type = BrewType.Tea,
        BrewDuration = TimeSpan.FromSeconds(seconds)
    };

    [Fact]
    public void Start_ShouldCreateActiveSession()
    {
        var profile = CreateProfile();
        var session = _sut.Start(profile);

        Assert.NotNull(session);
        Assert.Equal(BrewSessionState.Running, session.State);
        Assert.Equal(profile.Name, session.Profile.Name);
    }

    [Fact]
    public void Start_WhenAlreadyActive_ShouldThrow()
    {
        _sut.Start(CreateProfile());
        Assert.Throws<InvalidOperationException>(() => _sut.Start(CreateProfile()));
    }

    [Fact]
    public void Cancel_ShouldStopActiveSession()
    {
        var session = _sut.Start(CreateProfile());
        BrewCancelledEvent? cancelledEvent = null;
        _sut.BrewCancelled += (_, e) => cancelledEvent = e;

        _sut.Cancel(session.Id);

        Assert.Null(_sut.GetActiveSession());
        Assert.NotNull(cancelledEvent);
    }

    [Fact]
    public void Cancel_WithWrongId_ShouldDoNothing()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Cancel(Guid.NewGuid());

        Assert.NotNull(_sut.GetActiveSession());
    }

    [Fact]
    public void Pause_ShouldChangeStateToPaused()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Pause(session.Id);

        var active = _sut.GetActiveSession();
        Assert.NotNull(active);
        Assert.Equal(BrewSessionState.Paused, active.State);
    }

    [Fact]
    public void Resume_ShouldChangeStateToRunning()
    {
        var session = _sut.Start(CreateProfile());
        _sut.Pause(session.Id);
        _sut.Resume(session.Id);

        var active = _sut.GetActiveSession();
        Assert.NotNull(active);
        Assert.Equal(BrewSessionState.Running, active.State);
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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        var result = await tcs.Task;
        Assert.Equal(BrewSessionState.Completed, result.Session.State);
    }

    [Fact]
    public void GetActiveSession_WhenNone_ShouldReturnNull()
    {
        Assert.Null(_sut.GetActiveSession());
    }

    public void Dispose() => _sut.Dispose();
}
