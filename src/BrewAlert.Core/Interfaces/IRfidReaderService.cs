namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Events;

/// <summary>
/// Reads RFID card scans from a dedicated hardware reader (Linux evdev)
/// or accepts keyboard-wedge fallback submissions from the UI layer.
/// </summary>
public interface IRfidReaderService
{
    /// <summary>True when a dedicated hardware reader device is exclusively grabbed.</summary>
    bool IsHardwareReaderConnected { get; }

    /// <summary>Raised when a card scan completes. May fire on a background thread.</summary>
    event EventHandler<RfidCardScannedEvent>? CardScanned;

    /// <summary>Submits a card identifier from the UI keyboard-wedge fallback or tests.</summary>
    void SubmitCardScan(string cardId);
}
