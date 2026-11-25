using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using IPreferencesService = BrewAlert.UI.Services.IPreferencesService;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace BrewAlert.UI.Tests;

public class SettingsViewModelTests
{
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IProfileRepository _repository = Substitute.For<IProfileRepository>();
    private readonly IPreferencesService _preferencesService = Substitute.For<IPreferencesService>();
    private readonly IUpdateService _updateService = Substitute.For<IUpdateService>();
    private readonly ILocalizationService _loc = CreateEnglishLoc();
    private readonly IConfigurationRoot _configurationRoot = Substitute.For<IConfigurationRoot>();
    private readonly INetworkInfoService _network = Substitute.For<INetworkInfoService>();
    private readonly IRfidUserRepository _rfidUserRepo = Substitute.For<IRfidUserRepository>();
    private readonly IRfidReaderService _rfidReader = Substitute.For<IRfidReaderService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly RfidUserService _rfidUserService;
    private readonly BrewProfileService _profileService;

    private static IOptionsMonitor<T> CreateMonitor<T>(T value) where T : class, new()
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(value);
        return monitor;
    }

    private static ILocalizationService CreateEnglishLoc()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        loc.CurrentLanguage.Returns("English");
        loc.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        return loc;
    }

    private static IAppearanceService CreateAppearance()
    {
        var appearance = Substitute.For<IAppearanceService>();
        appearance.Options.Returns(new AppearanceOptions());
        return appearance;
    }

    private readonly IOptionsMonitor<TeamsNotificationOptions> DefaultWebhookOptions =
        CreateMonitor(new TeamsNotificationOptions());

    private readonly IOptionsMonitor<NotificationProviderOptions> DefaultProviderOptions =
        CreateMonitor(new NotificationProviderOptions { Provider = NotificationProvider.Graph });

    private readonly IOptionsMonitor<RfidOptions> DefaultRfidOptions =
        CreateMonitor(new RfidOptions { RequireCardToBrew = true });

    public SettingsViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
        _rfidUserService = new RfidUserService(_rfidUserRepo);
        _rfidUserRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RfidUser>());
        _updateService.CurrentVersion.Returns("1.0.0");
    }

    private SettingsViewModel CreateVm(
        IOptionsMonitor<TeamsGraphOptions>? graph = null,
        IOptionsMonitor<TeamsNotificationOptions>? webhook = null,
        IOptionsMonitor<NotificationProviderOptions>? provider = null,
        ILocalizationService? loc = null) =>
        new(
            _notificationService,
            graph ?? CreateMonitor(new TeamsGraphOptions()),
            webhook ?? DefaultWebhookOptions,
            provider ?? DefaultProviderOptions,
            DefaultRfidOptions,
            _profileService,
            _preferencesService,
            loc ?? _loc,
            _updateService,
            _configurationRoot,
            CreateAppearance(),
            _network,
            _rfidUserService,
            _rfidReader,
            _navigation);

    [Fact]
    public void Constructor_MasksSensitiveInformation()
    {
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "12345678-90ab-cdef-1234-567890abcdef",
            ClientId = "abcdef12-3456-7890-abcd-ef1234567890",
            ChatId = "19:abc@thread.v2"
        });

        var vm = CreateVm(graph: graphOptions);

        Assert.StartsWith("12345678", vm.TenantId);
        Assert.Contains("••••••••", vm.TenantId);
        Assert.StartsWith("19:abc@t", vm.ChatId);
        Assert.Contains("••••••••", vm.ChatId);
    }

    [Fact]
    public async Task TestConnection_SetsResult_WhenGraphConfigured()
    {
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "T",
            ClientId = "C",
            ClientSecret = "S",
            ChatId = "CH"
        });
        _notificationService.TestConnectionAsync().Returns(true);
        var vm = CreateVm(graph: graphOptions);

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Contains("ConnectionSuccess", vm.TestResult);
    }

    [Fact]
    public void IsGraphConfigured_TrueWhenAllGraphFieldsSet()
    {
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "T",
            ClientId = "C",
            ClientSecret = "S",
            ChatId = "CH"
        });
        var vm = CreateVm(graph: graphOptions);

        Assert.True(vm.IsGraphConfigured);
    }

    [Fact]
    public void IsWebhookConfigured_TrueWhenUrlSet()
    {
        var webhookOptions = CreateMonitor(new TeamsNotificationOptions
        {
            WebhookUrl = "https://example.webhook.office.com/xxx"
        });
        var providerOptions = CreateMonitor(new NotificationProviderOptions { Provider = NotificationProvider.Webhook });
        var vm = CreateVm(webhook: webhookOptions, provider: providerOptions);

        Assert.True(vm.IsWebhookConfigured);
        Assert.True(vm.IsConfigured);
    }

    [Fact]
    public void Constructor_InitializesWebhookUrlInputFromOptions()
    {
        var url = "https://prod.webhook.office.com/abc";
        var webhookOptions = CreateMonitor(new TeamsNotificationOptions { WebhookUrl = url });

        var vm = CreateVm(webhook: webhookOptions);

        Assert.Equal(url, vm.WebhookUrlInput);
    }

    [Fact]
    public async Task SaveWebhookUrlCommand_CallsPreferencesServiceAndSetsResult()
    {
        var vm = CreateVm();

        vm.WebhookUrlInput = "https://example.com/hook";
        await vm.SaveWebhookUrlCommand.ExecuteAsync(null);

        await _preferencesService.Received(1).SaveWebhookUrlAsync("https://example.com/hook", Arg.Any<CancellationToken>());
        Assert.Contains("WebhookUrlSaved", vm.TestResult);
    }

    [Fact]
    public async Task SetLanguageCommand_ReloadsConfigurationBeforeApplyingLanguage()
    {
        var vm = CreateVm();

        await vm.SetLanguageCommand.ExecuteAsync("Turkish");

        await _preferencesService.Received(1).SaveLanguageAsync("Turkish", Arg.Any<CancellationToken>());
        _configurationRoot.Received(1).Reload();
        _loc.Received(1).SetLanguage("Turkish");
    }

    [Fact]
    public void Constructor_SetsLocalizedProfileNameWatermark()
    {
        var vm = CreateVm();

        Assert.Equal("ProfileNameLabel", vm.ProfileNameWatermark);
    }

    [Fact]
    public void EditableProfileViewModel_DurationText_UsesLocalizedUnit()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get("MinShort").Returns("dk");
        loc.Get("DeleteProfile").Returns("Sil");

        var profile = new BrewProfile { Name = "Çay", BrewDuration = TimeSpan.FromMinutes(5) };
        var vm = new EditableProfileViewModel(profile, _profileService, loc);

        Assert.Equal("5 dk", vm.DurationText);
    }

    [Fact]
    public void EditableProfileViewModel_DurationText_RefreshesOnLanguageChange()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get("MinShort").Returns("min");
        loc.Get("DeleteProfile").Returns("Delete");
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(7) };
        var vm = new EditableProfileViewModel(profile, _profileService, loc);
        Assert.Equal("7 min", vm.DurationText);

        var trLoc = Substitute.For<ILocalizationService>();
        trLoc.Get("MinShort").Returns("dk");
        trLoc.Get("DeleteProfile").Returns("Sil");
        vm.RefreshLocalization(trLoc);

        Assert.Equal("7 dk", vm.DurationText);
    }

    [Fact]
    public async Task CheckForUpdates_WhenUpdateFound_SetsUpdateAvailableStatus()
    {
        _updateService.CheckForUpdatesAsync().Returns(true);
        var vm = CreateVm();

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("UpdateAvailable", vm.UpdateStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CheckForUpdates_WhenNoUpdate_SetsUpToDateStatus()
    {
        _updateService.CheckForUpdatesAsync().Returns(false);
        var vm = CreateVm();

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("UpToDate", vm.UpdateStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        // Invariant (AGENT.md §4.3): subscribers unsubscribe on Dispose.
        var loc = CreateEnglishLoc();
        var vm = CreateVm(loc: loc);

        vm.Dispose();

        // Settings and its appearance, network, and rfid children release their language subscriptions.
        loc.Received(4).LanguageChanged -= Arg.Any<Action<string>>();
    }

    [Fact]
    public async Task SaveWebhookUrlCommand_OnError_SetsTestResult()
    {
        _preferencesService
            .SaveWebhookUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("disk full")));

        var vm = CreateVm();

        await vm.SaveWebhookUrlCommand.ExecuteAsync(null);

        Assert.Contains("SaveFailed", vm.TestResult);
    }

    [Fact]
    public async Task RfidSettings_TogglesRequireCardAndNavigatesToUsers()
    {
        var vm = CreateVm();
        Assert.True(vm.Rfid.RequireCardToBrew);

        await vm.Rfid.DisableRequireCardCommand.ExecuteAsync(null);

        await _preferencesService.Received(1).SaveRequireCardToBrewAsync(false, Arg.Any<CancellationToken>());
        _configurationRoot.Received(1).Reload();
        Assert.False(vm.Rfid.RequireCardToBrew);

        vm.Rfid.OpenRfidUsersCommand.Execute(null);
        _navigation.Received(1).NavigateTo<RfidUsersViewModel>();
    }
}
