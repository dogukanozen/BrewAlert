using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the active brew countdown screen.
/// Implements IDisposable to unsubscribe from timer and localization events.
/// </summary>
public partial class BrewTimerViewModel : ViewModelBase, IDisposable
{
    private readonly IBrewTimerService _timerService;
    private readonly IBrewCompletionNotificationService _notificationCoordinator;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private string _profileIcon = "☕";
    [ObservableProperty] private TimeSpan _remaining;
    [ObservableProperty] private TimeSpan _totalDuration;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _notificationStatus = string.Empty;

    // Localized button labels (bound from AXAML)
    [ObservableProperty] private string _pauseButtonText = string.Empty;
    [ObservableProperty] private string _resumeButtonText = string.Empty;
    [ObservableProperty] private string _cancelButtonText = string.Empty;
    [ObservableProperty] private string _backButtonText = string.Empty;

    private Guid _activeSessionId;
    private bool _disposed;

    public BrewTimerViewModel(
        IBrewTimerService timerService,
        IBrewCompletionNotificationService notificationCoordinator,
        INavigationService navigation,
        ILocalizationService loc)
    {
        _timerService = timerService;
        _notificationCoordinator = notificationCoordinator;
        _navigation = navigation;
        _loc = loc;

        _timerService.TimerTick += OnTimerTick;
        _timerService.BrewCompleted += OnBrewCompleted;
        _notificationCoordinator.NotificationCompleted += OnNotificationCompleted;
        _loc.LanguageChanged += OnLanguageChanged;

        RefreshLocalizedStrings();
    }

    private void RefreshLocalizedStrings()
    {
        PauseButtonText = _loc.Get("PauseButton");
        ResumeButtonText = _loc.Get("ResumeButton");
        CancelButtonText = _loc.Get("CancelButton");
        BackButtonText = _loc.Get("BackButton");
    }

    private void OnLanguageChanged(string _) => RefreshLocalizedStrings();

    /// <summary>
    /// Populate all UI state from an existing session without calling Start().
    /// Used when reattaching after navigating away while a brew was in progress.
    /// </summary>
    public void AttachToSession(BrewSession session)
    {
        _activeSessionId = session.Id;
        ProfileName = session.Profile.Name;
        ProfileIcon = session.Profile.Icon;
        TotalDuration = session.Profile.BrewDuration;
        Remaining = session.Remaining;
        Progress = TotalDuration > TimeSpan.Zero
            ? 1.0 - (session.Remaining.TotalSeconds / session.Profile.BrewDuration.TotalSeconds)
            : 0;
        IsRunning = session.State is BrewSessionState.Running or BrewSessionState.Paused;
        IsPaused = session.State == BrewSessionState.Paused;
        IsCompleted = session.State == BrewSessionState.Completed;
        StatusText = session.State switch
        {
            BrewSessionState.Paused => _loc.Get("Paused"),
            BrewSessionState.Completed => _loc.Get("Ready"),
            _ => _loc.Get("Brewing")
        };
        NotificationStatus = string.Empty;
    }

    /// <summary>
    /// Start a brew or reattach to an already-running session.
    /// Never throws if a Running/Paused session exists — attaches to it instead.
    /// </summary>
    public void StartBrew(BrewProfile profile)
    {
        var activeSession = _timerService.GetActiveSession();
        if (activeSession is { State: BrewSessionState.Running or BrewSessionState.Paused })
        {
            AttachToSession(activeSession);
            return;
        }

        var session = _timerService.Start(profile);
        AttachToSession(session);
    }

    [RelayCommand]
    private void Pause()
    {
        if (_activeSessionId == Guid.Empty) return;
        _timerService.Pause(_activeSessionId);
        IsPaused = true;
        StatusText = _loc.Get("Paused");
    }

    [RelayCommand]
    private void Resume()
    {
        if (_activeSessionId == Guid.Empty) return;
        _timerService.Resume(_activeSessionId);
        IsPaused = false;
        StatusText = _loc.Get("Brewing");
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_activeSessionId != Guid.Empty)
            _timerService.Cancel(_activeSessionId);
        IsRunning = false;
        StatusText = _loc.Get("Cancelled");
        Dispose();
        _navigation.NavigateTo<ProfileListViewModel>();
    }

    [RelayCommand]
    private void BackToProfiles()
    {
        Dispose();
        _navigation.NavigateTo<ProfileListViewModel>();
    }

    private void OnTimerTick(object? sender, TimeSpan remaining)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || _activeSessionId == Guid.Empty) return;
            Remaining = remaining;
            if (TotalDuration > TimeSpan.Zero)
                Progress = 1.0 - (remaining.TotalSeconds / TotalDuration.TotalSeconds);
        });
    }

    private void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        if (e.Session.Id != _activeSessionId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            IsRunning = false;
            IsCompleted = true;
            Progress = 1.0;
            Remaining = TimeSpan.Zero;
            StatusText = _loc.Get("Ready");
            // Only set "Sending" if NotificationCompleted hasn't already delivered a result.
            // When the notification service completes synchronously the coordinator can fire
            // NotificationCompleted before this Post runs, so we must not overwrite it.
            if (string.IsNullOrEmpty(NotificationStatus))
                NotificationStatus = _loc.Get("SendingNotification");
        });
    }

    private void OnNotificationCompleted(object? sender, BrewNotificationResult result)
    {
        if (result.SessionId != _activeSessionId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            NotificationStatus = result.IsSuccess
                ? _loc.Get("NotificationSent")
                : string.Format(_loc.Get("CouldNotSend"), result.ErrorMessage);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerService.TimerTick -= OnTimerTick;
        _timerService.BrewCompleted -= OnBrewCompleted;
        _notificationCoordinator.NotificationCompleted -= OnNotificationCompleted;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
