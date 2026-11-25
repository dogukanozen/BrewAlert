namespace BrewAlert.Infrastructure.Configuration;

/// <summary>
/// Configuration options for RFID reader hardware and card-required brewing.
/// Bound from <c>BrewAlert:Rfid</c>.
/// </summary>
public sealed class RfidOptions
{
    public const string SectionPath = "BrewAlert:Rfid";

    /// <summary>Optional explicit Linux evdev path (e.g. /dev/input/event3). Empty = auto-detect.</summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>When true, tapping a brew profile prompts the user to scan their RFID card first.</summary>
    public bool RequireCardToBrew { get; set; } = true;
}
