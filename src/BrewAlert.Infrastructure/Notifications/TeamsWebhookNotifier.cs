namespace BrewAlert.Infrastructure.Notifications;

using System.Net.Http.Headers;
using System.Text;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Sends brew notifications to Microsoft Teams via Incoming Webhook.
/// </summary>
public sealed class TeamsWebhookNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TeamsNotificationOptions> options,
    ILogger<TeamsWebhookNotifier> logger) : INotificationService
{
    private readonly TeamsNotificationOptions _options = options.Value;

    public async Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            logger.LogWarning("Teams webhook URL is not configured.");
            return NotificationResult.Failure("Webhook URL is not configured.");
        }

        try
        {
            var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);
            var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var client = httpClientFactory.CreateClient(nameof(TeamsWebhookNotifier));
            var response = await client.PostAsync(_options.WebhookUrl, content, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Teams notification sent successfully for session {SessionId}.", session.Id);
                return NotificationResult.Success();
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Teams webhook returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            return NotificationResult.Failure($"Teams returned {(int)response.StatusCode}: {responseBody}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to send Teams notification for session {SessionId}.", session.Id);
            return NotificationResult.Failure($"HTTP error: {ex.Message}");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
            return false;

        try
        {
            var payload = TeamsMessageBuilder.BuildTestPayload();
            var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var client = httpClientFactory.CreateClient(nameof(TeamsWebhookNotifier));
            var response = await client.PostAsync(_options.WebhookUrl, content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Teams connection test failed.");
            return false;
        }
    }
}
