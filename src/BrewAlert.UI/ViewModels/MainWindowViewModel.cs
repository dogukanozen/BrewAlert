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
    private readonly INavigationService _navigation;
    private readonly IBrewTimerService _timerService;
    private readonly ILocalizationService _loc;

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
        Title = "BrewAlert";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigation.NavigateTo<SettingsViewModel>();
        Title = "Settings";
    }

    private void OnBrewStarted(object? sender, BrewStartedEvent e)
    {
        Title = $"{e.Session.Profile.Icon} {e.Session.Profile.Name}";
    }

    private void HandleNavigationViewChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    private void OnLanguageChanged(string _)
    {
        BrewsNavText = _loc.Get("BrewsNavButton");
    }

    public void Dispose()
    {
        _navigation.CurrentViewChanged -= HandleNavigationViewChanged;
        _timerService.BrewStarted -= OnBrewStarted;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
