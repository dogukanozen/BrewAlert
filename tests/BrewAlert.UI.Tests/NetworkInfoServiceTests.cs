using BrewAlert.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.UI.Tests;

public class NetworkInfoServiceTests
{
    [Fact]
    public void Nmcli_ReturnsTheActiveNetwork()
    {
        const string output = "no:Guest\nyes:Office-WiFi\nno:Printer\n";

        Assert.Equal("Office-WiFi", NetworkInfoService.ParseNmcliActiveSsid(output));
    }

    [Fact]
    public void Nmcli_UnescapesColonsAndBackslashes()
    {
        const string output = @"yes:Cafe\:2nd\\floor";

        Assert.Equal(@"Cafe:2nd\floor", NetworkInfoService.ParseNmcliActiveSsid(output));
    }

    [Fact]
    public void Nmcli_WhenNothingActive_ReturnsNull()
    {
        Assert.Null(NetworkInfoService.ParseNmcliActiveSsid("no:Guest\n"));
        Assert.Null(NetworkInfoService.ParseNmcliActiveSsid(null));
    }

    [Fact]
    public void Iw_ReturnsTheLinkedSsid()
    {
        const string output = "Connected to aa:bb:cc:dd:ee:ff (on wlan0)\n\tSSID: Office WiFi\n\tfreq: 2437\n";

        Assert.Equal("Office WiFi", NetworkInfoService.ParseIwLinkSsid(output));
    }

    [Fact]
    public void Iw_WhenNotConnected_ReturnsNull()
    {
        Assert.Null(NetworkInfoService.ParseIwLinkSsid("Not connected.\n"));
    }

    [Fact]
    public void Netsh_ReadsSsidAndIgnoresBssid()
    {
        const string output = """

            There is 1 interface on the system:

                Name                   : Wi-Fi
                State                  : connected
                SSID                   : Office-WiFi
                BSSID                  : aa:bb:cc:dd:ee:ff
            """;

        Assert.Equal("Office-WiFi", NetworkInfoService.ParseNetshSsid(output));
    }

    [Fact]
    public async Task GetStatus_WhenNoWifiToolIsInstalled_StillReturnsAddresses()
    {
        using var sut = new NetworkInfoService(NullLogger<NetworkInfoService>.Instance, (_, _, _) => Task.FromResult<string?>(null));

        var status = await sut.GetStatusAsync();

        Assert.All(status.IPv4Addresses, address => Assert.Equal(4, address.Split('.').Length));
        Assert.Null(status.WifiName);
    }
}
