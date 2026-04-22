namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Models;

/// <summary>
/// Sends notifications when a brew session completes.
/// Implementations may target Teams, Slack, console, etc.
/// </summary>
public interface INotificationService
{
    /// <summary>Send a brew completion notification.</summary>
    Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default);

    /// <summary>Verify that the notification channel is properly configured and reachable.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
