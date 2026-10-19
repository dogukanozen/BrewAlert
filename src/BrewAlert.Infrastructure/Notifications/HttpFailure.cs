namespace BrewAlert.Infrastructure.Notifications;

using System.Net;
using BrewAlert.Core.Models;

/// <summary>
/// Maps a failed HTTP delivery to a <see cref="NotificationResult"/>, marking the failures
/// a later retry can fix (network drop, timeout, 408, 429, 5xx) as transient.
/// </summary>
internal static class HttpFailure
{
    public static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    public static NotificationResult FromStatus(HttpStatusCode status, string message) =>
        IsTransient(status) ? NotificationResult.TransientFailure(message) : NotificationResult.Failure(message);

    public static NotificationResult FromException(Exception ex, string message) =>
        ex is HttpRequestException { StatusCode: { } status } && !IsTransient(status)
            ? NotificationResult.Failure(message)
            : NotificationResult.TransientFailure(message);
}
