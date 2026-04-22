namespace BrewAlert.Core.Events;

using BrewAlert.Core.Models;

/// <summary>Raised when a new brew session begins.</summary>
public sealed record BrewStartedEvent(BrewSession Session);
