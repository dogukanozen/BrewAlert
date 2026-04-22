namespace BrewAlert.Core.Models;

/// <summary>
/// Represents an active or completed brew session with timer state.
/// </summary>
public sealed class BrewSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required BrewProfile Profile { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime EndsAtUtc { get; init; }
    public TimeSpan Remaining { get; set; }
    public BrewSessionState State { get; set; } = BrewSessionState.Running;
}

/// <summary>
/// The lifecycle state of a brew session.
/// </summary>
public enum BrewSessionState
{
    Running,
    Paused,
    Completed,
    Cancelled
}
