using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class NetworkSettingsViewModelTests
{
    private readonly INetworkInfoService _network = Substitute.For<INetworkInfoService>();
    private readonly ILocalizationService _loc = Substitute.For<ILocalizationService>();

    public NetworkSettingsViewModelTests()
    {
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
    }

    private void Returns(NetworkStatus status) =>
        _network.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(status));

    [Fact]
    public void Wifi_ShowsSsidAndAddress()
    {
        Returns(new NetworkStatus("Office-WiFi", true, ["192.168.1.23"]));

        using var vm = new NetworkSettingsViewModel(_network, _loc);

        Assert.Equal("Office-WiFi", vm.NetworkText);
        Assert.Equal("IPv4: 192.168.1.23", vm.AddressText);
    }

    [Fact]
    public void WifiWithUnknownName_FallsBackToWifiLabel()
    {
        Returns(new NetworkStatus(null, true, ["10.0.0.5"]));

        using var vm = new NetworkSettingsViewModel(_network, _loc);

        Assert.Equal("NetworkWifi", vm.NetworkText);
    }

    [Fact]
    public void Wired_ShowsWiredLabelAndEveryAddress()
    {
        Returns(new NetworkStatus(null, false, ["192.168.1.23", "10.0.0.5"]));

        using var vm = new NetworkSettingsViewModel(_network, _loc);

        Assert.Equal("NetworkWired", vm.NetworkText);
        Assert.Equal("IPv4: 192.168.1.23, 10.0.0.5", vm.AddressText);
    }

    [Fact]
    public void Disconnected_ShowsNotConnectedWithoutAddress()
    {
        Returns(NetworkStatus.Disconnected);

        using var vm = new NetworkSettingsViewModel(_network, _loc);

        Assert.Equal("NetworkNotConnected", vm.NetworkText);
        Assert.Equal(string.Empty, vm.AddressText);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        Returns(NetworkStatus.Disconnected);
        var vm = new NetworkSettingsViewModel(_network, _loc);

        vm.Dispose();

        _network.Received(1).Changed -= Arg.Any<EventHandler>();
        _loc.Received(1).LanguageChanged -= Arg.Any<Action<string>>();
    }
}
