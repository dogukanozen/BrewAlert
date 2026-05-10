using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the profile selection screen.
/// Uses <see cref="INavigationService"/> to navigate — no service locator.
/// </summary>
public partial class ProfileListViewModel : ViewModelBase, IDisposable
{
    private const int RecentBrewLimit = 5;

    private readonly BrewProfileService _profileService;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly IBrewHistoryService _history;
    private readonly ILogger<ProfileListViewModel> _logger;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _loadingText = string.Empty;
    [ObservableProperty] private string _recentBrewsLabel = string.Empty;
    [ObservableProperty] private bool _hasRecentBrews;

    public ObservableCollection<BrewProfile> Profiles { get; } = [];
    public ObservableCollection<RecentBrewItem> RecentBrews { get; } = [];

    public ProfileListViewModel(
        BrewProfileService profileService,
        INavigationService navigation,
        ILocalizationService loc,
        IBrewHistoryService history,
        ILogger<ProfileListViewModel> logger)
    {
        _profileService = profileService;
        _navigation = navigation;
        _loc = loc;
        _history = history;
        _logger = logger;

        _loc.LanguageChanged += OnLanguageChanged;
        _history.HistoryUpdated += OnHistoryUpdated;
        RefreshLocalizedStrings();
        _ = LoadProfilesAsync();
        _ = LoadRecentBrewsAsync();
    }

    private void RefreshLocalizedStrings()
    {
        PageTitle = _loc.Get("SelectYourBrew");
        LoadingText = _loc.Get("Loading");
        RecentBrewsLabel = _loc.Get("RecentBrews");
    }

    private void OnLanguageChanged(string language)
    {
        RefreshLocalizedStrings();
        // Relative-time strings are language-dependent; rebuild the projection.
        _ = LoadRecentBrewsAsync();
    }

    private void OnHistoryUpdated(object? sender, BrewHistoryEntry entry)
    {
        Dispatcher.UIThread.Post(() => { _ = LoadRecentBrewsAsync(); });
    }

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

    private async Task LoadRecentBrewsAsync()
    {
        try
        {
            var entries = await _history.GetRecentAsync(RecentBrewLimit);
            var now = DateTime.UtcNow;
            RecentBrews.Clear();
            foreach (var entry in entries)
            {
                RecentBrews.Add(new RecentBrewItem(
                    Icon: entry.Icon,
                    Name: entry.ProfileName,
                    RelativeTime: FormatRelative(now - entry.CompletedAtUtc)));
            }
            HasRecentBrews = RecentBrews.Count > 0;
        }
        catch (Exception ex)
        {
            // Persistence failures (locked DB, corruption) shouldn't crash the home screen.
            _logger.LogError(ex, "Failed to load recent brew history.");
            HasRecentBrews = false;
        }
    }

    private string FormatRelative(TimeSpan delta)
    {
        if (delta < TimeSpan.FromMinutes(1)) return _loc.Get("JustNow");
        if (delta < TimeSpan.FromHours(1))
            return string.Format(CultureInfo.CurrentCulture, _loc.Get("MinutesAgo"), (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromDays(1))
            return string.Format(CultureInfo.CurrentCulture, _loc.Get("HoursAgo"), (int)delta.TotalHours);
        return string.Format(CultureInfo.CurrentCulture, _loc.Get("DaysAgo"), (int)delta.TotalDays);
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
        _history.HistoryUpdated -= OnHistoryUpdated;
    }
}
