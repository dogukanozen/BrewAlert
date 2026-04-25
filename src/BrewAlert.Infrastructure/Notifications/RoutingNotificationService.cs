using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrewAlert.Infrastructure.Notifications;

/// <summary>
/// Singleton INotificationService that routes calls to the currently selected
/// back-end (Graph | Webhook | Console) without requiring a restart.
/// The active provider is read from IOptionsMonitor on every call so that
/// a write to preferences.json takes effect immediately.
/// </summary>
public sealed class RoutingNotificationService(
    TeamsWebhookNotifier webhookNotifier,
    TeamsGraphNotifier graphNotifier,
    ConsoleNotifier consoleNotifier,
    IOptionsMonitor<NotificationProviderOptions> providerOptions,
    ILogger<RoutingNotificationService> logger) : INotificationService
{
    public Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default)
    {
        var notifier = Resolve();
        return notifier.SendBrewCompletedAsync(session, ct);
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var notifier = Resolve();
        return notifier.TestConnectionAsync(ct);
    }

    private INotificationService Resolve()
    {
        var provider = providerOptions.CurrentValue.Provider;
        logger.LogDebug("Resolving notification back-end: {Provider}", provider);

        return provider?.ToLowerInvariant() switch
        {
            "graph" => graphNotifier,
            "webhook" => webhookNotifier,
            _ => consoleNotifier,
        };
    }
}
