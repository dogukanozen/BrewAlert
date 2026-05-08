namespace BrewAlert.UI.Services;

public record BrewNotificationResult(Guid SessionId, bool IsSuccess, string? ErrorMessage = null);

/// <summary>
/// Singleton coordinator that sends exactly one Teams notification per brew session.
/// Decoupled from the timer screen so notifications fire even when the view is not visible.
/// </summary>
public interface IBrewCompletionNotificationService
{
    /// <summary>Raised on the calling thread when notification delivery finishes (success or failure).</summary>
    event EventHandler<BrewNotificationResult>? NotificationCompleted;
}
