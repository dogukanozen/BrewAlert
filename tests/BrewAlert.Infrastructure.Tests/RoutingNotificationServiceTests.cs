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
        var webhookNotifier = Substitute.For<ITeamsWebhookNotifier>();
        var graphNotifier = Substitute.For<ITeamsGraphNotifier>();
        var consoleNotifier = Substitute.For<IConsoleNotifier>();
        consoleNotifier.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        await consoleNotifier.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToConsole_WhenProviderIsConsole()
    {
        var monitor = CreateMonitor(NotificationProvider.Console);
        var webhookNotifier = Substitute.For<ITeamsWebhookNotifier>();
        var graphNotifier = Substitute.For<ITeamsGraphNotifier>();
        var consoleNotifier = Substitute.For<IConsoleNotifier>();
        consoleNotifier.TestConnectionAsync().Returns(true);

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.TestConnectionAsync();

        Assert.True(result);
        await consoleNotifier.Received(1).TestConnectionAsync();
    }

    [Fact]
    public async Task SendBrewCompletedAsync_RoutesToWebhook_WhenProviderIsWebhook()
    {
        var monitor = CreateMonitor(NotificationProvider.Webhook);
        var webhookNotifier = Substitute.For<ITeamsWebhookNotifier>();
        var graphNotifier = Substitute.For<ITeamsGraphNotifier>();
        var consoleNotifier = Substitute.For<IConsoleNotifier>();
        webhookNotifier.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        await webhookNotifier.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToGraph_WhenProviderIsGraph()
    {
        var monitor = CreateMonitor(NotificationProvider.Graph);
        var webhookNotifier = Substitute.For<ITeamsWebhookNotifier>();
        var graphNotifier = Substitute.For<ITeamsGraphNotifier>();
        var consoleNotifier = Substitute.For<IConsoleNotifier>();
        graphNotifier.TestConnectionAsync().Returns(true);

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.TestConnectionAsync();

        Assert.True(result);
        await graphNotifier.Received(1).TestConnectionAsync();
    }

    [Fact]
    public async Task SendBrewCompletedAsync_FallsBackToConsole_WhenProviderIsUnknown()
    {
        var monitor = CreateMonitor("InvalidProvider");
        var webhookNotifier = Substitute.For<ITeamsWebhookNotifier>();
        var graphNotifier = Substitute.For<ITeamsGraphNotifier>();
        var consoleNotifier = Substitute.For<IConsoleNotifier>();
        consoleNotifier.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var svc = new RoutingNotificationService(webhookNotifier, graphNotifier, consoleNotifier, monitor, Substitute.For<ILogger<RoutingNotificationService>>());

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        await consoleNotifier.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }
}
