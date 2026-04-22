namespace BrewAlert.Core.Events;

using BrewAlert.Core.Models;

/// <summary>Raised when a brew session timer reaches zero.</summary>
public sealed record BrewCompletedEvent(BrewSession Session);
