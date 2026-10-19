using Avalonia.Threading;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Settings block showing which network the device is on and its IPv4 address,
/// so the Pi can be found for SSH without a keyboard or monitor.
/// </summary>
public partial class NetworkSettingsViewModel : ObservableObject, IDisposable
{
    private readonly INetworkInfoService _network;
    private readonly ILocalizationService _loc;
    private readonly CancellationTokenSource _disposeCts = new();
    private NetworkStatus? _status;
    private bool _disposed;

    [ObservableProperty] private string _networkText = string.Empty;
    [ObservableProperty] private string _addressText = string.Empty;

    public string Title => _loc.Get("NetworkTitle");

    public NetworkSettingsViewModel(INetworkInfoService network, ILocalizationService loc)
    {
        _network = network;
        _loc = loc;
        _network.Changed += OnNetworkChanged;
        _loc.LanguageChanged += OnLanguageChanged;
        Render();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _status = await _network.GetStatusAsync(_disposeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!_disposed) Render();
    }

    private void Render()
    {
        OnPropertyChanged(nameof(Title));
        if (_status is null)
        {
            NetworkText = _loc.Get("Loading");
            AddressText = string.Empty;
        }
        else if (!_status.IsConnected)
        {
            NetworkText = _loc.Get("NetworkNotConnected");
            AddressText = string.Empty;
        }
        else
        {
            NetworkText = _status.WifiName ?? _loc.Get(_status.IsWireless ? "NetworkWifi" : "NetworkWired");
            AddressText = "IPv4: " + string.Join(", ", _status.IPv4Addresses);
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) _ = RefreshAsync();
        });

    private void OnLanguageChanged(string _) => Render();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _network.Changed -= OnNetworkChanged;
        _loc.LanguageChanged -= OnLanguageChanged;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
