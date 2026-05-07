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
    public void NavigateToSettings_SetsLocalizedTitle()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert — mock returns the key as value, so the title is the key itself.
        _navigation.Received(1).NavigateTo<SettingsViewModel>();
        Assert.Equal("SettingsTitle", vm.Title);
    }

    [Fact]
    public void NavigateToSettings_RefreshesTitle_OnLanguageChange()
    {
        // Arrange — first call returns English label, second returns Turkish.
        _loc.Get("SettingsTitle").Returns("Settings", "Ayarlar");
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);
        vm.NavigateToSettingsCommand.Execute(null);
        Assert.Equal("Settings", vm.Title);

        // Act
        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");

        // Assert
        Assert.Equal("Ayarlar", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_SetsAppNameTitle_NotLocalized()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc);
        vm.NavigateToSettingsCommand.Execute(null);

        // Act
        vm.NavigateToProfilesCommand.Execute(null);

        // Assert — app name stays "BrewAlert" regardless of language.
        Assert.Equal("BrewAlert", vm.Title);

        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");
        Assert.Equal("BrewAlert", vm.Title);
    }
}
