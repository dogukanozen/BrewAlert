using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class MainWindowViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    [Fact]
    public void Constructor_NavigatesToProfileList()
    {
        // Act
        var vm = new MainWindowViewModel(_navigation);

        // Assert
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [Fact]
    public void HandleNavigationViewChanged_UpdatesCurrentView()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation);
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
        var vm = new MainWindowViewModel(_navigation);

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert
        _navigation.Received(1).NavigateTo<SettingsViewModel>();
        Assert.Equal("Settings", vm.Title);
    }
}
