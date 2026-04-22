using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Root ViewModel — subscribes to <see cref="INavigationService"/> for view changes.
/// No direct DI container access.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ViewModelBase _currentView = null!;

    [ObservableProperty]
    private string _title = "BrewAlert";

    public MainWindowViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.CurrentViewChanged += HandleNavigationViewChanged;

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

    /// <summary>
    /// Navigate to the timer view and start a brew session.
    /// Called from <see cref="ProfileListViewModel"/>.
    /// </summary>
    public void StartBrew(BrewProfile profile)
    {
        // Navigate to a fresh BrewTimerViewModel, then kick off the brew
        _navigation.NavigateTo<BrewTimerViewModel>();

        if (_navigation.CurrentView is BrewTimerViewModel timerVm)
        {
            timerVm.StartBrew(profile);
        }

        Title = $"{profile.Icon} {profile.Name}";
    }

    private void HandleNavigationViewChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    public void Dispose()
    {
        _navigation.CurrentViewChanged -= HandleNavigationViewChanged;
    }
}
