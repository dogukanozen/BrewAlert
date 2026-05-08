namespace BrewAlert.Infrastructure.Notifications;

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using BrewAlert.Core.Models;

/// <summary>
/// Builds JSON payloads for the Graph API POST /chats/{chatId}/messages endpoint.
/// The adaptive card content is embedded as a serialized string inside the attachment.
/// Card text is localized via <see cref="TeamsCardStrings"/>.
/// </summary>
public static class TeamsGraphMessageBuilder
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string BuildBrewCompletedPayload(BrewSession session, string language)
    {
        ArgumentNullException.ThrowIfNull(session);

        var minShort = TeamsCardStrings.Get(language, "MinShort");
        var secShort = TeamsCardStrings.Get(language, "SecShort");
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} {minShort}"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} {secShort}";

        var endsAt = session.EndsAtUtc == DateTime.MinValue
            ? session.StartedAtUtc.Add(session.Profile.BrewDuration)
            : session.EndsAtUtc;
        var completedAt = endsAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        var typeLabel = TeamsCardStrings.Get(language, $"BrewType_{session.Profile.Type}");
        var headerTemplate = TeamsCardStrings.Get(language, "HeaderReady");
        var headerText = string.Format(CultureInfo.InvariantCulture, headerTemplate, session.Profile.Icon, typeLabel);

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
                    text = headerText,
                    weight = "Bolder",
                    size = "Large",
                    color = "Good"
                },
                new
                {
                    type = "FactSet",
                    facts = new object[]
                    {
                        new { title = TeamsCardStrings.Get(language, "FactProfile"), value = session.Profile.Name },
                        new { title = TeamsCardStrings.Get(language, "FactBrewTime"), value = duration },
                        new { title = TeamsCardStrings.Get(language, "FactCompletedAt"), value = completedAt }
                    }
                },
                new
                {
                    type = "TextBlock",
                    text = TeamsCardStrings.Get(language, "FooterDontLetItGetCold"),
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

    public static string BuildTestPayload(string language)
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
                    text = TeamsCardStrings.Get(language, "TestSuccess"),
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
