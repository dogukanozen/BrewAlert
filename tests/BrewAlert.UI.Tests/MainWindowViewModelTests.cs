using BrewAlert.Core.Interfaces;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class MainWindowViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly ILocalizationService _loc;

    public MainWindowViewModelTests()
    {
        _loc = Substitute.For<ILocalizationService>();
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.CurrentLanguage.Returns("English");
    }

    [Fact]
    public void Constructor_NavigatesToProfileList()
    {
        // Act
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);

        // Assert
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [Fact]
    public void HandleNavigationViewChanged_UpdatesCurrentView()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);
        var mockVm = Substitute.For<ViewModelBase>();

        // Act
        _navigation.CurrentViewChanged += Raise.Event<Action<ViewModelBase>>(mockVm);

        // Assert
        Assert.Equal(mockVm, vm.CurrentView);
    }

    [Fact]
    public void NavigateToSettings_CallsNavigation()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert
        _navigation.Received(1).NavigateTo<SettingsViewModel>();
        Assert.Equal("Settings", vm.Title);
    }
}
