namespace BrewAlert.Core.Events;

/// <summary>
/// Event raised when an RFID card is scanned by the hardware reader or keyboard fallback.
/// </summary>
public sealed record RfidCardScannedEvent(string CardId, DateTime ScannedAtUtc);
