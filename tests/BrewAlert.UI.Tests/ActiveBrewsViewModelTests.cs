using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class ActiveBrewsViewModelTests
{
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly IBrewCompletionNotificationService _notificationCoordinator =
        Substitute.For<IBrewCompletionNotificationService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly ILocalizationService _loc;

    public ActiveBrewsViewModelTests()
    {
        _loc = Substitute.For<ILocalizationService>();
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.CurrentLanguage.Returns("English");
        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
    }

    private ActiveBrewsViewModel CreateVm() =>
        new(_timerService, _notificationCoordinator, _navigation, _loc);

    private static BrewSession Session(string name = "Coffee") => new()
    {
        Profile = new BrewProfile { Name = name, Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) },
        Remaining = TimeSpan.FromMinutes(4)
    };

    [AvaloniaFact]
    public void Constructor_HydratesItemsFromExistingSessions()
    {
        var s1 = Session("Coffee");
        var s2 = Session("Tea");
        _timerService.GetActiveSessions().Returns([s1, s2]);

        using var vm = CreateVm();

        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, i => i.SessionId == s1.Id);
        Assert.Contains(vm.Items, i => i.SessionId == s2.Id);
        Assert.True(vm.HasItems);
    }

    [AvaloniaFact]
    public async Task BrewStarted_AddsNewItem()
    {
        using var vm = CreateVm();
        Assert.Empty(vm.Items);

        var session = Session();
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Single(vm.Items);
        Assert.Equal(session.Id, vm.Items[0].SessionId);
        Assert.True(vm.HasItems);
    }

    [AvaloniaFact]
    public async Task BrewStarted_TwiceForSameSession_AddsOnlyOnce()
    {
        using var vm = CreateVm();
        var session = Session();

        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(session));
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Single(vm.Items);
    }

    [AvaloniaFact]
    public async Task ItemCancel_RemovesItemFromList()
    {
        using var vm = CreateVm();
        var session = Session();
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        vm.Items[0].CancelCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.False(vm.HasItems);
        // Cancelling the last brew should hand the user back to the home screen.
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task ItemCancel_NotLastBrew_StaysOnActiveView()
    {
        using var vm = CreateVm();
        var s1 = Session("Coffee");
        var s2 = Session("Tea");
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(s1));
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(s2));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        vm.Items[0].CancelCommand.Execute(null);

        Assert.Single(vm.Items);
        _navigation.DidNotReceive().NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task AllBrewsCompleted_NavigatesHomeAfterAutoReturnDelay()
    {
        var session = Session();
        _timerService.GetActiveSessions().Returns([session]);
        using var vm = new ActiveBrewsViewModel(
            _timerService, _notificationCoordinator, _navigation, _loc, TimeSpan.FromMilliseconds(50));

        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(session));

        await WaitUntilAsync(NavigatedHome, TimeSpan.FromSeconds(2));

        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task LastRunningCancelled_WithCompletedRowVisible_StartsAutoReturn()
    {
        // Repro: one brew completes (stays as dismissable row), the other is cancelled.
        // Active sessions drop to zero but Items is non-empty — auto-return must fire.
        var completed = Session("Coffee");
        var running = Session("Tea");
        _timerService.GetActiveSessions().Returns([completed, running]);
        using var vm = new ActiveBrewsViewModel(
            _timerService, _notificationCoordinator, _navigation, _loc, TimeSpan.FromMilliseconds(50));

        _timerService.GetActiveSessions().Returns([running]);
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(completed));
        await Dispatcher.UIThread.InvokeAsync(() => { });
        _navigation.DidNotReceive().NavigateTo<ProfileListViewModel>();

        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        vm.Items.First(i => i.SessionId == running.Id).CancelCommand.Execute(null);

        // Cancel removed only the running row — completed row still visible, no immediate nav.
        Assert.Single(vm.Items);
        _navigation.DidNotReceive().NavigateTo<ProfileListViewModel>();

        await WaitUntilAsync(NavigatedHome, TimeSpan.FromSeconds(2));
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task NewBrewStartedDuringAutoReturn_CancelsReturn()
    {
        var s1 = Session("Coffee");
        _timerService.GetActiveSessions().Returns([s1]);
        using var vm = new ActiveBrewsViewModel(
            _timerService, _notificationCoordinator, _navigation, _loc, TimeSpan.FromMilliseconds(80));

        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(s1));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        var s2 = Session("Tea");
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(s2));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        // Auto-return delay is 80ms; pump for well past it and assert nothing happens.
        await PumpForAsync(TimeSpan.FromMilliseconds(300));

        _navigation.DidNotReceive().NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task ItemDismiss_LastCompletedBrew_RemovesAndNavigatesHome()
    {
        using var vm = CreateVm();
        var session = Session();
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(
            this, new BrewStartedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        vm.Items[0].DismissCommand.Execute(null);

        Assert.Empty(vm.Items);
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    private bool NavigatedHome() =>
        _navigation.ReceivedCalls()
            .Any(c => c.GetMethodInfo() is { IsGenericMethod: true } m
                && m.GetGenericArguments().Length == 1
                && m.GetGenericArguments()[0] == typeof(ProfileListViewModel));

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
            if (predicate()) return;
            await Task.Delay(10);
        }
        await Dispatcher.UIThread.InvokeAsync(() => { });
        if (!predicate())
            throw new TimeoutException($"Condition not met within {timeout.TotalMilliseconds}ms.");
    }

    private static async Task PumpForAsync(TimeSpan duration)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < duration)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
            await Task.Delay(10);
        }
    }

    [AvaloniaFact]
    public void NewBrew_NavigatesToProfileList()
    {
        using var vm = CreateVm();

        vm.NewBrewCommand.Execute(null);

        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [Fact]
    public void Dispose_UnsubscribesAndDisposesItems()
    {
        var session = Session();
        _timerService.GetActiveSessions().Returns([session]);
        var vm = CreateVm();
        Assert.Single(vm.Items);

        vm.Dispose();

        _timerService.Received(1).BrewStarted -= Arg.Any<EventHandler<BrewStartedEvent>>();
        // VM and each item both subscribe to LanguageChanged; both must unsubscribe.
        _loc.Received(2).LanguageChanged -= Arg.Any<Action<string>>();
        Assert.Empty(vm.Items);
    }
}
