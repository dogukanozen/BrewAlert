using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the profile selection screen.
/// Uses <see cref="INavigationService"/> to navigate — no service locator.
/// </summary>
public partial class ProfileListViewModel : ViewModelBase, IRfidCardScanAwaiter, IDisposable
{
    private const int RecentBrewLimit = 30;
    private static readonly TimeSpan RecentBrewRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CardScanPromptTimeout = TimeSpan.FromSeconds(30);

    private readonly BrewProfileService _profileService;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly IBrewHistoryService _history;
    private readonly IBrewTimerService _timerService;
    private readonly IRfidReaderService _rfidReader;
    private readonly RfidUserService _rfidUserService;
    private readonly IOptionsMonitor<RfidOptions> _rfidOptions;
    private readonly ILogger<ProfileListViewModel> _logger;
    private readonly DispatcherTimer _recentRefreshTimer;
    private readonly DispatcherTimer _scanTimeoutTimer;
    private readonly IDisposable? _rfidOptionsListener;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _isLoadingRecentBrews;
    private bool _reloadRecentBrewsRequested;
    private int _scanGeneration;
    private bool _disposed;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _profilesErrorText = string.Empty;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _loadingText = string.Empty;
    [ObservableProperty] private string _recentBrewsLabel = string.Empty;
    [ObservableProperty] private bool _hasRecentBrews;
    [ObservableProperty] private int _recentBrewsCount;

