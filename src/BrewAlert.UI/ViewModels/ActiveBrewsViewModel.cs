using System.Collections.ObjectModel;
using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Hosts a live list of concurrently running brews. Each row is a
/// <see cref="BrewItemViewModel"/> with its own pause/cancel/dismiss state.
/// </summary>
public partial class ActiveBrewsViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan DefaultAutoReturnDelay = TimeSpan.FromSeconds(30);

    private readonly IBrewTimerService _timerService;
    private readonly IBrewCompletionNotificationService _notificationCoordinator;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly TimeSpan _autoReturnDelay;
    private DispatcherTimer? _autoReturnTimer;
    private bool _disposed;

    public ObservableCollection<BrewItemViewModel> Items { get; } = [];

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _newBrewButtonText = string.Empty;
    [ObservableProperty] private string _emptyStateText = string.Empty;
    [ObservableProperty] private bool _hasItems;

    public ActiveBrewsViewModel(
        IBrewTimerService timerService,
        IBrewCompletionNotificationService notificationCoordinator,
        INavigationService navigation,
        ILocalizationService loc)
        : this(timerService, notificationCoordinator, navigation, loc, DefaultAutoReturnDelay)
    {
    }

    internal ActiveBrewsViewModel(
        IBrewTimerService timerService,
        IBrewCompletionNotificationService notificationCoordinator,
        INavigationService navigation,
        ILocalizationService loc,
        TimeSpan autoReturnDelay)
    {
        _timerService = timerService;
        _notificationCoordinator = notificationCoordinator;
        _navigation = navigation;
        _loc = loc;
        _autoReturnDelay = autoReturnDelay;

        _timerService.BrewStarted += OnBrewStarted;
        _timerService.BrewCompleted += OnBrewCompleted;
        _timerService.BrewCancelled += OnBrewCancelled;
        _loc.LanguageChanged += OnLanguageChanged;

        RefreshLocalizedStrings();
        SyncFromExistingSessions();
    }

    private void RefreshLocalizedStrings()
    {
        Title = _loc.Get("ActiveBrewsTitle");
        NewBrewButtonText = _loc.Get("NewBrewButton");
        EmptyStateText = _loc.Get("NoActiveBrews");
    }

    private void OnLanguageChanged(string _) => RefreshLocalizedStrings();

    private void SyncFromExistingSessions()
    {
        foreach (var session in _timerService.GetActiveSessions())
        {
            if (Items.Any(i => i.SessionId == session.Id)) continue;
            Items.Add(new BrewItemViewModel(session, _timerService, _notificationCoordinator, _loc, RemoveItem));
        }
        HasItems = Items.Count > 0;
    }

    private void OnBrewStarted(object? sender, BrewStartedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            StopAutoReturnTimer();
            if (Items.Any(i => i.SessionId == e.Session.Id)) return;
            Items.Add(new BrewItemViewModel(e.Session, _timerService, _notificationCoordinator, _loc, RemoveItem));
            HasItems = true;
        });
    }

    // When the last running brew completes, schedule an auto-return to home so a finished
    // screen doesn't sit indefinitely. New brew / dismiss / cancel all reset the timer.
    private void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            if (_timerService.GetActiveSessions().Count == 0)
                StartAutoReturnTimer();
        });
    }

    private void OnBrewCancelled(object? sender, BrewCancelledEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            if (_timerService.GetActiveSessions().Count == 0 && Items.Count == 0)
                StopAutoReturnTimer();
        });
    }

    private void StartAutoReturnTimer()
    {
        StopAutoReturnTimer();
        _autoReturnTimer = new DispatcherTimer { Interval = _autoReturnDelay };
        _autoReturnTimer.Tick += OnAutoReturnTick;
        _autoReturnTimer.Start();
    }

    private void StopAutoReturnTimer()
    {
        if (_autoReturnTimer is null) return;
        _autoReturnTimer.Stop();
        _autoReturnTimer.Tick -= OnAutoReturnTick;
        _autoReturnTimer = null;
    }

    private void OnAutoReturnTick(object? sender, EventArgs e)
    {
        StopAutoReturnTimer();
        if (_disposed) return;
        if (_timerService.GetActiveSessions().Count != 0) return;
        _navigation.NavigateTo<ProfileListViewModel>();
    }

    private void RemoveItem(BrewItemViewModel item)
    {
        if (_disposed) return;
        Items.Remove(item);
        item.Dispose();
        HasItems = Items.Count > 0;

        if (Items.Count == 0)
        {
            StopAutoReturnTimer();
            _navigation.NavigateTo<ProfileListViewModel>();
        }
    }

    [RelayCommand]
    private void NewBrew() => _navigation.NavigateTo<ProfileListViewModel>();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAutoReturnTimer();
        _timerService.BrewStarted -= OnBrewStarted;
        _timerService.BrewCompleted -= OnBrewCompleted;
        _timerService.BrewCancelled -= OnBrewCancelled;
        _loc.LanguageChanged -= OnLanguageChanged;
        foreach (var item in Items)
            item.Dispose();
        Items.Clear();
    }
}
