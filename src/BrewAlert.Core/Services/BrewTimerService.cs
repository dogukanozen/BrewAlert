namespace BrewAlert.Core.Services;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Default implementation of <see cref="IBrewTimerService"/>.
/// Each session runs its own <see cref="PeriodicTimer"/> loop so coffee and tea
/// (and anything else) can brew side-by-side.
/// Remaining time is measured against a monotonic deadline, so a stalled or late tick
/// never stretches the brew, and wall-clock jumps (NTP on a Pi without RTC) cannot shorten it.
/// Thread-safe: dictionary and per-session state mutations are guarded by a single lock.
/// Events are fired OUTSIDE the lock to prevent re-entrant deadlocks.
/// Callers only ever receive snapshots; the live sessions never leave the lock.
/// </summary>
public sealed class BrewTimerService : IBrewTimerService, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private sealed class SessionEntry
    {
        public required BrewSession Session { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public long DeadlineTimestamp { get; set; }
    }

    private readonly TimeProvider _time;
    private readonly Dictionary<Guid, SessionEntry> _sessions = [];
    private readonly object _lock = new();

    public event EventHandler<BrewTimerTickEvent>? TimerTick;
    public event EventHandler<BrewCompletedEvent>? BrewCompleted;
    public event EventHandler<BrewStartedEvent>? BrewStarted;
    public event EventHandler<BrewCancelledEvent>? BrewCancelled;

    public BrewTimerService() : this(TimeProvider.System)
    {
    }

    public BrewTimerService(TimeProvider timeProvider)
    {
        _time = timeProvider;
    }

    public BrewSession Start(BrewProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        BrewSession snapshot;
        SessionEntry entry;

        lock (_lock)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            entry = new SessionEntry
            {
                Session = new BrewSession
                {
                    Profile = profile,
                    StartedAtUtc = now,
                    EndsAtUtc = now.Add(profile.BrewDuration),
                    Remaining = profile.BrewDuration,
                    State = BrewSessionState.Running
                },
                Cts = new CancellationTokenSource(),
                DeadlineTimestamp = TimestampAfter(profile.BrewDuration)
            };

            _sessions[entry.Session.Id] = entry;
            snapshot = Snapshot(entry.Session);
        }

        // Fire events and start timer OUTSIDE the lock
        _ = RunTimerLoopAsync(entry, entry.Cts.Token);
        BrewStarted?.Invoke(this, new BrewStartedEvent(snapshot));
        return snapshot;
    }

    public void Cancel(Guid sessionId)
    {
        BrewCancelledEvent? cancelledEvent = null;
        CancellationTokenSource? ctsToDispose = null;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry)) return;

            if (entry.Session.State == BrewSessionState.Running)
                entry.Session.Remaining = RemainingUntil(entry.DeadlineTimestamp);
            entry.Session.State = BrewSessionState.Cancelled;
            cancelledEvent = new BrewCancelledEvent(Snapshot(entry.Session), entry.Session.Remaining);
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
                entry.Session.Remaining = RemainingUntil(entry.DeadlineTimestamp);
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
                entry.DeadlineTimestamp = TimestampAfter(entry.Session.Remaining);
                entry.Session.EndsAtUtc = _time.GetUtcNow().UtcDateTime.Add(entry.Session.Remaining);
                entry.Session.State = BrewSessionState.Running;
            }
        }
    }

    public IReadOnlyList<BrewSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values
                .Select(e => Snapshot(e.Session))
                .OrderBy(s => s.StartedAtUtc)
                .ToList();
        }
    }

    private async Task RunTimerLoopAsync(SessionEntry entry, CancellationToken ct)
    {
        var session = entry.Session;
        CancellationTokenSource? completedCts = null;
        try
        {
            using var timer = new PeriodicTimer(TickInterval, _time);

            while (await timer.WaitForNextTickAsync(ct))
            {
                bool shouldTick = false;
                bool shouldComplete = false;
                TimeSpan remaining;
                BrewSession snapshot;

                lock (_lock)
                {
                    if (session.State == BrewSessionState.Paused) continue;
                    if (session.State != BrewSessionState.Running) break;

                    session.Remaining = RemainingUntil(entry.DeadlineTimestamp);

                    if (session.Remaining <= TimeSpan.Zero)
                    {
                        session.Remaining = TimeSpan.Zero;
                        session.State = BrewSessionState.Completed;
                        // Capture the CTS so we can dispose it after leaving the lock.
                        // Removing the entry here means a racing Cancel/Dispose can't
                        // dispose the same CTS again.
                        if (_sessions.Remove(session.Id))
                        {
                            completedCts = entry.Cts;
                        }
                        shouldComplete = true;
                    }
                    else
                    {
                        shouldTick = true;
                    }

                    remaining = session.Remaining;
                    snapshot = Snapshot(session);
                }

                // Fire events OUTSIDE the lock (AGENT.md §4.2).
                if (shouldComplete)
                {
                    TimerTick?.Invoke(this, new BrewTimerTickEvent(session.Id, TimeSpan.Zero));
                    BrewCompleted?.Invoke(this, new BrewCompletedEvent(snapshot));
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
            // Expected on cancel — Cancel() owns disposing the CTS in that path.
        }
        finally
        {
            // Normal-completion path: CTS was captured above and is now safe to dispose
            // (no further WaitForNextTickAsync calls reference its token).
            completedCts?.Dispose();
        }
    }

    private long TimestampAfter(TimeSpan duration) =>
        _time.GetTimestamp() + (long)(duration.TotalSeconds * _time.TimestampFrequency);

    // Rounded to whole seconds: a tick that lands a few milliseconds early must not
    // hold the countdown on the same second or delay completion by a full tick.
    private TimeSpan RemainingUntil(long deadlineTimestamp)
    {
        var seconds = (deadlineTimestamp - _time.GetTimestamp()) / (double)_time.TimestampFrequency;
        return TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds, MidpointRounding.AwayFromZero)));
    }

    private static BrewSession Snapshot(BrewSession session) => new()
    {
        Id = session.Id,
        Profile = session.Profile,
        StartedAtUtc = session.StartedAtUtc,
        EndsAtUtc = session.EndsAtUtc,
        Remaining = session.Remaining,
        State = session.State
    };

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
