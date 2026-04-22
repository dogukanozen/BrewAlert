namespace BrewAlert.Infrastructure.Notifications;

using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fallback notifier that writes to the console / debug output.
/// Useful during development when Teams webhook is not configured.
/// </summary>
public sealed class ConsoleNotifier(ILogger<ConsoleNotifier> logger) : INotificationService
{
    public Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default)
    {
        var message = $"[BrewAlert] {session.Profile.Icon} {session.Profile.Name} is ready! " +
                      $"(Brewed for {session.Profile.BrewDuration.TotalMinutes:F0} min)";

        logger.LogInformation("{Message}", message);
        Console.WriteLine(message);

        return Task.FromResult(NotificationResult.Success());
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[BrewAlert] Console notifier connection test — always succeeds.");
        return Task.FromResult(true);
    }
}
