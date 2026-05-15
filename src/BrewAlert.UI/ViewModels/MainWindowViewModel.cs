using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Root ViewModel — subscribes to <see cref="INavigationService"/> for view changes
/// and to <see cref="IBrewTimerService"/> events for window title updates.
/// No direct DI container access.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const string TitleKeyAppName = "__AppName";
    private const string TitleKeyBrewRunning = "__BrewRunning";

    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(1);

    private readonly INavigationService _navigation;
    private readonly IBrewTimerService _timerService;
    private readonly ILocalizationService _loc;
    private readonly IUpdateService _updateService;
    private readonly CancellationTokenSource _updateCts = new();
    private readonly DispatcherTimer _clockTimer;

    private string _currentTitleKey = TitleKeyAppName;

    [ObservableProperty]
    private ViewModelBase _currentView = null!;

    [ObservableProperty]
    private string _title = "BrewAlert";

    [ObservableProperty]
    private string _brewsNavText = string.Empty;

    [ObservableProperty]
    private bool _isUpdateToastVisible;

    [ObservableProperty]
    private string _updateToastMessage = string.Empty;

    [ObservableProperty]
    private string _updateToastInstallText = string.Empty;

    [ObservableProperty]
    private string _updateToastDismissText = string.Empty;

    [ObservableProperty]
    private string _currentDateTime = string.Empty;

    public MainWindowViewModel(
        INavigationService navigation,
        IBrewTimerService timerService,
        ILocalizationService loc,
        IUpdateService updateService)
    {
        _navigation = navigation;
        _timerService = timerService;
        _loc = loc;
        _updateService = updateService;

        _navigation.CurrentViewChanged += HandleNavigationViewChanged;
        _timerService.BrewStarted += OnBrewStarted;
        _timerService.BrewCompleted += OnBrewCompleted;
        _timerService.BrewCancelled += OnBrewCancelled;
        _loc.LanguageChanged += OnLanguageChanged;
        _updateService.UpdateAvailable += OnUpdateAvailable;

        BrewsNavText = _loc.Get("BrewsNavButton");

        UpdateClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        // Start on the profile list screen
        _navigation.NavigateTo<ProfileListViewModel>();

        _ = RunPeriodicUpdateCheckAsync(_updateCts.Token);
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        try
        {
            await _updateService.DownloadAndInstallUpdatesAsync();
            IsUpdateToastVisible = false;
        }
        catch (Exception)
        {
            UpdateToastMessage = _loc.Get("UpdateError");
        }
    }

    [RelayCommand]
    private void DismissUpdate() => IsUpdateToastVisible = false;

    private async Task RunPeriodicUpdateCheckAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(UpdateCheckInterval);
            do
            {
                await _updateService.CheckForUpdatesAsync();
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private void OnUpdateAvailable()
    {
        if (IsUpdateToastVisible) return;
        if (Dispatcher.UIThread.CheckAccess())
            ShowUpdateToast();
        else
            Dispatcher.UIThread.Post(ShowUpdateToast, DispatcherPriority.Normal);
    }

    private void ShowUpdateToast()
    {
        UpdateToastMessage = _loc.Get("UpdateAvailable");
        UpdateToastInstallText = _loc.Get("InstallUpdate");
        UpdateToastDismissText = _loc.Get("UpdateDismiss");
        IsUpdateToastVisible = true;
    }

    [RelayCommand]
    private void NavigateToProfiles()
    {
        var active = _timerService.GetActiveSessions();
        if (active.Count > 0)
        {
            _navigation.NavigateTo<ActiveBrewsViewModel>();
            _currentTitleKey = TitleKeyBrewRunning;
            Title = ComposeBrewTitle(active);
        }
        else
        {
            _navigation.NavigateTo<ProfileListViewModel>();
            _currentTitleKey = TitleKeyAppName;
            Title = "BrewAlert";
        }
    }

    private string ComposeBrewTitle(IReadOnlyList<BrewSession> sessions)
    {
        if (sessions.Count == 1)
            return $"{sessions[0].Profile.Icon} {sessions[0].Profile.Name}";
        return string.Format(_loc.Get("ActiveBrewsCount"), sessions.Count);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigation.NavigateTo<SettingsViewModel>();
        _currentTitleKey = "SettingsTitle";
        Title = _loc.Get("SettingsTitle");
    }

    // Brew events arrive from BrewTimerService.RunTimerLoopAsync — a background task.
    // Marshal title-property updates to the UI thread.
    private void OnBrewStarted(object? sender, BrewStartedEvent e) => RefreshBrewTitleOnUi();

    private void OnBrewCompleted(object? sender, BrewCompletedEvent e) => RefreshBrewTitleOnUi();

    private void OnBrewCancelled(object? sender, BrewCancelledEvent e) => RefreshBrewTitleOnUi();

    private void RefreshBrewTitleOnUi()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshBrewTitle();
        else
            Dispatcher.UIThread.Post(RefreshBrewTitle);
    }

    private void RefreshBrewTitle()
    {
        // Brew events must not clobber non-brew page titles (Settings, etc.) — only refresh
        // when the user is currently in a brew-related context.
        if (_currentTitleKey != TitleKeyAppName && _currentTitleKey != TitleKeyBrewRunning)
            return;

        var active = _timerService.GetActiveSessions();
        if (active.Count == 0)
        {
            _currentTitleKey = TitleKeyAppName;
            Title = "BrewAlert";
            return;
        }
        _currentTitleKey = TitleKeyBrewRunning;
        Title = ComposeBrewTitle(active);
    }

    private void HandleNavigationViewChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    private void OnLanguageChanged(string _)
    {
        BrewsNavText = _loc.Get("BrewsNavButton");
        if (_currentTitleKey == TitleKeyBrewRunning)
        {
            // The 2+-active title is the localized "{0} active brews" — relocalize it.
            var active = _timerService.GetActiveSessions();
            if (active.Count > 0) Title = ComposeBrewTitle(active);
        }
        else if (_currentTitleKey != TitleKeyAppName)
        {
            Title = _loc.Get(_currentTitleKey);
        }
        if (IsUpdateToastVisible)
            ShowUpdateToast();
    }

    private void UpdateClock() =>
        CurrentDateTime = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");

    public void Dispose()
    {
        _clockTimer.Stop();
        _updateCts.Cancel();
        _updateCts.Dispose();
        _navigation.CurrentViewChanged -= HandleNavigationViewChanged;
        _timerService.BrewStarted -= OnBrewStarted;
        _timerService.BrewCompleted -= OnBrewCompleted;
        _timerService.BrewCancelled -= OnBrewCancelled;
        _loc.LanguageChanged -= OnLanguageChanged;
        _updateService.UpdateAvailable -= OnUpdateAvailable;
    }
}
