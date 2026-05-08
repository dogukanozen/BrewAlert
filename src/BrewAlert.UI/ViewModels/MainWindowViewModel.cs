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
            using var timer = new PeriodicTimer(TimeSpan.FromHours(2));
            do
            {
                var hasUpdate = await _updateService.CheckForUpdatesAsync();
                if (hasUpdate && !IsUpdateToastVisible)
                    await Dispatcher.UIThread.InvokeAsync(ShowUpdateToast, DispatcherPriority.Normal, ct);
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
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
        var activeSession = _timerService.GetActiveSession();
        if (activeSession is { State: BrewSessionState.Running or BrewSessionState.Paused })
        {
            _navigation.NavigateTo<BrewTimerViewModel>();
            if (_navigation.CurrentView is BrewTimerViewModel timerVm)
                timerVm.AttachToSession(activeSession);
            _currentTitleKey = TitleKeyBrewRunning;
            Title = $"{activeSession.Profile.Icon} {activeSession.Profile.Name}";
        }
        else
        {
            _navigation.NavigateTo<ProfileListViewModel>();
            _currentTitleKey = TitleKeyAppName;
            Title = "BrewAlert";
        }
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

    private void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        if (_currentTitleKey == TitleKeyBrewRunning)
        {
            _currentTitleKey = TitleKeyAppName;
            Title = "BrewAlert";
        }
    }

    private void OnBrewCancelled(object? sender, BrewCancelledEvent e)
    {
        if (_currentTitleKey == TitleKeyBrewRunning)
        {
            _currentTitleKey = TitleKeyAppName;
            Title = "BrewAlert";
        }
    }

    private void HandleNavigationViewChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    private void OnLanguageChanged(string _)
    {
        BrewsNavText = _loc.Get("BrewsNavButton");
        if (_currentTitleKey != TitleKeyAppName && _currentTitleKey != TitleKeyBrewRunning)
            Title = _loc.Get(_currentTitleKey);
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
    }
}
