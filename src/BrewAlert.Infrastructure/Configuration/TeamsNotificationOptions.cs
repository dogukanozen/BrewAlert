namespace BrewAlert.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed options for Teams webhook notifications.
/// Bound from appsettings.json section "BrewAlert:Notifications:Teams".
/// </summary>
public sealed class TeamsNotificationOptions
{
    public const string SectionPath = "BrewAlert:Notifications:Teams";

    public string WebhookUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }

    /// <summary>Timeout in seconds for the HTTP request to Teams.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
