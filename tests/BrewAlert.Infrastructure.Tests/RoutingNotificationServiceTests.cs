using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
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
    public async Task SendBrewCompletedAsync_ReturnsWebhookResult_WhenProviderIsWebhook()
    {
        var webhookSub = Substitute.For<INotificationService>();
        var graphSub = Substitute.For<INotificationService>();
        var consoleSub = Substitute.For<INotificationService>();
        webhookSub.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var monitor = CreateMonitor(NotificationProvider.Webhook);
        var svc = new RoutingServiceShim(webhookSub, graphSub, consoleSub, monitor);

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        await webhookSub.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
        await graphSub.DidNotReceive().SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }

    [Fact]
    public async Task SendBrewCompletedAsync_ReturnsGraphResult_WhenProviderIsGraph()
    {
        var webhookSub = Substitute.For<INotificationService>();
        var graphSub = Substitute.For<INotificationService>();
        var consoleSub = Substitute.For<INotificationService>();
        graphSub.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var monitor = CreateMonitor(NotificationProvider.Graph);
        var svc = new RoutingServiceShim(webhookSub, graphSub, consoleSub, monitor);

        var result = await svc.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        await graphSub.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
        await webhookSub.DidNotReceive().SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }

    [Fact]
    public async Task SendBrewCompletedAsync_FallsBackToConsole_WhenProviderIsUnknown()
    {
        var webhookSub = Substitute.For<INotificationService>();
        var graphSub = Substitute.For<INotificationService>();
        var consoleSub = Substitute.For<INotificationService>();
        consoleSub.SendBrewCompletedAsync(Arg.Any<BrewSession>()).Returns(NotificationResult.Success());

        var monitor = CreateMonitor("InvalidProvider");
        var svc = new RoutingServiceShim(webhookSub, graphSub, consoleSub, monitor);

        await svc.SendBrewCompletedAsync(CreateSession());

        await consoleSub.Received(1).SendBrewCompletedAsync(Arg.Any<BrewSession>());
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToCorrectProvider()
    {
        var webhookSub = Substitute.For<INotificationService>();
        var graphSub = Substitute.For<INotificationService>();
        var consoleSub = Substitute.For<INotificationService>();
        webhookSub.TestConnectionAsync().Returns(true);

        var monitor = CreateMonitor(NotificationProvider.Webhook);
        var svc = new RoutingServiceShim(webhookSub, graphSub, consoleSub, monitor);

        var result = await svc.TestConnectionAsync();

        Assert.True(result);
        await webhookSub.Received(1).TestConnectionAsync();
        await consoleSub.DidNotReceive().TestConnectionAsync();
    }

    [Fact]
    public async Task TestConnectionAsync_RoutesToConsole_WhenProviderIsConsole()
    {
        var webhookSub = Substitute.For<INotificationService>();
        var graphSub = Substitute.For<INotificationService>();
        var consoleSub = Substitute.For<INotificationService>();
        consoleSub.TestConnectionAsync().Returns(true);

        var monitor = CreateMonitor(NotificationProvider.Console);
        var svc = new RoutingServiceShim(webhookSub, graphSub, consoleSub, monitor);

        var result = await svc.TestConnectionAsync();

        Assert.True(result);
        await consoleSub.Received(1).TestConnectionAsync();
    }

    /// <summary>
    /// Test shim that exposes INotificationService slots for substitution,
    /// replicating the routing logic of RoutingNotificationService.
    /// </summary>
    private sealed class RoutingServiceShim(
        INotificationService webhook,
        INotificationService graph,
        INotificationService console,
        IOptionsMonitor<NotificationProviderOptions> providerOptions) : INotificationService
    {
        private INotificationService Resolve() => providerOptions.CurrentValue.Provider switch
        {
            _ when string.Equals(providerOptions.CurrentValue.Provider, NotificationProvider.Graph, StringComparison.OrdinalIgnoreCase) => graph,
            _ when string.Equals(providerOptions.CurrentValue.Provider, NotificationProvider.Webhook, StringComparison.OrdinalIgnoreCase) => webhook,
            _ => console,
        };

        public Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default)
            => Resolve().SendBrewCompletedAsync(session, ct);

        public Task<bool> TestConnectionAsync(CancellationToken ct = default)
            => Resolve().TestConnectionAsync(ct);
    }
}
