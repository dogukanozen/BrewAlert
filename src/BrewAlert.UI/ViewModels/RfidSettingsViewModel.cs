using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Settings section for RFID reader status, card-required toggle, and user registration navigation.
/// </summary>
public partial class RfidSettingsViewModel : ObservableObject, IDisposable
{
    private readonly RfidUserService _userService;
    private readonly IRfidReaderService _rfidReader;
    private readonly IPreferencesService _preferences;
    private readonly IConfigurationRoot _configuration;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly CancellationTokenSource _disposeCts = new();
    private int _userCount;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequireCardDisabled))]
    private bool _requireCardToBrew;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _userCountText = string.Empty;

    public bool RequireCardDisabled => !RequireCardToBrew;
    public string Title => _loc.Get("RfidSettingsTitle");
    public string RequireCardLabel => _loc.Get("RfidRequireCard");
    public string ManageUsersText => _loc.Get("RfidManageUsers");
    public string OnText => _loc.Get("RfidEnabled");
    public string OffText => _loc.Get("RfidDisabled");
    public string ReaderStatusText => _loc.Get(
        _rfidReader.IsHardwareReaderConnected ? "RfidReaderConnected" : "RfidReaderKeyboardFallback");

    public RfidSettingsViewModel(
        RfidUserService userService,
        IRfidReaderService rfidReader,
        IOptionsMonitor<RfidOptions> rfidOptions,
        IPreferencesService preferences,
        IConfigurationRoot configuration,
        INavigationService navigation,
        ILocalizationService loc)
    {
        _userService = userService;
        _rfidReader = rfidReader;
        _preferences = preferences;
        _configuration = configuration;
        _navigation = navigation;
        _loc = loc;

        _requireCardToBrew = rfidOptions.CurrentValue.RequireCardToBrew;
        _loc.LanguageChanged += OnLanguageChanged;
        RefreshLabels();
        _ = LoadUserCountAsync();
    }

    private async Task LoadUserCountAsync()
    {
        try
        {
            var users = await _userService.GetAllAsync(_disposeCts.Token);
            if (_disposed) return;
            _userCount = users.Count;
            UpdateUserCountText();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!_disposed)
            {
                _userCount = 0;
                UpdateUserCountText();
            }
        }
    }

    private void UpdateUserCountText() =>
        UserCountText = string.Format(_loc.CurrentCulture, _loc.Get("RfidUserCount"), _userCount);

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RequireCardLabel));
        OnPropertyChanged(nameof(ManageUsersText));
        OnPropertyChanged(nameof(OnText));
        OnPropertyChanged(nameof(OffText));
        OnPropertyChanged(nameof(ReaderStatusText));
        UpdateUserCountText();
    }

    private void OnLanguageChanged(string _) => RefreshLabels();

    [RelayCommand]
    private Task EnableRequireCard() => SetRequireCardAsync(true);

    [RelayCommand]
    private Task DisableRequireCard() => SetRequireCardAsync(false);

    private async Task SetRequireCardAsync(bool required)
    {
        if (RequireCardToBrew == required) return;
        IsBusy = true;
        try
        {
            await _preferences.SaveRequireCardToBrewAsync(required, _disposeCts.Token);
            _configuration.Reload();
            RequireCardToBrew = required;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenRfidUsers() => _navigation.NavigateTo<RfidUsersViewModel>();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loc.LanguageChanged -= OnLanguageChanged;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
