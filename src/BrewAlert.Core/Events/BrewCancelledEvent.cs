namespace BrewAlert.Core.Events;

using BrewAlert.Core.Models;

/// <summary>Raised when the user cancels an active brew session.</summary>
public sealed record BrewCancelledEvent(BrewSession Session, TimeSpan RemainingTime);
