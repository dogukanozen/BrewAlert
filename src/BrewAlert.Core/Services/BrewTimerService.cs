namespace BrewAlert.Core.Services;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Default implementation of <see cref="IBrewTimerService"/>.
/// Uses a background task with <see cref="PeriodicTimer"/> for second-by-second countdown.
/// Thread-safe: all state mutations are guarded by a lock.
/// Events are fired OUTSIDE the lock to prevent potential deadlocks.
/// </summary>
public sealed class BrewTimerService : IBrewTimerService, IDisposable
{
    private BrewSession? _activeSession;
    private CancellationTokenSource? _timerCts;
    private readonly object _lock = new();

    public event EventHandler<TimeSpan>? TimerTick;
    public event EventHandler<BrewCompletedEvent>? BrewCompleted;
    public event EventHandler<BrewStartedEvent>? BrewStarted;
    public event EventHandler<BrewCancelledEvent>? BrewCancelled;

    public BrewSession Start(BrewProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        BrewSession session;

        lock (_lock)
        {
            if (_activeSession is { State: BrewSessionState.Running or BrewSessionState.Paused })
            {
                throw new InvalidOperationException("A brew session is already active. Cancel it first.");
            }

            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _timerCts = new CancellationTokenSource();

            session = new BrewSession
            {
                Profile = profile,
                StartedAtUtc = DateTime.UtcNow,
                EndsAtUtc = DateTime.UtcNow.Add(profile.BrewDuration),
                Remaining = profile.BrewDuration,
                State = BrewSessionState.Running
            };

            _activeSession = session;
        }

        // Fire events and start timer OUTSIDE the lock
        _ = RunTimerLoopAsync(session, _timerCts.Token);
        BrewStarted?.Invoke(this, new BrewStartedEvent(session));
        return session;
    }

    public void Cancel(Guid sessionId)
    {
        BrewCancelledEvent? cancelledEvent = null;

        lock (_lock)
        {
            if (_activeSession is null || _activeSession.Id != sessionId) return;

            _timerCts?.Cancel();
            _activeSession.State = BrewSessionState.Cancelled;
            cancelledEvent = new BrewCancelledEvent(_activeSession, _activeSession.Remaining);
            _activeSession = null;
        }

        // Fire event OUTSIDE the lock
        if (cancelledEvent is not null)
        {
            BrewCancelled?.Invoke(this, cancelledEvent);
        }
    }

    public void Pause(Guid sessionId)
    {
        lock (_lock)
        {
            if (_activeSession is not null && _activeSession.Id == sessionId
                && _activeSession.State == BrewSessionState.Running)
            {
                _activeSession.State = BrewSessionState.Paused;
            }
        }
    }

    public void Resume(Guid sessionId)
    {
        lock (_lock)
        {
            if (_activeSession is not null && _activeSession.Id == sessionId
                && _activeSession.State == BrewSessionState.Paused)
            {
                _activeSession.State = BrewSessionState.Running;
            }
        }
    }

    public BrewSession? GetActiveSession()
    {
        lock (_lock) { return _activeSession; }
    }

    private async Task RunTimerLoopAsync(BrewSession session, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (await timer.WaitForNextTickAsync(ct))
            {
                bool shouldTick = false;
                bool shouldComplete = false;
                TimeSpan remaining;

                lock (_lock)
                {
                    if (session.State == BrewSessionState.Paused) continue;
                    if (session.State != BrewSessionState.Running) break;

                    session.Remaining -= TimeSpan.FromSeconds(1);

                    if (session.Remaining <= TimeSpan.Zero)
                    {
                        session.Remaining = TimeSpan.Zero;
                        session.State = BrewSessionState.Completed;
                        _activeSession = null;
                        shouldComplete = true;
                    }
                    else
                    {
                        shouldTick = true;
                    }

                    remaining = session.Remaining;
                }

                // Fire events OUTSIDE the lock
                if (shouldComplete)
                {
                    TimerTick?.Invoke(this, TimeSpan.Zero);
                    BrewCompleted?.Invoke(this, new BrewCompletedEvent(session));
                    break;
                }

                if (shouldTick)
                {
                    TimerTick?.Invoke(this, remaining);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancel — no action needed.
        }
    }

    public void Dispose()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
    }
}
