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
    private const int RecentBrewLimit = 30;
    private static readonly TimeSpan RecentBrewRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly BrewProfileService _profileService;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly IBrewHistoryService _history;
    private readonly ILogger<ProfileListViewModel> _logger;
    private readonly DispatcherTimer _recentRefreshTimer;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoadingRecentBrews;
    private bool _reloadRecentBrewsRequested;
    private bool _disposed;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _loadingText = string.Empty;
    [ObservableProperty] private string _recentBrewsLabel = string.Empty;
    [ObservableProperty] private bool _hasRecentBrews;
    [ObservableProperty] private int _recentBrewsCount;

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

        _recentRefreshTimer = new DispatcherTimer { Interval = RecentBrewRefreshInterval };
        _recentRefreshTimer.Tick += OnRecentRefreshTimerTick;
        _recentRefreshTimer.Start();

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
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                _ = LoadRecentBrewsAsync();
        });
    }

    private void OnRecentRefreshTimerTick(object? sender, EventArgs e)
    {
        if (!_disposed)
            _ = LoadRecentBrewsAsync();
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
        if (_disposed) return;

        if (_isLoadingRecentBrews)
        {
            _reloadRecentBrewsRequested = true;
            return;
        }

        _isLoadingRecentBrews = true;
        try
        {
            do
            {
                _reloadRecentBrewsRequested = false;
                var entries = await _history.GetRecentAsync(RecentBrewLimit, _disposeCts.Token);
                if (_disposed) return;

                var now = DateTime.UtcNow;
                RecentBrews.Clear();
                foreach (var entry in entries)
                {
                    RecentBrews.Add(new RecentBrewItem(
                        Icon: entry.Icon,
                        Name: entry.ProfileName,
                        RelativeTime: FormatRelative(now - entry.CompletedAtUtc),
                        DurationText: FormatDuration(entry.DurationSeconds)));
                }
#if DEBUG
                RecentBrews.Clear();
                foreach (var mock in BuildMockRecentBrews(30, now))
                    RecentBrews.Add(mock);
#endif
                RecentBrewsCount = RecentBrews.Count;
                HasRecentBrews = RecentBrews.Count > 0;
            }
            while (_reloadRecentBrewsRequested && !_disposed);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // Persistence failures (locked DB, corruption) shouldn't crash the home screen.
            _logger.LogError(ex, "Failed to load recent brew history.");
            HasRecentBrews = false;
        }
        finally
        {
            _isLoadingRecentBrews = false;
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

#if DEBUG
    private IEnumerable<RecentBrewItem> BuildMockRecentBrews(int count, DateTime now)
    {
        var samples = new (string Icon, string Name)[]
        {
            ("♨",  "Çay"),
            ("☕", "Filtre Kahve"),
            ("☕", "Türk Kahvesi"),
            ("☕", "Espresso"),
            ("🍵", "Yeşil Çay"),
            ("🌿", "Bitki Çayı"),
            ("🥛", "Latte"),
            ("☕", "Americano"),
            ("☕", "Cappuccino"),
            ("🍯", "Ihlamur"),
        };
        var rng = new Random(42);
        var cumulativeMinutes = 0;
        for (var i = 0; i < count; i++)
        {
            var sample = samples[rng.Next(samples.Length)];
            cumulativeMinutes += rng.Next(7, 240);
            var completedAt = now - TimeSpan.FromMinutes(cumulativeMinutes);
            var duration = rng.Next(60, 1200);
            yield return new RecentBrewItem(
                Icon: sample.Icon,
                Name: sample.Name,
                RelativeTime: FormatRelative(now - completedAt),
                DurationText: FormatDuration(duration));
        }
    }
#endif

    private string FormatDuration(int durationSeconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(TimeSpan.FromSeconds(durationSeconds).TotalMinutes));
        return string.Format(CultureInfo.CurrentCulture, "{0} {1}", minutes, _loc.Get("MinShort"));
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
        if (_disposed) return;
        _disposed = true;

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _recentRefreshTimer.Stop();
        _recentRefreshTimer.Tick -= OnRecentRefreshTimerTick;
        _loc.LanguageChanged -= OnLanguageChanged;
        _history.HistoryUpdated -= OnHistoryUpdated;
    }
}
