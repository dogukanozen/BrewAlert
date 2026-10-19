namespace BrewAlert.UI.Services;

/// <summary>Current network of the device, shown in Settings so the Pi can be reached over SSH.</summary>
/// <param name="WifiName">SSID of the connected Wi-Fi, or null when wired or unknown.</param>
/// <param name="IsWireless">True when the active connection is Wi-Fi.</param>
/// <param name="IPv4Addresses">Addresses on interfaces that have a default gateway.</param>
public sealed record NetworkStatus(string? WifiName, bool IsWireless, IReadOnlyList<string> IPv4Addresses)
{
    public static NetworkStatus Disconnected { get; } = new(null, false, []);

    public bool IsConnected => IPv4Addresses.Count > 0;
}

public interface INetworkInfoService
{
    Task<NetworkStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Raised on a background thread when an address changes (Wi-Fi reconnect, DHCP renew, cable).</summary>
    event EventHandler? Changed;
}
