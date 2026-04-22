namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Events;
using BrewAlert.Core.Models;

/// <summary>
/// Manages brew countdown timers. Only one session can be active at a time.
/// </summary>
public interface IBrewTimerService
{
    /// <summary>Start a brew session with the given profile.</summary>
    BrewSession Start(BrewProfile profile);

    /// <summary>Cancel the active brew session.</summary>
    void Cancel(Guid sessionId);

    /// <summary>Pause the active brew session.</summary>
    void Pause(Guid sessionId);

    /// <summary>Resume a paused brew session.</summary>
    void Resume(Guid sessionId);

    /// <summary>Get the currently active session, if any.</summary>
    BrewSession? GetActiveSession();

    /// <summary>Fired every second with the remaining time.</summary>
    event EventHandler<TimeSpan>? TimerTick;

    /// <summary>Fired when the brew timer reaches zero.</summary>
    event EventHandler<BrewCompletedEvent>? BrewCompleted;

    /// <summary>Fired when a brew session starts.</summary>
    event EventHandler<BrewStartedEvent>? BrewStarted;

    /// <summary>Fired when a brew session is cancelled.</summary>
    event EventHandler<BrewCancelledEvent>? BrewCancelled;
}
