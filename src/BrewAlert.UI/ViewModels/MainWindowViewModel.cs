using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Root ViewModel — subscribes to <see cref="INavigationService"/> for view changes
/// and to <see cref="IBrewTimerService.BrewStarted"/> for window title updates.
/// No direct DI container access.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    // Sentinel values for the "current title key" tracker.
    // App name is intentionally not localized; brew-running shows a profile-specific
    // title that doesn't refresh on language change (user wouldn't switch languages
    // mid-brew, and the icon + name carry the meaning).
    private const string TitleKeyAppName = "__AppName";
    private const string TitleKeyBrewRunning = "__BrewRunning";

    private readonly INavigationService _navigation;
    private readonly IBrewTimerService _timerService;
    private readonly ILocalizationService _loc;

    private string _currentTitleKey = TitleKeyAppName;

    [ObservableProperty]
    private ViewModelBase _currentView = null!;

    [ObservableProperty]
    private string _title = "BrewAlert";

    [ObservableProperty]
    private string _brewsNavText = string.Empty;

    public MainWindowViewModel(
        INavigationService navigation,
        IBrewTimerService timerService,
        ILocalizationService loc)
    {
        _navigation = navigation;
        _timerService = timerService;
        _loc = loc;

        _navigation.CurrentViewChanged += HandleNavigationViewChanged;
        _timerService.BrewStarted += OnBrewStarted;
        _loc.LanguageChanged += OnLanguageChanged;

        BrewsNavText = _loc.Get("BrewsNavButton");

        // Start on the profile list screen
        _navigation.NavigateTo<ProfileListViewModel>();
    }

    [RelayCommand]
    private void NavigateToProfiles()
    {
        _navigation.NavigateTo<ProfileListViewModel>();
        _currentTitleKey = TitleKeyAppName;
        Title = "BrewAlert";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigation.NavigateTo<SettingsViewModel>();
        _currentTitleKey = "SettingsTitle";
        Title = _loc.Get("SettingsTitle");
    }

    private void OnBrewStarted(object? sender, BrewStartedEvent e)
    {
        _currentTitleKey = TitleKeyBrewRunning;
        Title = $"{e.Session.Profile.Icon} {e.Session.Profile.Name}";
    }

    private void HandleNavigationViewChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    private void OnLanguageChanged(string _)
    {
        BrewsNavText = _loc.Get("BrewsNavButton");
        if (_currentTitleKey != TitleKeyAppName && _currentTitleKey != TitleKeyBrewRunning)
        {
            Title = _loc.Get(_currentTitleKey);
        }
    }

    public void Dispose()
    {
        _navigation.CurrentViewChanged -= HandleNavigationViewChanged;
        _timerService.BrewStarted -= OnBrewStarted;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
