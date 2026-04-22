namespace BrewAlert.Core.Models;

/// <summary>
/// Outcome of a notification delivery attempt.
/// </summary>
public sealed class NotificationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime SentAtUtc { get; init; } = DateTime.UtcNow;

    public static NotificationResult Success() => new() { IsSuccess = true };
    public static NotificationResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
