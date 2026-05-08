namespace BrewAlert.Infrastructure.Notifications;

using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrewAlert.Core.Models;

/// <summary>
/// Builds JSON payloads for Teams Incoming Webhook messages.
/// Card text is localized via <see cref="TeamsCardStrings"/>; callers pass the
/// active UI language (typically <c>LanguageOptions.Language</c>).
/// </summary>
public static class TeamsMessageBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
        var completedAt = endsAt
            .ToLocalTime()
            .ToString("HH:mm", CultureInfo.InvariantCulture);

        var typeLabel = TeamsCardStrings.Get(language, $"BrewType_{session.Profile.Type}");
        var headerTemplate = TeamsCardStrings.Get(language, "HeaderReady");
        var headerText = string.Format(CultureInfo.InvariantCulture, headerTemplate, session.Profile.Icon, typeLabel);

        var card = new AdaptiveCard
        {
            Body =
            [
                new TextBlock
                {
                    Text = headerText,
                    Weight = "Bolder",
                    Size = "Large",
                    Color = "Good",
                },
                new FactSetBlock
                {
                    Facts =
                    [
                        new Fact(TeamsCardStrings.Get(language, "FactProfile"), session.Profile.Name),
                        new Fact(TeamsCardStrings.Get(language, "FactBrewTime"), duration),
                        new Fact(TeamsCardStrings.Get(language, "FactCompletedAt"), completedAt),
                    ],
                },
                new TextBlock
                {
                    Text = TeamsCardStrings.Get(language, "FooterDontLetItGetCold"),
                    Wrap = true,
                    IsSubtle = true,
                },
            ],
        };

        // Return the card as the root payload. The Power Automate flow uses
        // triggerBody() as the Adaptive Card field, so the HTTP body must BE
        // the card — not wrapped in an attachments envelope.
        return JsonSerializer.Serialize(card, JsonOptions);
    }

    public static string BuildTestPayload(string language)
    {
        var card = new AdaptiveCard
        {
            Body =
            [
                new TextBlock
                {
                    Text = TeamsCardStrings.Get(language, "TestSuccess"),
                    Weight = "Bolder",
                    Color = "Good",
                },
            ],
        };

        return JsonSerializer.Serialize(card, JsonOptions);
    }

    private sealed class AdaptiveCard
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; init; } = "http://adaptivecards.io/schemas/adaptive-card.json";
        public string Type { get; init; } = "AdaptiveCard";
        public string Version { get; init; } = "1.4";
        public List<object> Body { get; init; } = [];
    }

    private sealed class TextBlock
    {
        public string Type { get; init; } = "TextBlock";
        public string Text { get; init; } = string.Empty;
        public string? Weight { get; init; }
        public string? Size { get; init; }
        public string? Color { get; init; }
        public bool? Wrap { get; init; }
        public bool? IsSubtle { get; init; }
    }

    private sealed class FactSetBlock
    {
        public string Type { get; init; } = "FactSet";
        public List<Fact> Facts { get; init; } = [];
    }

    private sealed record Fact(string Title, string Value);
}
