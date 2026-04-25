using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.ViewModels;
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
    private readonly BrewProfileService _profileService;

    private static IOptionsMonitor<T> CreateMonitor<T>(T value) where T : class, new()
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(value);
        return monitor;
    }

    private readonly IOptionsMonitor<TeamsNotificationOptions> DefaultWebhookOptions =
        CreateMonitor(new TeamsNotificationOptions());

    private readonly IOptionsMonitor<NotificationProviderOptions> DefaultProviderOptions =
        CreateMonitor(new NotificationProviderOptions { Provider = NotificationProvider.Graph });

    public SettingsViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
    }

    [Fact]
    public void Constructor_MasksSensitiveInformation()
    {
        // Arrange
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "12345678-90ab-cdef-1234-567890abcdef",
            ClientId = "abcdef12-3456-7890-abcd-ef1234567890",
            ChatId = "19:abc@thread.v2"
        });

        // Act
        var vm = new SettingsViewModel(
            _notificationService, graphOptions, DefaultWebhookOptions, DefaultProviderOptions, _profileService, _preferencesService);

        // Assert
        Assert.StartsWith("12345678", vm.TenantId);
        Assert.Contains("••••••••", vm.TenantId);
        Assert.Equal("19:abc@thread.v2", vm.ChatId);
    }

    [Fact]
    public async Task TestConnection_SetsResult_WhenGraphConfigured()
    {
        // Arrange
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "T", ClientId = "C", ClientSecret = "S", ChatId = "CH"
        });
        _notificationService.TestConnectionAsync().Returns(true);
        var vm = new SettingsViewModel(
            _notificationService, graphOptions, DefaultWebhookOptions, DefaultProviderOptions, _profileService, _preferencesService);

        // Act
        await vm.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("başarılı", vm.TestResult);
    }

    [Fact]
    public void IsGraphConfigured_TrueWhenAllGraphFieldsSet()
    {
        var graphOptions = CreateMonitor(new TeamsGraphOptions
        {
            TenantId = "T", ClientId = "C", ClientSecret = "S", ChatId = "CH"
        });
        var vm = new SettingsViewModel(
            _notificationService, graphOptions, DefaultWebhookOptions, DefaultProviderOptions, _profileService, _preferencesService);

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
        var vm = new SettingsViewModel(
            _notificationService, CreateMonitor(new TeamsGraphOptions()), webhookOptions, providerOptions, _profileService, _preferencesService);

        Assert.True(vm.IsWebhookConfigured);
        Assert.True(vm.IsConfigured);
    }
}
