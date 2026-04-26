using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the profile selection screen.
/// Uses <see cref="INavigationService"/> to navigate — no service locator.
/// </summary>
public partial class ProfileListViewModel : ViewModelBase, IDisposable
{
    private readonly BrewProfileService _profileService;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _loadingText = string.Empty;

    public ObservableCollection<BrewProfile> Profiles { get; } = [];

    public ProfileListViewModel(
        BrewProfileService profileService,
        INavigationService navigation,
        ILocalizationService loc)
    {
        _profileService = profileService;
        _navigation = navigation;
        _loc = loc;

        _loc.LanguageChanged += OnLanguageChanged;
        RefreshLocalizedStrings();
        _ = LoadProfilesAsync();
    }

    private void RefreshLocalizedStrings()
    {
        PageTitle = _loc.Get("SelectYourBrew");
        LoadingText = _loc.Get("Loading");
    }

    private void OnLanguageChanged(string _) => RefreshLocalizedStrings();

    private async Task LoadProfilesAsync()
    {
        IsLoading = true;
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync();
            Profiles.Clear();
            foreach (var p in profiles)
                Profiles.Add(p);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectProfile(BrewProfile profile)
    {
        _navigation.NavigateTo<BrewTimerViewModel>();
        if (_navigation.CurrentView is BrewTimerViewModel timerVm)
            timerVm.StartBrew(profile);
    }

    [RelayCommand]
    private async Task RefreshProfiles()
    {
        await LoadProfilesAsync();
    }

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
