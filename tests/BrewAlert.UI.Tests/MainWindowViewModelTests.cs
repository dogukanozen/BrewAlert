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
        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        vm.NavigateToSettingsCommand.Execute(null);

        vm.NavigateToProfilesCommand.Execute(null);

        _navigation.Received(2).NavigateTo<ProfileListViewModel>(); // once in ctor, once now
        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_WhenSingleActiveSession_NavigatesToActiveBrewsAndSetsProfileTitle()
    {
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession
        {
            Profile = profile,
            State = BrewSessionState.Running,
            Remaining = TimeSpan.FromMinutes(3)
        };
        _timerService.GetActiveSessions().Returns([session]);
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        vm.NavigateToProfilesCommand.Execute(null);

        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
        Assert.Equal("☕ Coffee", vm.Title);
    }

    [Fact]
    public void NavigateToProfiles_WhenMultipleActiveSessions_ShowsCountTitle()
    {
        var s1 = new BrewSession { Profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) } };
        var s2 = new BrewSession { Profile = new BrewProfile { Name = "Tea", Icon = "🍵", BrewDuration = TimeSpan.FromMinutes(3) } };
        _timerService.GetActiveSessions().Returns([s1, s2]);
        _loc.Get("ActiveBrewsCount").Returns("{0} active brews");
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        vm.NavigateToProfilesCommand.Execute(null);

        _navigation.Received(1).NavigateTo<ActiveBrewsViewModel>();
        Assert.Equal("2 active brews", vm.Title);
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
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var startedSession = new BrewSession { Profile = profile };

        // Brew starting — service reports it as active so title reflects it
        _timerService.GetActiveSessions().Returns([startedSession]);
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(this, new BrewStartedEvent(startedSession));
        Assert.Equal("☕ Coffee", vm.Title);

        // Brew completes — no longer active
        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(startedSession));

        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void BrewCancelled_WhenTitleIsBrewRunning_ResetsToAppName()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        var profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) };
        var session = new BrewSession { Profile = profile };

        _timerService.GetActiveSessions().Returns([session]);
        _timerService.BrewStarted += Raise.Event<EventHandler<BrewStartedEvent>>(this, new BrewStartedEvent(session));
        Assert.Equal("☕ Coffee", vm.Title);

        _timerService.GetActiveSessions().Returns(Array.Empty<BrewSession>());
        _timerService.BrewCancelled += Raise.Event<EventHandler<BrewCancelledEvent>>(this, new BrewCancelledEvent(session, TimeSpan.FromMinutes(2)));

        Assert.Equal("BrewAlert", vm.Title);
    }

    [Fact]
    public void BrewCompleted_WithRemainingSession_ShowsThatProfilesTitle()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);
        var coffee = new BrewSession { Profile = new BrewProfile { Name = "Coffee", Icon = "☕", BrewDuration = TimeSpan.FromMinutes(4) } };
        var tea = new BrewSession { Profile = new BrewProfile { Name = "Tea", Icon = "🍵", BrewDuration = TimeSpan.FromMinutes(3) } };

        _timerService.GetActiveSessions().Returns([coffee]);
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(tea));

        Assert.Equal("☕ Coffee", vm.Title);
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
    // We verify the -= calls directly via NSubstitute.Received() rather than
    // raising the event and observing state — a state-based check can pass
    // accidentally when an unrelated path also produces the same final state.

    [Fact]
    public void Dispose_UnsubscribesFromTimerServiceEvents()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        vm.Dispose();

        _timerService.Received(1).BrewStarted -= Arg.Any<EventHandler<BrewStartedEvent>>();
        _timerService.Received(1).BrewCompleted -= Arg.Any<EventHandler<BrewCompletedEvent>>();
        _timerService.Received(1).BrewCancelled -= Arg.Any<EventHandler<BrewCancelledEvent>>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        loc.CurrentLanguage.Returns("English");
        var vm = new MainWindowViewModel(_navigation, _timerService, loc, _updateService);

        vm.Dispose();

        loc.Received(1).LanguageChanged -= Arg.Any<Action<string>>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromUpdateAvailable()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        vm.Dispose();

        _updateService.Received(1).UpdateAvailable -= Arg.Any<Action>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromNavigationCurrentViewChanged()
    {
        var vm = new MainWindowViewModel(_navigation, _timerService, _loc, _updateService);

        vm.Dispose();

        _navigation.Received(1).CurrentViewChanged -= Arg.Any<Action<ViewModelBase>>();
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
