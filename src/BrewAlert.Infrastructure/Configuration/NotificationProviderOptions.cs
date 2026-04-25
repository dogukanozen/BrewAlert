namespace BrewAlert.Infrastructure.Configuration;

/// <summary>
/// Selects which notification back-end is active.
/// Bound from "BrewAlert:Notifications" (same section as Teams/TeamsGraph options).
/// Override via %AppData%\BrewAlert\preferences.json or env var
/// BREWALERT__NOTIFICATIONS__PROVIDER.
/// </summary>
public sealed class NotificationProviderOptions
{
    /// <summary>Section path shared with Teams/TeamsGraph options.</summary>
    public const string SectionPath = "BrewAlert:Notifications";

    /// <summary>Active back-end: "Graph" | "Webhook" | "Console" (default).</summary>
    public string Provider { get; set; } = NotificationProvider.Console;
}

/// <summary>Well-known provider name constants.</summary>
public static class NotificationProvider
{
    public const string Graph = "Graph";
    public const string Webhook = "Webhook";
    public const string Console = "Console";
}
