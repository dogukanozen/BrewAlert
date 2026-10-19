using System.Threading.Channels;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewTimerServiceClockTests : IDisposable
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _clock = new();
    private readonly BrewTimerService _sut;
    private readonly Channel<TimeSpan> _ticks = Channel.CreateUnbounded<TimeSpan>();
    private readonly TaskCompletionSource<BrewSession> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BrewTimerServiceClockTests()
    {
        _sut = new BrewTimerService(_clock);
        _sut.TimerTick += (_, e) => _ticks.Writer.TryWrite(e.Remaining);
        _sut.BrewCompleted += (_, e) => _completed.TrySetResult(e.Session);
    }

    private static BrewProfile Profile(int seconds) => new()
    {
        Name = "Tea",
        BrewDuration = TimeSpan.FromSeconds(seconds),
        Type = BrewType.Tea
    };

    private async Task AdvanceAndWaitForRemainingAsync(TimeSpan advance, int expectedSeconds)
    {
        _clock.Advance(advance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await _ticks.Reader.ReadAsync(timeout.Token) != TimeSpan.FromSeconds(expectedSeconds))
        {
        }
    }

    [Fact]
    public async Task StalledTimer_DoesNotStretchTheBrew()
    {
        _sut.Start(Profile(10));

        await AdvanceAndWaitForRemainingAsync(Second, 9);
        // A 5 s stall: the periodic timer coalesces the missed ticks into one.
        await AdvanceAndWaitForRemainingAsync(TimeSpan.FromSeconds(5), 4);
        for (var remaining = 3; remaining >= 1; remaining--)
            await AdvanceAndWaitForRemainingAsync(Second, remaining);

        Assert.False(_completed.Task.IsCompleted);
        await AdvanceAndWaitForRemainingAsync(Second, 0);

        var session = await _completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(BrewSessionState.Completed, session.State);
        Assert.Empty(_sut.GetActiveSessions());
    }

    [Fact]
    public async Task Pause_FreezesRemaining_AndResumeMovesTheEnd()
    {
        var started = _sut.Start(Profile(10));
        await AdvanceAndWaitForRemainingAsync(Second, 9);

        _sut.Pause(started.Id);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var paused = Assert.Single(_sut.GetActiveSessions());
        Assert.Equal(BrewSessionState.Paused, paused.State);
        Assert.Equal(TimeSpan.FromSeconds(9), paused.Remaining);

        _sut.Resume(started.Id);
        var resumed = Assert.Single(_sut.GetActiveSessions());
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddSeconds(9), resumed.EndsAtUtc);

        for (var remaining = 8; remaining >= 0; remaining--)
            await AdvanceAndWaitForRemainingAsync(Second, remaining);
        await _completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Snapshots_DoNotTrackLaterChanges()
    {
        var started = _sut.Start(Profile(10));

        _sut.Pause(started.Id);

        Assert.Equal(BrewSessionState.Running, started.State);
        Assert.Equal(BrewSessionState.Paused, Assert.Single(_sut.GetActiveSessions()).State);
    }

    public void Dispose() => _sut.Dispose();
}
