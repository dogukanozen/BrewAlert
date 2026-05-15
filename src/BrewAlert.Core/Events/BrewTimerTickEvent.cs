namespace BrewAlert.Core.Events;

/// <summary>Per-second tick. Carries the session id so multi-brew listeners can filter.</summary>
public sealed record BrewTimerTickEvent(Guid SessionId, TimeSpan Remaining);
