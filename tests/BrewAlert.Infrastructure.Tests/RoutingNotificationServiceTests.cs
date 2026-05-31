using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class RoutingNotificationServiceTests
{
    private static IOptionsMonitor<NotificationProviderOptions> CreateMonitor(string provider)
    {
        var monitor = Substitute.For<IOptionsMonitor<NotificationProviderOptions>>();
        monitor.CurrentValue.Returns(new NotificationProviderOptions { Provider = provider });
        return monitor;
    }

    private static BrewSession CreateSession() => new()
    {
        Profile = new BrewProfile { Name = "Test", Type = BrewType.Coffee, BrewDuration = TimeSpan.FromMinutes(4), Icon = "☕" },
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-4),
        EndsAtUtc = DateTime.UtcNow,
        State = BrewSessionState.Completed
    };

    [Fact]
    public async Task SendBrewCompletedAsync_RoutesToConsole_WhenProviderIsConsole()
    {
        var monitor = CreateMonitor(NotificationProvider.Console);

        // Since we cannot mock sealed classes via NSubstitute's Substiute.ForPartsOf,
        // we instantiate them with mock dependencies.
        var webhookNotifier = new TeamsWebhookNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsNotificationOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsWebhookNotifier>>());

        var graphNotifier = new TeamsGraphNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsGraphOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsGraphNotifier>>());

        var consoleNotifier = new ConsoleNotifier(Substitute.For<ILogger<ConsoleNotifier>>());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToConsole_WhenProviderIsConsole()
    {
        var monitor = CreateMonitor(NotificationProvider.Console);
        var webhookNotifier = new TeamsWebhookNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsNotificationOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsWebhookNotifier>>());

        var graphNotifier = new TeamsGraphNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsGraphOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsGraphNotifier>>());

        var consoleNotifier = new ConsoleNotifier(Substitute.For<ILogger<ConsoleNotifier>>());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.TestConnectionAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_RoutesToWebhook_WhenProviderIsWebhook()
    {
        var monitor = CreateMonitor(NotificationProvider.Webhook);

        var teamsOptions = Substitute.For<IOptionsMonitor<TeamsNotificationOptions>>();
        teamsOptions.CurrentValue.Returns(new TeamsNotificationOptions { WebhookUrl = "" }); // Force failure to prove routing to Webhook

        var webhookNotifier = new TeamsWebhookNotifier(
            Substitute.For<IHttpClientFactory>(),
            teamsOptions,
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsWebhookNotifier>>());

        var graphNotifier = new TeamsGraphNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsGraphOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsGraphNotifier>>());

        var consoleNotifier = new ConsoleNotifier(Substitute.For<ILogger<ConsoleNotifier>>());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        // Should be false because Webhook URL is not configured, meaning it reached the webhook notifier.
        Assert.False(result.IsSuccess);
        Assert.Equal("Webhook URL is not configured.", result.ErrorMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToGraph_WhenProviderIsGraph()
    {
        var monitor = CreateMonitor(NotificationProvider.Graph);
        var webhookNotifier = new TeamsWebhookNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsNotificationOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsWebhookNotifier>>());

        var graphOptions = Substitute.For<IOptionsMonitor<TeamsGraphOptions>>();
        graphOptions.CurrentValue.Returns(new TeamsGraphOptions { ChatId = "" }); // Causes test connection to return false

        var graphNotifier = new TeamsGraphNotifier(
            Substitute.For<IHttpClientFactory>(),
            graphOptions,
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsGraphNotifier>>());

        var consoleNotifier = new ConsoleNotifier(Substitute.For<ILogger<ConsoleNotifier>>());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.TestConnectionAsync();

        // Will be false if routed to Graph correctly because ChatId is empty
        Assert.False(result);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_FallsBackToConsole_WhenProviderIsUnknown()
    {
        var monitor = CreateMonitor("InvalidProvider");
        var webhookNotifier = new TeamsWebhookNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsNotificationOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsWebhookNotifier>>());

        var graphNotifier = new TeamsGraphNotifier(
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IOptionsMonitor<TeamsGraphOptions>>(),
            Substitute.For<IOptionsMonitor<LanguageOptions>>(),
            Substitute.For<ILogger<TeamsGraphNotifier>>());

        var consoleNotifier = new ConsoleNotifier(Substitute.For<ILogger<ConsoleNotifier>>());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
    }
}
