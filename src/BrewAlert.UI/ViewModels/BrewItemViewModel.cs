using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// One row in the Active Brews list. Subscribes to timer/notification events
/// filtered by its own session id, so concurrent brews each update independently.
/// </summary>
public partial class BrewItemViewModel : ViewModelBase, IDisposable
{
    private readonly IBrewTimerService _timerService;
    private readonly IBrewCompletionNotificationService _notificationCoordinator;
    private readonly ILocalizationService _loc;
    private readonly Action<BrewItemViewModel> _onRemoveRequested;
    private bool _disposed;

    public Guid SessionId { get; }

    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private string _profileIcon = string.Empty;
    [ObservableProperty] private TimeSpan _remaining;
    [ObservableProperty] private TimeSpan _totalDuration;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _notificationStatus = string.Empty;

    [ObservableProperty] private string _pauseButtonText = string.Empty;
    [ObservableProperty] private string _resumeButtonText = string.Empty;
    [ObservableProperty] private string _cancelButtonText = string.Empty;
    [ObservableProperty] private string _dismissButtonText = string.Empty;

    public BrewItemViewModel(
        BrewSession session,
        IBrewTimerService timerService,
        IBrewCompletionNotificationService notificationCoordinator,
        ILocalizationService loc,
        Action<BrewItemViewModel> onRemoveRequested)
    {
        _timerService = timerService;
        _notificationCoordinator = notificationCoordinator;
        _loc = loc;
        _onRemoveRequested = onRemoveRequested;

        SessionId = session.Id;
        ProfileName = session.Profile.Name;
        ProfileIcon = session.Profile.Icon;
        TotalDuration = session.Profile.BrewDuration;
        ApplySessionState(session);

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
        DismissButtonText = _loc.Get("DismissButton");
        if (IsCompleted) StatusText = _loc.Get("Ready");
        else if (IsPaused) StatusText = _loc.Get("Paused");
        else if (IsRunning) StatusText = _loc.Get("Brewing");
    }

    private void OnLanguageChanged(string _) => RefreshLocalizedStrings();

    private void ApplySessionState(BrewSession session)
    {
        Remaining = session.Remaining;
        Progress = TotalDuration > TimeSpan.Zero
            ? 1.0 - (session.Remaining.TotalSeconds / TotalDuration.TotalSeconds)
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
    }

    [RelayCommand]
    private void Pause()
    {
        _timerService.Pause(SessionId);
        IsPaused = true;
        StatusText = _loc.Get("Paused");
    }

    [RelayCommand]
    private void Resume()
    {
        _timerService.Resume(SessionId);
        IsPaused = false;
        StatusText = _loc.Get("Brewing");
    }

    [RelayCommand]
    private void Cancel()
    {
        _timerService.Cancel(SessionId);
        _onRemoveRequested(this);
    }

    [RelayCommand]
    private void Dismiss() => _onRemoveRequested(this);

    private void OnTimerTick(object? sender, BrewTimerTickEvent e)
    {
        if (e.SessionId != SessionId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            Remaining = e.Remaining;
            if (TotalDuration > TimeSpan.Zero)
                Progress = 1.0 - (e.Remaining.TotalSeconds / TotalDuration.TotalSeconds);
        });
    }

    private void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        if (e.Session.Id != SessionId) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            IsRunning = false;
            IsCompleted = true;
            Progress = 1.0;
            Remaining = TimeSpan.Zero;
            StatusText = _loc.Get("Ready");
            if (string.IsNullOrEmpty(NotificationStatus))
                NotificationStatus = _loc.Get("SendingNotification");
        });
    }

    private void OnNotificationCompleted(object? sender, BrewNotificationResult result)
    {
        if (result.SessionId != SessionId) return;

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
