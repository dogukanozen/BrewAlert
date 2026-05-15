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
    private readonly IBrewTimerService _timerService;
    private readonly IBrewCompletionNotificationService _notificationCoordinator;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
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
    {
        _timerService = timerService;
        _notificationCoordinator = notificationCoordinator;
        _navigation = navigation;
        _loc = loc;

        _timerService.BrewStarted += OnBrewStarted;
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
            if (Items.Any(i => i.SessionId == e.Session.Id)) return;
            Items.Add(new BrewItemViewModel(e.Session, _timerService, _notificationCoordinator, _loc, RemoveItem));
            HasItems = true;
        });
    }

    private void RemoveItem(BrewItemViewModel item)
    {
        if (_disposed) return;
        Items.Remove(item);
        item.Dispose();
        HasItems = Items.Count > 0;
    }

    [RelayCommand]
    private void NewBrew() => _navigation.NavigateTo<ProfileListViewModel>();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerService.BrewStarted -= OnBrewStarted;
        _loc.LanguageChanged -= OnLanguageChanged;
        foreach (var item in Items)
            item.Dispose();
        Items.Clear();
    }
}
