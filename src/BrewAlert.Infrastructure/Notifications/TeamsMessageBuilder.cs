namespace BrewAlert.Infrastructure.Notifications;

using BrewAlert.Core.Models;

/// <summary>
/// Builds the JSON payload for Teams Incoming Webhook messages.
/// Uses Adaptive Card format for rich notifications.
/// </summary>
public static class TeamsMessageBuilder
{
    /// <summary>
    /// Creates an Adaptive Card JSON payload for a brew completion notification.
    /// </summary>
    public static string BuildBrewCompletedPayload(BrewSession session)
    {
        var profileName = EscapeJson(session.Profile.Name);
        var icon = EscapeJson(session.Profile.Icon);
        var brewType = session.Profile.Type.ToString();
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} min"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} sec";
        var completedAt = session.StartedAtUtc.Add(session.Profile.BrewDuration).ToString("HH:mm");

        return $$"""
        {
            "type": "message",
            "attachments": [
                {
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": {
                        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                        "type": "AdaptiveCard",
                        "version": "1.4",
                        "body": [
                            {
                                "type": "TextBlock",
                                "text": "{{icon}} BrewAlert — Your {{brewType}} is Ready!",
                                "weight": "Bolder",
                                "size": "Large",
                                "color": "Good"
                            },
                            {
                                "type": "FactSet",
                                "facts": [
                                    { "title": "Profile", "value": "{{profileName}}" },
                                    { "title": "Brew Time", "value": "{{duration}}" },
                                    { "title": "Completed At", "value": "{{completedAt}} (UTC)" }
                                ]
                            },
                            {
                                "type": "TextBlock",
                                "text": "Don't let it get cold! ☕",
                                "wrap": true,
                                "isSubtle": true
                            }
                        ]
                    }
                }
            ]
        }
        """;
    }

    /// <summary>
    /// Creates a simple test/ping payload for connection validation.
    /// </summary>
    public static string BuildTestPayload()
    {
        return """
        {
            "type": "message",
            "attachments": [
                {
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": {
                        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                        "type": "AdaptiveCard",
                        "version": "1.4",
                        "body": [
                            {
                                "type": "TextBlock",
                                "text": "✅ BrewAlert — Connection Test Successful!",
                                "weight": "Bolder",
                                "color": "Good"
                            }
                        ]
                    }
                }
            ]
        }
        """;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
