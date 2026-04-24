namespace BrewAlert.Infrastructure.Notifications;

using BrewAlert.Core.Models;

/// <summary>
/// Builds Adaptive Card JSON payloads for Teams notifications via Power Automate.
/// Payload is the card body only — wrap with "Post a card" action in Power Automate.
/// </summary>
public static class TeamsMessageBuilder
{
    public static string BuildBrewCompletedPayload(BrewSession session)
    {
        var profileName = EscapeJson(session.Profile.Name);
        var icon = EscapeJson(session.Profile.Icon);
        var brewType = session.Profile.Type.ToString();
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} dk"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} sn";
        var completedAt = session.StartedAtUtc.Add(session.Profile.BrewDuration).ToString("HH:mm");

        return $$"""
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.4",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "{{icon}} BrewAlert — {{brewType}} Hazır!",
                    "weight": "Bolder",
                    "size": "Large",
                    "color": "Good"
                },
                {
                    "type": "FactSet",
                    "facts": [
                        { "title": "Profil", "value": "{{profileName}}" },
                        { "title": "Demleme Süresi", "value": "{{duration}}" },
                        { "title": "Tamamlandı", "value": "{{completedAt}} (UTC)" }
                    ]
                },
                {
                    "type": "TextBlock",
                    "text": "Soğumadan iç! ☕",
                    "wrap": true,
                    "isSubtle": true
                }
            ]
        }
        """;
    }

    public static string BuildTestPayload() => """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.4",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "✅ BrewAlert — Bağlantı testi başarılı!",
                    "weight": "Bolder",
                    "color": "Good"
                }
            ]
        }
        """;

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