    [ObservableProperty] private BrewProfile? _pendingProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardScanStep))]
    private bool _isAwaitingCardScan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardScanStep))]
    private bool _isChoosingBrewer;

    [ObservableProperty] private bool _hasAvailableBrewers;
    [ObservableProperty] private string _scanErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnregisteredCard))]
    private string? _unregisteredCardId;

    public bool IsCardScanStep => IsAwaitingCardScan && !IsChoosingBrewer;
    public bool HasUnregisteredCard => !string.IsNullOrEmpty(UnregisteredCardId);
    public bool IsCardRequired => _rfidOptions.CurrentValue.RequireCardToBrew;

    bool IRfidCardScanAwaiter.CanAcceptKeyboardCardScan =>
        !_disposed && (IsAwaitingCardScan || _rfidOptions.CurrentValue.RequireCardToBrew);

    public ObservableCollection<BrewProfile> Profiles { get; } = [];
    public ObservableCollection<RecentBrewItem> RecentBrews { get; } = [];
    public ObservableCollection<RfidUser> AvailableBrewers { get; } = [];
    public string HomeTitle => _loc.Get("HomeTitle");
    public string HomeEyebrow => _loc.Get("HomeEyebrow");
    public string HomeSubtitle => _loc.Get("HomeSubtitle");
    public string OperatorLoginText => _loc.Get("OperatorLoginButton");
    public string StartText => _loc.Get("BrewStart");
    public string ScanPromptTitle => _loc.Get("RfidScanPromptTitle");
    public string ScanPromptSubtitle => _loc.Get("RfidScanPromptSubtitle");
    public string CancelText => _loc.Get("CancelButton");
    public string RegisterThisCardText => _loc.Get("RfidRegisterThisCard");
    public string BrewWithoutCardText => _loc.Get("BrewWithoutCard");
    public string WhoIsBrewingTitle => _loc.Get("WhoIsBrewingTitle");
    public string WhoIsBrewingSubtitle => _loc.Get("WhoIsBrewingSubtitle");
    public string BackToCardScanText => _loc.Get("BackToCardScan");
    public string NoOperatorsText => _loc.Get("NoOperatorsToPick");
    public string AddOperatorQuickText => _loc.Get("AddOperatorQuick");
    public string HistoryToggleText => _loc.Get(ShowAllHistory ? "ShowLessHistory" : "ShowMoreHistory");
    public bool CanExpandHistory => RecentBrews.Count > 3;
    public IEnumerable<RecentBrewItem> DisplayedRecentBrews => ShowAllHistory ? RecentBrews : RecentBrews.Take(3);
    [ObservableProperty] private bool _showAllHistory;
    partial void OnShowAllHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayedRecentBrews));
        OnPropertyChanged(nameof(HistoryToggleText));
    }
    [RelayCommand] private void ToggleHistory() => ShowAllHistory = !ShowAllHistory;

    public ProfileListViewModel(
        BrewProfileService profileService,
        INavigationService navigation,
        ILocalizationService loc,
        IBrewHistoryService history,
        IBrewTimerService timerService,
        IRfidReaderService rfidReader,
        RfidUserService rfidUserService,
        IOptionsMonitor<RfidOptions> rfidOptions,
        ILogger<ProfileListViewModel> logger)
    {
        _profileService = profileService;
        _navigation = navigation;
        _loc = loc;
        _history = history;
        _timerService = timerService;
        _rfidReader = rfidReader;
        _rfidUserService = rfidUserService;
        _rfidOptions = rfidOptions;
        _logger = logger;

        _loc.LanguageChanged += OnLanguageChanged;
        _history.HistoryUpdated += OnHistoryUpdated;
        _rfidReader.CardScanned += OnCardScanned;
        _rfidOptionsListener = _rfidOptions.OnChange(_ => Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                OnPropertyChanged(nameof(IsCardRequired));
        }));
        RefreshLocalizedStrings();

        _recentRefreshTimer = new DispatcherTimer { Interval = RecentBrewRefreshInterval };
        _recentRefreshTimer.Tick += OnRecentRefreshTimerTick;
        _recentRefreshTimer.Start();

        _scanTimeoutTimer = new DispatcherTimer { Interval = CardScanPromptTimeout };
        _scanTimeoutTimer.Tick += OnScanTimeoutTick;

        _ = LoadProfilesAsync();
        _ = LoadRecentBrewsAsync();
    }

    private void RefreshLocalizedStrings()
    {
        foreach (var name in new[]
        {
            nameof(HomeTitle), nameof(HomeEyebrow), nameof(HomeSubtitle), nameof(OperatorLoginText), nameof(StartText),
            nameof(HistoryToggleText), nameof(ScanPromptTitle), nameof(ScanPromptSubtitle),
            nameof(CancelText), nameof(RegisterThisCardText), nameof(BrewWithoutCardText),
            nameof(WhoIsBrewingTitle), nameof(WhoIsBrewingSubtitle), nameof(BackToCardScanText),
            nameof(NoOperatorsText), nameof(AddOperatorQuickText)
        })
        {
            OnPropertyChanged(name);
        }
        PageTitle = _loc.Get("SelectYourBrew");
        LoadingText = _loc.Get("Loading");
        RecentBrewsLabel = _loc.Get("RecentBrews");
        if (HasUnregisteredCard)
            ScanErrorMessage = string.Format(CultureInfo.CurrentCulture, _loc.Get("RfidUnknownCard"), UnregisteredCardId);
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

    private void OnScanTimeoutTick(object? sender, EventArgs e)
    {
        CancelCardScan();
    }

    private void OnCardScanned(object? sender, RfidCardScannedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                _ = HandleCardScannedAsync(e.CardId);
        });
    }

    private async Task HandleCardScannedAsync(string cardId)
    {
        if (_disposed) return;
        if (!IsAwaitingCardScan && !_rfidOptions.CurrentValue.RequireCardToBrew) return;

        var normalized = RfidUserService.NormalizeCardId(cardId);
        if (normalized is null) return;

        var generation = _scanGeneration;
        try
        {
            var user = await _rfidUserService.FindByCardIdAsync(normalized, _disposeCts.Token);
            if (_disposed || generation != _scanGeneration) return;
            if (!IsAwaitingCardScan && !_rfidOptions.CurrentValue.RequireCardToBrew) return;

            if (user is not null)
            {
                var pending = PendingProfile;
                ClearScanPrompt();
                if (pending is not null && user.HasConfiguredProfiles)
                {
                    var personalized = RfidUserService.CreatePersonalizedProfile(pending, user);
                    _timerService.Start(personalized, user.Name, user.CardId);
                    _navigation.NavigateTo<ActiveBrewsViewModel>();
                }
                else
                {
                    _navigation.NavigateTo<OperatorBrewViewModel>(vm => vm.Initialize(user, pending));
                }
            }
            else
            {
                IsChoosingBrewer = false;
                IsAwaitingCardScan = true;
                RestartScanTimeout();
                UnregisteredCardId = normalized;
                ScanErrorMessage = string.Format(CultureInfo.CurrentCulture, _loc.Get("RfidUnknownCard"), normalized);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to look up RFID card {CardId}.", normalized);
        }
    }

    private async Task LoadProfilesAsync()
    {
        IsLoading = true;
        ProfilesErrorText = string.Empty;
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync();
            Profiles.Clear();
            foreach (var p in profiles)
                Profiles.Add(p);
        }
        catch (Exception ex)
        {
            // An unreadable profiles file used to leave the home screen silently empty.
            _logger.LogError(ex, "Failed to load brew profiles.");
            ProfilesErrorText = string.Format(CultureInfo.CurrentCulture, _loc.Get("ProfilesLoadError"), ex.Message);
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
                var todayUtcStart = DateTime.Today.ToUniversalTime();
                var todayUtcEnd = todayUtcStart.AddDays(1);
                RecentBrews.Clear();
                foreach (var entry in entries)
                {
                    if (entry.CompletedAtUtc < todayUtcStart || entry.CompletedAtUtc >= todayUtcEnd) continue;
                    RecentBrews.Add(new RecentBrewItem(
                        Icon: entry.Icon,
                        Name: entry.ProfileName,
                        RelativeTime: FormatRelative(now - entry.CompletedAtUtc),
                        DurationText: FormatDuration(entry.DurationSeconds),
                        BrewerName: entry.BrewerName));
                }
                RecentBrewsCount = RecentBrews.Count;
                HasRecentBrews = RecentBrews.Count > 0;
                OnPropertyChanged(nameof(DisplayedRecentBrews));
                OnPropertyChanged(nameof(CanExpandHistory));
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

    private string FormatDuration(int durationSeconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(TimeSpan.FromSeconds(durationSeconds).TotalMinutes));
        return string.Format(CultureInfo.CurrentCulture, "{0} {1}", minutes, _loc.Get("MinShort"));
    }

    [RelayCommand]
    private void SelectProfile(BrewProfile profile)
    {
        if (!_rfidOptions.CurrentValue.RequireCardToBrew)
        {
            // Start the brew first so the ActiveBrews VM picks it up via GetActiveSessions()
            // when it constructs, regardless of whether BrewStarted fires before/after the nav.
            _timerService.Start(profile);
            _navigation.NavigateTo<ActiveBrewsViewModel>();
            return;
        }

        _scanGeneration++;
        PendingProfile = profile;
        ScanErrorMessage = string.Empty;
        UnregisteredCardId = null;
        IsChoosingBrewer = false;
        IsAwaitingCardScan = true;
        RestartScanTimeout();
    }

    [RelayCommand]
    private async Task OpenOperatorSignInAsync()
    {
        if (_disposed || !_rfidOptions.CurrentValue.RequireCardToBrew) return;

        _scanGeneration++;
        PendingProfile = null;
        ScanErrorMessage = string.Empty;
        UnregisteredCardId = null;
        IsAwaitingCardScan = true;
        await ChooseBrewerWithoutCardAsync();
    }

    [RelayCommand]
    private async Task ChooseBrewerWithoutCardAsync()
    {
        if (_disposed || !IsAwaitingCardScan) return;

        try
        {
            var users = await _rfidUserService.GetAllAsync(_disposeCts.Token);
            if (_disposed || !IsAwaitingCardScan) return;

            AvailableBrewers.Clear();
            foreach (var user in users)
                AvailableBrewers.Add(user);
            HasAvailableBrewers = AvailableBrewers.Count > 0;
            IsChoosingBrewer = true;
            RestartScanTimeout();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load operators for cardless brew selection.");
        }
    }

    [RelayCommand]
    private void BackToCardScan()
    {
        if (!IsAwaitingCardScan) return;
        IsChoosingBrewer = false;
        RestartScanTimeout();
    }

    [RelayCommand]
    private void SelectBrewer(RfidUser user)
    {
        if (user is null) return;

        var pending = PendingProfile;
        ClearScanPrompt();
        if (pending is not null && user.HasConfiguredProfiles)
        {
            var personalized = RfidUserService.CreatePersonalizedProfile(pending, user);
            _timerService.Start(personalized, user.Name, user.CardId);
            _navigation.NavigateTo<ActiveBrewsViewModel>();
        }
        else
        {
            _navigation.NavigateTo<OperatorBrewViewModel>(vm => vm.Initialize(user, pending));
        }
    }

    [RelayCommand]
    private void OpenAddOperator()
    {
        ClearScanPrompt();
        _navigation.NavigateTo<RfidUsersViewModel>(vm => vm.StartCardlessRegistration());
    }

    [RelayCommand]
    private void CancelCardScan() => ClearScanPrompt();

    [RelayCommand]
    private void RegisterUnregisteredCard()
    {
        var cardId = UnregisteredCardId;
        if (string.IsNullOrEmpty(cardId)) return;

        ClearScanPrompt();
        _navigation.NavigateTo<RfidUsersViewModel>(vm => vm.StartRegistrationWithCard(cardId));
    }

    private void ClearScanPrompt()
    {
        _scanGeneration++;
        _scanTimeoutTimer.Stop();
        IsAwaitingCardScan = false;
        IsChoosingBrewer = false;
        PendingProfile = null;
        ScanErrorMessage = string.Empty;
        UnregisteredCardId = null;
        AvailableBrewers.Clear();
        HasAvailableBrewers = false;
    }

    private void RestartScanTimeout()
    {
        _scanTimeoutTimer.Stop();
        _scanTimeoutTimer.Start();
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
        _scanTimeoutTimer.Stop();
        _scanTimeoutTimer.Tick -= OnScanTimeoutTick;
        _loc.LanguageChanged -= OnLanguageChanged;
        _history.HistoryUpdated -= OnHistoryUpdated;
        _rfidReader.CardScanned -= OnCardScanned;
        _rfidOptionsListener?.Dispose();
    }
}
