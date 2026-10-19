namespace BrewAlert.UI.Services;

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads the device's IPv4 addresses from the OS and the Wi-Fi name from the platform tools
/// that are actually present: nmcli (Raspberry Pi OS Bookworm), iwgetid or iw on Linux,
/// netsh on Windows. A missing tool is not an error; the next one is tried.
/// </summary>
public sealed class NetworkInfoService : INetworkInfoService, IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<NetworkInfoService> _logger;
    private readonly Func<string, string, CancellationToken, Task<string?>> _runCommand;

    public event EventHandler? Changed;

    public NetworkInfoService(ILogger<NetworkInfoService> logger)
        : this(logger, RunCommandAsync)
    {
    }

    internal NetworkInfoService(
        ILogger<NetworkInfoService> logger,
        Func<string, string, CancellationToken, Task<string?>> runCommand)
    {
        _logger = logger;
        _runCommand = runCommand;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public async Task<NetworkStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var interfaces = ReadInterfaces();
        if (interfaces.Count == 0) return NetworkStatus.Disconnected;

        var addresses = interfaces.SelectMany(i => i.Addresses).Distinct().ToList();
        var wireless = interfaces.FirstOrDefault(i => i.IsWireless);
        var wifiName = wireless is null ? null : await ReadWifiNameAsync(wireless.Name, ct);
        return new NetworkStatus(wifiName, wireless is not null, addresses);
    }

    private sealed record ActiveInterface(string Name, bool IsWireless, IReadOnlyList<string> Addresses);

    private List<ActiveInterface> ReadInterfaces()
    {
        NetworkInterface[] all;
        try
        {
            all = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException ex)
        {
            _logger.LogWarning(ex, "Could not enumerate network interfaces.");
            return [];
        }

        var candidates = new List<(ActiveInterface Interface, bool HasGateway)>();
        foreach (var ni in all)
        {
            if (ni.OperationalStatus != OperationalStatus.Up
                || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var properties = ni.GetIPProperties();
            var addresses = properties.UnicastAddresses
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !IsLinkLocal(a))
                .Select(a => a.ToString())
                .ToList();
            if (addresses.Count == 0) continue;

            var hasGateway = properties.GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            candidates.Add((new ActiveInterface(ni.Name, IsWireless(ni), addresses), hasGateway));
        }

        // Interfaces with a gateway are the ones reachable from the office LAN; this also drops
        // virtual adapters (Hyper-V, Docker) that only have a host-local address.
        var routed = candidates.Where(c => c.HasGateway).Select(c => c.Interface).ToList();
        return routed.Count > 0 ? routed : candidates.Select(c => c.Interface).ToList();
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsWireless(NetworkInterface ni) =>
        // .NET reports Linux Wi-Fi adapters as Ethernet; the kernel exposes a wireless directory instead.
        OperatingSystem.IsLinux()
            ? Directory.Exists($"/sys/class/net/{ni.Name}/wireless")
            : ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;

    private async Task<string?> ReadWifiNameAsync(string interfaceName, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
            return ParseNetshSsid(await _runCommand("netsh", "wlan show interfaces", ct));

        return ParseNmcliActiveSsid(await _runCommand("nmcli", $"-t -f ACTIVE,SSID device wifi list ifname {interfaceName} --rescan no", ct))
            ?? Clean(await _runCommand("iwgetid", $"-r {interfaceName}", ct))
            ?? ParseIwLinkSsid(await _runCommand("iw", $"dev {interfaceName} link", ct));
    }

    // nmcli terse output: one "ACTIVE:SSID" line per network, ':' and '\' escaped with '\'.
    internal static string? ParseNmcliActiveSsid(string? output)
    {
        if (output is null) return null;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (!trimmed.StartsWith("yes:", StringComparison.Ordinal)) continue;
            return Clean(trimmed["yes:".Length..].Replace("\\:", ":").Replace("\\\\", "\\"));
        }
        return null;
    }

    internal static string? ParseIwLinkSsid(string? output) => ParseValue(output, "SSID:");

    // "SSID : name" — BSSID lines start with "BSSID", so a trimmed "SSID" prefix is unambiguous.
    internal static string? ParseNetshSsid(string? output)
    {
        if (output is null) return null;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("SSID", StringComparison.Ordinal)) continue;
            var colon = trimmed.IndexOf(':');
            if (colon < 0 || trimmed[..colon].Trim() != "SSID") continue;
            return Clean(trimmed[(colon + 1)..]);
        }
        return null;
    }

    private static string? ParseValue(string? output, string key)
    {
        if (output is null) return null;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(key, StringComparison.Ordinal))
                return Clean(trimmed[key.Length..]);
        }
        return null;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static async Task<string?> RunCommandAsync(string fileName, string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Keep "yes"/"SSID" untranslated regardless of the Pi's locale.
        startInfo.Environment["LC_ALL"] = "C";

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            return null; // tool not installed
        }
        if (process is null) return null;

        using (process)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(CommandTimeout);
            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                return process.ExitCode == 0 ? output : null;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(); }
                catch (InvalidOperationException) { /* already exited */ }
                return null;
            }
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
}
