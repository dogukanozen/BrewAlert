namespace BrewAlert.Infrastructure.Notifications;

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using BrewAlert.Core.Models;

/// <summary>
/// Builds JSON payloads for the Graph API POST /chats/{chatId}/messages endpoint.
/// The adaptive card content is embedded as a serialized string inside the attachment.
/// </summary>
public static class TeamsGraphMessageBuilder
{
    private static readonly JsonSerializerOptions Options = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string BuildBrewCompletedPayload(BrewSession session)
    {
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} min"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} sec";
        
        var completedAt = session.StartedAtUtc.Add(session.Profile.BrewDuration).ToString("HH:mm", CultureInfo.InvariantCulture);

        var cardContentObj = new Dictionary<string, object>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["body"] = new object[]
            {
                new 
                {
                    type = "TextBlock",
                    text = $"{session.Profile.Icon} BrewAlert — Your {session.Profile.Type} is Ready!",
                    weight = "Bolder",
                    size = "Large",
                    color = "Good"
                },
                new
                {
                    type = "FactSet",
                    facts = new object[]
                    {
                        new { title = "Profile", value = session.Profile.Name },
                        new { title = "Brew Time", value = duration },
                        new { title = "Completed At", value = $"{completedAt} (UTC)" }
                    }
                },
                new
                {
                    type = "TextBlock",
                    text = "Don't let it get cold! ☕",
                    wrap = true,
                    isSubtle = true
                }
            }
        };

        var cardContentString = JsonSerializer.Serialize(cardContentObj, Options);

        var payload = new
        {
            body = new
            {
                contentType = "html",
                content = "<attachment id=\"brewalert-complete\"></attachment>"
            },
            attachments = new[]
            {
                new
                {
                    id = "brewalert-complete",
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = cardContentString
                }
            }
        };

        return JsonSerializer.Serialize(payload, Options);
    }

    public static string BuildTestPayload()
    {
        var cardContentObj = new Dictionary<string, object>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["body"] = new object[]
            {
                new 
                {
                    type = "TextBlock",
                    text = "✅ BrewAlert — Connection Test Successful!",
                    weight = "Bolder",
                    color = "Good"
                }
            }
        };

        var cardContentString = JsonSerializer.Serialize(cardContentObj, Options);

        var payload = new
        {
            body = new
            {
                contentType = "html",
                content = "<attachment id=\"brewalert-test\"></attachment>"
            },
            attachments = new[]
            {
                new
                {
                    id = "brewalert-test",
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = cardContentString
                }
            }
        };

        return JsonSerializer.Serialize(payload, Options);
    }
}
