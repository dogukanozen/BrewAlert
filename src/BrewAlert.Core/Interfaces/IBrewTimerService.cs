namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Events;
using BrewAlert.Core.Models;

/// <summary>
/// Manages brew countdown timers. Supports any number of concurrent sessions —
/// each call to <see cref="Start"/> returns a distinct session that runs independently.
/// </summary>
public interface IBrewTimerService
{
    /// <summary>Start a new brew session. Multiple concurrent sessions are allowed.</summary>
    BrewSession Start(BrewProfile profile);

    /// <summary>Cancel a specific brew session.</summary>
    void Cancel(Guid sessionId);

    /// <summary>Pause a specific brew session.</summary>
    void Pause(Guid sessionId);

    /// <summary>Resume a specific paused brew session.</summary>
    void Resume(Guid sessionId);

    /// <summary>Snapshot of all currently running/paused sessions, ordered by start time.</summary>
    IReadOnlyList<BrewSession> GetActiveSessions();

    /// <summary>Fired every second for each active session.</summary>
    event EventHandler<BrewTimerTickEvent>? TimerTick;

    /// <summary>Fired when a brew session's timer reaches zero.</summary>
    event EventHandler<BrewCompletedEvent>? BrewCompleted;

    /// <summary>Fired when a new brew session starts.</summary>
    event EventHandler<BrewStartedEvent>? BrewStarted;

    /// <summary>Fired when a brew session is cancelled.</summary>
    event EventHandler<BrewCancelledEvent>? BrewCancelled;
}
