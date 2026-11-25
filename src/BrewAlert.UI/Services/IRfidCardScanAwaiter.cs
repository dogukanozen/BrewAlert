namespace BrewAlert.UI.Services;

/// <summary>
/// Implemented by ViewModels that are actively waiting for an RFID card scan.
/// Used by <c>MainView</c> so desktop keyboard-wedge fallback only intercepts
/// keystrokes while a card scan is expected.
/// </summary>
public interface IRfidCardScanAwaiter
{
    bool IsAwaitingCardScan { get; }
    bool CanAcceptKeyboardCardScan => IsAwaitingCardScan;
}
