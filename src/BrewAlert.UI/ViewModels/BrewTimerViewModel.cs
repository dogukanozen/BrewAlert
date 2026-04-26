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
    private readonly INotificationService _notificationService;
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
        INotificationService notificationService,
        INavigationService navigation,
        ILocalizationService loc)
    {
        _timerService = timerService;
        _notificationService = notificationService;
        _navigation = navigation;
        _loc = loc;

        _timerService.TimerTick += OnTimerTick;
        _timerService.BrewCompleted += OnBrewCompleted;
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

    public void StartBrew(BrewProfile profile)
    {
        ProfileName = profile.Name;
        ProfileIcon = profile.Icon;
        TotalDuration = profile.BrewDuration;
        Remaining = profile.BrewDuration;
        Progress = 0;
        IsRunning = true;
        IsPaused = false;
        IsCompleted = false;
        StatusText = _loc.Get("Brewing");
        NotificationStatus = string.Empty;

        var session = _timerService.Start(profile);
        _activeSessionId = session.Id;
    }

    [RelayCommand]
    private void Pause()
    {
        _timerService.Pause(_activeSessionId);
        IsPaused = true;
        StatusText = _loc.Get("Paused");
    }

    [RelayCommand]
    private void Resume()
    {
        _timerService.Resume(_activeSessionId);
        IsPaused = false;
        StatusText = _loc.Get("Brewing");
    }

    [RelayCommand]
    private void Cancel()
    {
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
            if (_disposed) return;
            Remaining = remaining;
            if (TotalDuration > TimeSpan.Zero)
                Progress = 1.0 - (remaining.TotalSeconds / TotalDuration.TotalSeconds);
        });
    }

    private async void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_disposed) return;
            IsRunning = false;
            IsCompleted = true;
            Progress = 1.0;
            Remaining = TimeSpan.Zero;
            StatusText = _loc.Get("Ready");

            NotificationStatus = _loc.Get("SendingNotification");
            var result = await _notificationService.SendBrewCompletedAsync(e.Session);
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
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
