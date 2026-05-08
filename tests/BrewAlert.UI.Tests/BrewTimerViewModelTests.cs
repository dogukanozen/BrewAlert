using Avalonia.Headless.XUnit;
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

    // Helper to read the private _activeSessionId via reflection for testing attachment.
    private static Guid GetActiveSessionId(BrewTimerViewModel vm)
    {
        var field = typeof(BrewTimerViewModel)
            .GetField("_activeSessionId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Guid)field!.GetValue(vm)!;
    }
}
