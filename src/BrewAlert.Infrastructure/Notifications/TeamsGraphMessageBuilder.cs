namespace BrewAlert.Infrastructure.Notifications;

using BrewAlert.Core.Models;

/// <summary>
/// Builds JSON payloads for the Graph API POST /chats/{chatId}/messages endpoint.
/// The adaptive card content is embedded as a serialized string inside the attachment.
/// </summary>
public static class TeamsGraphMessageBuilder
{
    public static string BuildBrewCompletedPayload(BrewSession session)
    {
        var profileName = EscapeJson(session.Profile.Name);
        var icon = EscapeJson(session.Profile.Icon);
        var brewType = session.Profile.Type.ToString();
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} min"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} sec";
        var completedAt = session.StartedAtUtc.Add(session.Profile.BrewDuration).ToString("HH:mm");

        // Adaptive card as an escaped JSON string (Graph API requirement for attachment content)
        var cardContent = $$"""
            {\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"type\":\"AdaptiveCard\",\"version\":\"1.4\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"{{icon}} BrewAlert — Your {{brewType}} is Ready!\",\"weight\":\"Bolder\",\"size\":\"Large\",\"color\":\"Good\"},{\"type\":\"FactSet\",\"facts\":[{\"title\":\"Profile\",\"value\":\"{{profileName}}\"},{\"title\":\"Brew Time\",\"value\":\"{{duration}}\"},{\"title\":\"Completed At\",\"value\":\"{{completedAt}} (UTC)\"}]},{\"type\":\"TextBlock\",\"text\":\"Don't let it get cold! ☕\",\"wrap\":true,\"isSubtle\":true}]}
            """.Trim();

        return $$"""
        {
            "body": {
                "contentType": "html",
                "content": "<attachment id=\"brewalert-complete\"></attachment>"
            },
            "attachments": [
                {
                    "id": "brewalert-complete",
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": "{{cardContent}}"
                }
            ]
        }
        """;
    }

    public static string BuildTestPayload()
    {
        var cardContent = """
            {\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"type\":\"AdaptiveCard\",\"version\":\"1.4\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"✅ BrewAlert — Connection Test Successful!\",\"weight\":\"Bolder\",\"color\":\"Good\"}]}
            """.Trim();

        return $$"""
        {
            "body": {
                "contentType": "html",
                "content": "<attachment id=\"brewalert-test\"></attachment>"
            },
            "attachments": [
                {
                    "id": "brewalert-test",
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": "{{cardContent}}"
                }
            ]
        }
        """;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
