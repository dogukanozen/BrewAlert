namespace BrewAlert.Infrastructure.Notifications;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Sends brew notifications to a Microsoft Teams chat via Microsoft Graph API.
/// Uses client-credentials OAuth flow; token is cached until 60 seconds before expiry.
/// </summary>
public sealed class TeamsGraphNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TeamsGraphOptions> options,
    ILogger<TeamsGraphNotifier> logger) : INotificationService
{
    private readonly TeamsGraphOptions _options = options.Value;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public async Task<NotificationResult> SendBrewCompletedAsync(BrewSession session, CancellationToken ct = default)
    {
        if (!IsConfigured(out var missing))
            return NotificationResult.Failure($"TeamsGraph not fully configured. Missing: {missing}");

        try
        {
            var token = await GetAccessTokenAsync(ct);
            var payload = TeamsGraphMessageBuilder.BuildBrewCompletedPayload(session);
            var result = await PostToChatAsync(token, payload, ct);

            if (result.IsSuccess)
                logger.LogInformation("Teams Graph notification sent for session {SessionId}.", session.Id);

            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "HTTP error sending Teams Graph notification for session {SessionId}.", session.Id);
            return NotificationResult.Failure($"HTTP error: {ex.Message}");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (!IsConfigured(out _))
            return false;

        try
        {
            var token = await GetAccessTokenAsync(ct);
            var payload = TeamsGraphMessageBuilder.BuildTestPayload();
            var result = await PostToChatAsync(token, payload, ct);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Teams Graph connection test failed.");
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // Serve cached token if still valid (with 60s buffer)
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        var tokenUrl = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

        var body = new FormUrlEncodedContent([
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId),
            new("client_secret", _options.ClientSecret),
            new("scope", "https://graph.microsoft.com/.default"),
        ]);

        var client = httpClientFactory.CreateClient(nameof(TeamsGraphNotifier));
        var response = await client.PostAsync(tokenUrl, body, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Token request failed {StatusCode}: {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Token request failed: {(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing from response.");
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        _cachedToken = accessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

        return _cachedToken;
    }

    private async Task<NotificationResult> PostToChatAsync(string token, string payload, CancellationToken ct)
    {
        var url = $"https://graph.microsoft.com/v1.0/chats/{_options.ChatId}/messages";
        var content = new StringContent(payload, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var client = httpClientFactory.CreateClient(nameof(TeamsGraphNotifier));
        var response = await client.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
            return NotificationResult.Success();

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogError("Graph API returned {StatusCode}: {Body}", response.StatusCode, body);
        return NotificationResult.Failure($"Graph API returned {(int)response.StatusCode}: {body}");
    }

    private bool IsConfigured(out string missing)
    {
        var gaps = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.TenantId)) gaps.Add("TenantId");
        if (string.IsNullOrWhiteSpace(_options.ClientId)) gaps.Add("ClientId");
        if (string.IsNullOrWhiteSpace(_options.ClientSecret)) gaps.Add("ClientSecret");
        if (string.IsNullOrWhiteSpace(_options.ChatId)) gaps.Add("ChatId");
        missing = string.Join(", ", gaps);
        return gaps.Count == 0;
    }
}
