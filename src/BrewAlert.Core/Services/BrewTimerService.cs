namespace BrewAlert.Core.Services;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Default implementation of <see cref="IBrewTimerService"/>.
/// Each session runs its own <see cref="PeriodicTimer"/> loop so coffee and tea
/// (and anything else) can brew side-by-side.
/// Thread-safe: dictionary and per-session state mutations are guarded by a single lock.
/// Events are fired OUTSIDE the lock to prevent re-entrant deadlocks.
/// </summary>
public sealed class BrewTimerService : IBrewTimerService, IDisposable
{
    private sealed class SessionEntry
    {
        public required BrewSession Session { get; init; }
        public required CancellationTokenSource Cts { get; init; }
    }

    private readonly Dictionary<Guid, SessionEntry> _sessions = [];
    private readonly object _lock = new();

    public event EventHandler<BrewTimerTickEvent>? TimerTick;
    public event EventHandler<BrewCompletedEvent>? BrewCompleted;
    public event EventHandler<BrewStartedEvent>? BrewStarted;
    public event EventHandler<BrewCancelledEvent>? BrewCancelled;

    public BrewSession Start(BrewProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        BrewSession session;
        CancellationTokenSource cts;

        lock (_lock)
        {
            cts = new CancellationTokenSource();
            session = new BrewSession
            {
                Profile = profile,
                StartedAtUtc = DateTime.UtcNow,
                EndsAtUtc = DateTime.UtcNow.Add(profile.BrewDuration),
                Remaining = profile.BrewDuration,
                State = BrewSessionState.Running
            };

            _sessions[session.Id] = new SessionEntry { Session = session, Cts = cts };
        }

        // Fire events and start timer OUTSIDE the lock
        _ = RunTimerLoopAsync(session, cts.Token);
        BrewStarted?.Invoke(this, new BrewStartedEvent(session));
        return session;
    }

    public void Cancel(Guid sessionId)
    {
        BrewCancelledEvent? cancelledEvent = null;
        CancellationTokenSource? ctsToDispose = null;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry)) return;

            entry.Session.State = BrewSessionState.Cancelled;
            cancelledEvent = new BrewCancelledEvent(entry.Session, entry.Session.Remaining);
            _sessions.Remove(sessionId);
            ctsToDispose = entry.Cts;
        }

        // Fire event OUTSIDE the lock
        ctsToDispose?.Cancel();
        ctsToDispose?.Dispose();
        if (cancelledEvent is not null)
        {
            BrewCancelled?.Invoke(this, cancelledEvent);
        }
    }

    public void Pause(Guid sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry)
                && entry.Session.State == BrewSessionState.Running)
            {
                entry.Session.State = BrewSessionState.Paused;
            }
        }
    }

    public void Resume(Guid sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry)
                && entry.Session.State == BrewSessionState.Paused)
            {
                entry.Session.State = BrewSessionState.Running;
            }
        }
    }

    public IReadOnlyList<BrewSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values
                .Select(e => e.Session)
                .OrderBy(s => s.StartedAtUtc)
                .ToList();
        }
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
                        _sessions.Remove(session.Id);
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
                    TimerTick?.Invoke(this, new BrewTimerTickEvent(session.Id, TimeSpan.Zero));
                    BrewCompleted?.Invoke(this, new BrewCompletedEvent(session));
                    break;
                }

                if (shouldTick)
                {
                    TimerTick?.Invoke(this, new BrewTimerTickEvent(session.Id, remaining));
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
        List<CancellationTokenSource> toDispose;
        lock (_lock)
        {
            toDispose = _sessions.Values.Select(e => e.Cts).ToList();
            _sessions.Clear();
        }
        foreach (var cts in toDispose)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
