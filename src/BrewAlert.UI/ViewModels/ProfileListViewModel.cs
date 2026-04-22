using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the profile selection screen.
/// Uses <see cref="INavigationService"/> to navigate — no service locator.
/// </summary>
public partial class ProfileListViewModel : ViewModelBase
{
    private readonly BrewProfileService _profileService;
    private readonly INavigationService _navigation;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<BrewProfile> Profiles { get; } = [];

    public ProfileListViewModel(BrewProfileService profileService, INavigationService navigation)
    {
        _profileService = profileService;
        _navigation = navigation;
        _ = LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        IsLoading = true;
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync();
            Profiles.Clear();
            foreach (var p in profiles)
            {
                Profiles.Add(p);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectProfile(BrewProfile profile)
    {
        // Navigate up to MainWindowViewModel which orchestrates the brew start
        if (_navigation.CurrentView is ProfileListViewModel)
        {
            // Use the navigation service to get to timer, and let the parent handle StartBrew
            _navigation.NavigateTo<BrewTimerViewModel>();

            if (_navigation.CurrentView is BrewTimerViewModel timerVm)
            {
                timerVm.StartBrew(profile);
            }
        }
    }

    [RelayCommand]
    private async Task RefreshProfiles()
    {
        await LoadProfilesAsync();
    }
}
