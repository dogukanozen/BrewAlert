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

public class BrewTimerViewModelTests
{
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly IBrewCompletionNotificationService _notificationCoordinator =
        Substitute.For<IBrewCompletionNotificationService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private static ILocalizationService CreateEnglishLoc()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get("PauseButton").Returns("⏸ Pause");
        loc.Get("ResumeButton").Returns("▶ Resume");
        loc.Get("CancelButton").Returns("✕ Cancel");
        loc.Get("BackButton").Returns("← Back to Brews");
        loc.Get("Brewing").Returns("Brewing...");
        loc.Get("Paused").Returns("Paused");
        loc.Get("Cancelled").Returns("Cancelled");
        loc.Get("Ready").Returns("Ready! ☕");
        loc.Get("SendingNotification").Returns("Sending notification...");
        loc.Get("NotificationSent").Returns("✅ Notification sent!");
        loc.Get("CouldNotSend").Returns("❌ Could not send: {0}");
        loc.CurrentLanguage.Returns("English");
        return loc;
    }

    private BrewTimerViewModel CreateVm() =>
        new(_timerService, _notificationCoordinator, _navigation, CreateEnglishLoc());

    [AvaloniaFact]
    public void StartBrew_WhenNoActiveSession_SetsPropertiesAndStartsTimer()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile
        {
            Name = "Coffee",
            BrewDuration = TimeSpan.FromMinutes(4),
            Icon = "☕"
        };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);

        // Act
        vm.StartBrew(profile);

        // Assert
        Assert.Equal("Coffee", vm.ProfileName);
        Assert.Equal("☕", vm.ProfileIcon);
        Assert.Equal(TimeSpan.FromMinutes(4), vm.TotalDuration);
        Assert.True(vm.IsRunning);
        _timerService.Received(1).Start(profile);
    }

    [AvaloniaFact]
    public void StartBrew_WhenRunningSessionExists_AttachesAndDoesNotCallStart()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(5), Icon = "🍵" };
        var existing = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Running,
            Remaining = TimeSpan.FromMinutes(3)
        };
        _timerService.GetActiveSession().Returns(existing);

        // Act
        vm.StartBrew(profile);

        // Assert — attaches without calling Start()
        _timerService.DidNotReceive().Start(Arg.Any<BrewProfile>());
        Assert.Equal("Tea", vm.ProfileName);
        Assert.Equal(existing.Id, GetActiveSessionId(vm));
        Assert.True(vm.IsRunning);
        Assert.False(vm.IsPaused);
    }

    [AvaloniaFact]
    public void StartBrew_WhenPausedSessionExists_AttachesAndSetsPausedState()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4), Icon = "☕" };
        var existing = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Paused,
            Remaining = TimeSpan.FromMinutes(2)
        };
        _timerService.GetActiveSession().Returns(existing);

        // Act
        vm.StartBrew(profile);

        // Assert
        _timerService.DidNotReceive().Start(Arg.Any<BrewProfile>());
        Assert.True(vm.IsPaused);
        Assert.True(vm.IsRunning);
    }

    [AvaloniaFact]
    public void PauseCommand_CallsTimerService()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Test", BrewDuration = TimeSpan.FromMinutes(1) };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);
        vm.StartBrew(profile);

        // Act
        vm.PauseCommand.Execute(null);

        // Assert
        Assert.True(vm.IsPaused);
        Assert.Equal("Paused", vm.StatusText);
        _timerService.Received(1).Pause(session.Id);
    }

    [AvaloniaFact]
    public void Pause_AfterAttach_UsesAttachedSessionId()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(5), Icon = "🍵" };
        var existing = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Running,
            Remaining = TimeSpan.FromMinutes(3)
        };
        _timerService.GetActiveSession().Returns(existing);
        vm.StartBrew(profile); // attaches

        // Act
        vm.PauseCommand.Execute(null);

        // Assert — uses the reattached session id
        _timerService.Received(1).Pause(existing.Id);
    }

    [AvaloniaFact]
    public void Resume_AfterAttach_UsesAttachedSessionId()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(5), Icon = "🍵" };
        var existing = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Paused,
            Remaining = TimeSpan.FromMinutes(3)
        };
        _timerService.GetActiveSession().Returns(existing);
        vm.StartBrew(profile); // attaches

        // Act
        vm.ResumeCommand.Execute(null);

        // Assert
        _timerService.Received(1).Resume(existing.Id);
    }

    [AvaloniaFact]
    public void CancelCommand_NavigatesBack()
    {
        // Arrange
        var vm = CreateVm();
        var profile = new BrewProfile { Name = "Test", BrewDuration = TimeSpan.FromMinutes(1) };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(new BrewSession { Profile = profile });
        vm.StartBrew(profile);

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        Assert.False(vm.IsRunning);
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task OnBrewCompleted_WhenNotificationAlreadySucceeded_PreservesSuccessStatus()
    {
        // Arrange — simulate the race where NotificationCompleted fires before BrewCompleted's Post runs
        using var vm = CreateVm();
        var profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);
        vm.StartBrew(profile);

        // NotificationCompleted fires first (synchronous notifier path)
        _notificationCoordinator.NotificationCompleted +=
            Raise.Event<EventHandler<BrewNotificationResult>>(this, new BrewNotificationResult(session.Id, true));

        // BrewCompleted fires after — its Post must not overwrite the already-final status
        _timerService.BrewCompleted +=
            Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        // Flush all queued UI-thread posts (InvokeAsync is enqueued after them)
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal("✅ Notification sent!", vm.NotificationStatus);
    }

    [AvaloniaFact]
    public async Task OnBrewCompleted_WhenNotificationAlreadyFailed_PreservesFailureStatus()
    {
        using var vm = CreateVm();
        var profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);
        vm.StartBrew(profile);

        _notificationCoordinator.NotificationCompleted +=
            Raise.Event<EventHandler<BrewNotificationResult>>(
                this, new BrewNotificationResult(session.Id, false, "Teams unreachable"));

        _timerService.BrewCompleted +=
            Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal("❌ Could not send: Teams unreachable", vm.NotificationStatus);
    }

    [AvaloniaFact]
    public async Task OnBrewCompleted_AutoReturnsToProfilesAfterDelay()
    {
        using var vm = CreateVm();
        vm.AutoReturnDelay = TimeSpan.FromMilliseconds(1);
        var profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);
        vm.StartBrew(profile);

        _timerService.BrewCompleted +=
            Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        await Task.Delay(50);
        await Dispatcher.UIThread.InvokeAsync(() => { });

        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [AvaloniaFact]
    public async Task BackToProfiles_CancelsPendingAutoReturn()
    {
        // Use a long auto-return delay so the race between auto-return firing and
        // the manual Back tap is removed entirely: if the cancel works the CTS
        // field is nulled synchronously; if it doesn't, the field still holds a
        // non-cancelled CTS that would later fire NavigateTo. We assert on the
        // private state directly rather than relying on Task.Delay timing — the
        // previous version was flaky under some headless dispatcher setups.
        using var vm = CreateVm();
        vm.AutoReturnDelay = TimeSpan.FromMinutes(5);
        var profile = new BrewProfile { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile, Remaining = profile.BrewDuration };
        _timerService.GetActiveSession().Returns((BrewSession?)null);
        _timerService.Start(profile).Returns(session);
        vm.StartBrew(profile);

        _timerService.BrewCompleted +=
            Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.NotNull(GetAutoReturnCts(vm)); // sanity: auto-return is armed

        vm.BackToProfilesCommand.Execute(null);

        // BackToProfiles → Dispose → CancelAutoReturn nulls the field synchronously.
        Assert.Null(GetAutoReturnCts(vm));
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    private static CancellationTokenSource? GetAutoReturnCts(BrewTimerViewModel vm)
    {
        var field = typeof(BrewTimerViewModel)
            .GetField("_autoReturnCts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (CancellationTokenSource?)field!.GetValue(vm);
    }

    // Helper to read the private _activeSessionId via reflection for testing attachment.
    private static Guid GetActiveSessionId(BrewTimerViewModel vm)
    {
        var field = typeof(BrewTimerViewModel)
            .GetField("_activeSessionId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Guid)field!.GetValue(vm)!;
    }

    // Invariant (AGENT.md §4.3): event subscribers implement IDisposable and
    // unsubscribe on Dispose. We verify the -= calls directly via NSubstitute's
    // Received() on the event accessor — a state-based check (e.g. raising the
    // event after Dispose and asserting "no state change") would silently pass
    // because the VM also has an internal `_disposed` guard inside its handlers.

    [Fact]
    public void Dispose_UnsubscribesFromTimerServiceEvents()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        var vm = new BrewTimerViewModel(_timerService, _notificationCoordinator, _navigation, loc);

        vm.Dispose();

        _timerService.Received(1).TimerTick -= Arg.Any<EventHandler<TimeSpan>>();
        _timerService.Received(1).BrewCompleted -= Arg.Any<EventHandler<BrewCompletedEvent>>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromNotificationCoordinator()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        var vm = new BrewTimerViewModel(_timerService, _notificationCoordinator, _navigation, loc);

        vm.Dispose();

        _notificationCoordinator.Received(1).NotificationCompleted -= Arg.Any<EventHandler<BrewNotificationResult>>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        var vm = new BrewTimerViewModel(_timerService, _notificationCoordinator, _navigation, loc);

        vm.Dispose();

        loc.Received(1).LanguageChanged -= Arg.Any<Action<string>>();
    }
}
