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

public class BrewItemViewModelTests
{
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly IBrewCompletionNotificationService _notificationCoordinator =
        Substitute.For<IBrewCompletionNotificationService>();

    private static ILocalizationService CreateLoc()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        loc.CurrentLanguage.Returns("English");
        return loc;
    }

    private static BrewSession CreateSession(TimeSpan? duration = null, BrewSessionState state = BrewSessionState.Running)
    {
        var d = duration ?? TimeSpan.FromMinutes(4);
        return new BrewSession
        {
            Profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = d },
            Remaining = d,
            State = state
        };
    }

    private BrewItemViewModel CreateVm(BrewSession session, Action<BrewItemViewModel>? onRemove = null) =>
        new(session, _timerService, _notificationCoordinator, CreateLoc(), onRemove ?? (_ => { }));

    [AvaloniaFact]
    public void Constructor_PopulatesStateFromSession()
    {
        var session = CreateSession();

        var vm = CreateVm(session);

        Assert.Equal(session.Id, vm.SessionId);
        Assert.Equal("Coffee", vm.ProfileName);
        Assert.Equal("☕", vm.ProfileIcon);
        Assert.Equal(TimeSpan.FromMinutes(4), vm.TotalDuration);
        Assert.True(vm.IsRunning);
        Assert.False(vm.IsPaused);
        Assert.False(vm.IsCompleted);
    }

    [AvaloniaFact]
    public void Pause_CallsTimerServiceWithSessionId()
    {
        var session = CreateSession();
        var vm = CreateVm(session);

        vm.PauseCommand.Execute(null);

        _timerService.Received(1).Pause(session.Id);
        Assert.True(vm.IsPaused);
    }

    [AvaloniaFact]
    public void Resume_CallsTimerServiceWithSessionId()
    {
        var session = CreateSession(state: BrewSessionState.Paused);
        var vm = CreateVm(session);

        vm.ResumeCommand.Execute(null);

        _timerService.Received(1).Resume(session.Id);
        Assert.False(vm.IsPaused);
    }

    [AvaloniaFact]
    public void Cancel_RemovesItemAndCallsTimerService()
    {
        var session = CreateSession();
        BrewItemViewModel? removed = null;
        var vm = CreateVm(session, item => removed = item);

        vm.CancelCommand.Execute(null);

        _timerService.Received(1).Cancel(session.Id);
        Assert.Same(vm, removed);
    }

    [AvaloniaFact]
    public void TimerTick_ForOtherSession_IsIgnored()
    {
        var session = CreateSession();
        var vm = CreateVm(session);
        var originalRemaining = vm.Remaining;

        _timerService.TimerTick += Raise.Event<EventHandler<BrewTimerTickEvent>>(
            this, new BrewTimerTickEvent(Guid.NewGuid(), TimeSpan.FromMinutes(1)));

        Assert.Equal(originalRemaining, vm.Remaining);
    }

    [AvaloniaFact]
    public async Task TimerTick_ForOwnSession_UpdatesRemainingAndProgress()
    {
        var session = CreateSession();
        var vm = CreateVm(session);

        _timerService.TimerTick += Raise.Event<EventHandler<BrewTimerTickEvent>>(
            this, new BrewTimerTickEvent(session.Id, TimeSpan.FromMinutes(2)));

        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal(TimeSpan.FromMinutes(2), vm.Remaining);
        Assert.Equal(0.5, vm.Progress, 3);
    }

    [AvaloniaFact]
    public async Task BrewCompleted_FlipsToCompletedAndShowsSendingHint()
    {
        var session = CreateSession();
        var vm = CreateVm(session);

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsRunning);
        Assert.Equal(TimeSpan.Zero, vm.Remaining);
        Assert.Equal("SendingNotification", vm.NotificationStatus);
    }

    [AvaloniaFact]
    public async Task NotificationCompleted_BeforeBrewCompleted_PreservesFinalStatus()
    {
        var session = CreateSession();
        var vm = CreateVm(session);

        _notificationCoordinator.NotificationCompleted +=
            Raise.Event<EventHandler<BrewNotificationResult>>(this, new BrewNotificationResult(session.Id, true));
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(
            this, new BrewCompletedEvent(session));

        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal("NotificationSent", vm.NotificationStatus);
    }

    [AvaloniaFact]
    public void Dismiss_RemovesItemWithoutCancellingTimer()
    {
        var session = CreateSession();
        BrewItemViewModel? removed = null;
        var vm = CreateVm(session, item => removed = item);

        vm.DismissCommand.Execute(null);

        _timerService.DidNotReceive().Cancel(Arg.Any<Guid>());
        Assert.Same(vm, removed);
    }

    // Invariant (AGENT.md §4.3): event subscribers unsubscribe on Dispose.
    [Fact]
    public void Dispose_UnsubscribesFromAllEvents()
    {
        var loc = CreateLoc();
        var session = CreateSession();
        var vm = new BrewItemViewModel(session, _timerService, _notificationCoordinator, loc, _ => { });

        vm.Dispose();

        _timerService.Received(1).TimerTick -= Arg.Any<EventHandler<BrewTimerTickEvent>>();
        _timerService.Received(1).BrewCompleted -= Arg.Any<EventHandler<BrewCompletedEvent>>();
        _notificationCoordinator.Received(1).NotificationCompleted -= Arg.Any<EventHandler<BrewNotificationResult>>();
        loc.Received(1).LanguageChanged -= Arg.Any<Action<string>>();
    }
}
