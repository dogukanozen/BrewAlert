namespace BrewAlert.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed options for Teams webhook notifications.
/// Bound from appsettings.json section "BrewAlert:Notifications:Teams".
/// </summary>
public sealed class TeamsNotificationOptions
{
    public const string SectionPath = "BrewAlert:Notifications:Teams";

    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional key sent as <c>X-BrewAlert-Key</c>. Leave empty for Power Automate
    /// webhook URLs; configure it when posting to BrewAlert.TeamsBot.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Timeout in seconds for the HTTP request to Teams.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
