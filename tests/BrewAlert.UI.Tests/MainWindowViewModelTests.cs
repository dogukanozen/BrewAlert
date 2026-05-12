using Avalonia.Headless.XUnit;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class MainWindowViewModelTests
{
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly ILocalizationService _loc;
    private readonly IUpdateService _updateService = Substitute.For<IUpdateService>();

    public MainWindowViewModelTests()
    {
        _loc = Substitute.For<ILocalizationService>();
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.CurrentLanguage.Returns("English");
        _updateService.CheckForUpdatesAsync().Returns(false);
    }

    [Fact]
    public void Constructor_NavigatesToProfileList()
    {
        // Act
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        // Assert
        _navigation.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [Fact]
    public void HandleNavigationViewChanged_UpdatesCurrentView()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
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
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

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
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.NavigateToSettingsCommand.Execute(null);
        Assert.Equal("Settings", vm.Title);

        // Act
        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");

        // Assert
        Assert.Equal("Ayarlar", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_WhenNoActiveSession_NavigatesToProfileList()
    {
        // GetActiveSession() returns null by default (NSubstitute reference type default)
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.NavigateToSettingsCommand.Execute(null);

        // Act
        vm.NavigateToProfilesCommand.Execute(null);

        // Assert
        _navigation.Received(2).NavigateTo<ProfileListViewModel>(); // once in ctor, once now
        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_WhenActiveSession_NavigatesToBrewTimerAndSetsTitle()
    {
        // Arrange
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Running,
            Remaining = TimeSpan.FromMinutes(3)
        };
        _timerService.GetActiveSession().Returns(session);
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        // Act
        vm.NavigateToProfilesCommand.Execute(null);

        // Assert
        _navigation.Received(1).NavigateTo<BrewTimerViewModel>();
        Assert.Equal("☕ Coffee", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_SetsAppNameTitle_NotLocalized()
    {
        // Arrange
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.NavigateToSettingsCommand.Execute(null);

        // Act
        vm.NavigateToProfilesCommand.Execute(null);

        // Assert — app name stays "BrewAlert" regardless of language.
        Assert.Equal("BrewAlert", vm.Title);

        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");
        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void BrewCompleted_WhenTitleIsBrewRunning_ResetsToAppName()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        // Simulate brew starting (sets title to brew name)
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var startedSession = new BrewSession { Profile = profile };
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(this, new BrewStartedEvent(startedSession));
        Assert.Equal("☕ Coffee", vm.Title);

        // Act — brew completes
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(startedSession));

        // Assert
        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void BrewCancelled_WhenTitleIsBrewRunning_ResetsToAppName()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile };
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(this, new BrewStartedEvent(session));
        Assert.Equal("☕ Coffee", vm.Title);

        // Act — brew cancelled
        _timerService.BrewCancelled += Raise.Event<EventHandler<BrewCancelledEvent>>(this, new BrewCancelledEvent(session, TimeSpan.FromMinutes(2)));

        // Assert
        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void DismissUpdate_HidesToast()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = true;

        vm.DismissUpdateCommand.Execute(null);

        Assert.False(vm.IsUpdateToastVisible);
    }

    [Fact]
    public async Task InstallUpdate_OnSuccess_CallsServiceAndHidesToast()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = true;

        await ((IAsyncRelayCommand)vm.InstallUpdateCommand).ExecuteAsync(null);

        await _updateService.Received(1).DownloadAndInstallUpdatesAsync();
        Assert.False(vm.IsUpdateToastVisible);
    }

    [Fact]
    public async Task InstallUpdate_OnFailure_ShowsErrorMessageInToast()
    {
        _updateService.DownloadAndInstallUpdatesAsync().Returns(Task.FromException(new Exception("network error")));
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = true;

        await ((IAsyncRelayCommand)vm.InstallUpdateCommand).ExecuteAsync(null);

        Assert.True(vm.IsUpdateToastVisible);
        Assert.Equal("UpdateError", vm.UpdateToastMessage);
    }

    [Fact]
    public void LanguageChanged_WhenToastVisible_RefreshesToastTexts()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = true;

        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");

        Assert.Equal("UpdateAvailable", vm.UpdateToastMessage);
        Assert.Equal("InstallUpdate", vm.UpdateToastInstallText);
        Assert.Equal("UpdateDismiss", vm.UpdateToastDismissText);
    }

    [AvaloniaFact]
    public void UpdateAvailableEvent_WhenRaised_ShowsToastWithLocalizedTexts()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        Assert.False(vm.IsUpdateToastVisible);

        _updateService.UpdateAvailable += Raise.Event<Action>();

        Assert.True(vm.IsUpdateToastVisible);
        Assert.Equal("UpdateAvailable", vm.UpdateToastMessage);
        Assert.Equal("InstallUpdate", vm.UpdateToastInstallText);
        Assert.Equal("UpdateDismiss", vm.UpdateToastDismissText);
    }

    [AvaloniaFact]
    public void UpdateAvailableEvent_WhenToastAlreadyVisible_DoesNotResetTexts()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = true;
        vm.UpdateToastMessage = "already shown";

        _updateService.UpdateAvailable += Raise.Event<Action>();

        Assert.Equal("already shown", vm.UpdateToastMessage);
    }

    // Invariants (AGENT.md §4.3): event handlers unsubscribe on Dispose.

    [Fact]
    public void Dispose_UnsubscribesFromTimerEvents()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile };
        var titleBeforeDispose = vm.Title;

        vm.Dispose();
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(this, new BrewStartedEvent(session));
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));
        _timerService.BrewCancelled += Raise.Event<EventHandler<BrewCancelledEvent>>(this, new BrewCancelledEvent(session, TimeSpan.Zero));

        Assert.Equal(titleBeforeDispose, vm.Title);
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        loc.CurrentLanguage.Returns("English");
        var vm = new MainWindowViewModel(_navigation, _timerService, loc, _updateService);

        vm.Dispose();
        loc.ClearReceivedCalls();
        loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");

        loc.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact]
    public void Dispose_UnsubscribesFromUpdateAvailable()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = false;

        vm.Dispose();
        _updateService.UpdateAvailable += Raise.Event<Action>();

        Assert.False(vm.IsUpdateToastVisible);
    }

    [Fact]
    public void Dispose_UnsubscribesFromNavigationCurrentViewChanged()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        var stale = vm.CurrentView;

        vm.Dispose();
        var newVm = Substitute.For<ViewModelBase>();
        _navigation.CurrentViewChanged += Raise.Event<Action<ViewModelBase>>(newVm);

        Assert.Equal(stale, vm.CurrentView);
    }

    [Fact]
    public void LanguageChanged_WhenToastNotVisible_DoesNotRefreshToastTexts()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.IsUpdateToastVisible = false;
        vm.UpdateToastMessage = "stale";

        _loc.LanguageChanged += Raise.Event<Action<string>>("Turkish");

        Assert.Equal("stale", vm.UpdateToastMessage);
    }
}
