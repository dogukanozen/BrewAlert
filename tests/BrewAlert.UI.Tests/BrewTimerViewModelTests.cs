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
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    [AvaloniaFact]
    public void StartBrew_SetsPropertiesAndStartsTimer()
    {
        // Arrange
        var vm = new BrewTimerViewModel(_timerService, _notificationService, _navigation);
        var profile = new BrewProfile
        {
            Name = "Coffee",
            BrewDuration = TimeSpan.FromMinutes(4),
            Icon = "☕"
        };
        _timerService.Start(profile).Returns(new BrewSession { Profile = profile });

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
    public void PauseCommand_CallsTimerService()
    {
        // Arrange
        var vm = new BrewTimerViewModel(_timerService, _notificationService, _navigation);
        var profile = new BrewProfile { Name = "Test", BrewDuration = TimeSpan.FromMinutes(1) };
        var session = new BrewSession { Profile = profile };
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
    public void CancelCommand_NavigatesBack()
    {
        // Arrange
        var vm = new BrewTimerViewModel(_timerService, _notificationService, _navigation);
        var profile = new BrewProfile { Name = "Test", BrewDuration = TimeSpan.FromMinutes(1) };
        _timerService.Start(profile).Returns(new BrewSession { Profile = profile });
        vm.StartBrew(profile);

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        Assert.False(vm.IsRunning);
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }
}
