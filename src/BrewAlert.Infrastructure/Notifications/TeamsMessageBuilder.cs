namespace BrewAlert.Infrastructure.Notifications;

using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrewAlert.Core.Models;

/// <summary>
/// Builds JSON payloads for Teams Incoming Webhook messages.
/// Uses typed DTOs + System.Text.Json serialization instead of raw string interpolation.
/// Teams notifications are always in English.
/// </summary>
public static class TeamsMessageBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string BuildBrewCompletedPayload(BrewSession session)
    {
        var duration = session.Profile.BrewDuration.TotalMinutes >= 1
            ? $"{session.Profile.BrewDuration.TotalMinutes:F0} min"
            : $"{session.Profile.BrewDuration.TotalSeconds:F0} sec";

        var completedAt = session.StartedAtUtc
            .Add(session.Profile.BrewDuration)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

        var card = new AdaptiveCard
        {
            Body =
            [
                new TextBlock
                {
                    Text = $"{session.Profile.Icon} BrewAlert — Your {session.Profile.Type} is Ready!",
                    Weight = "Bolder",
                    Size = "Large",
                    Color = "Good",
                },
                new FactSetBlock
                {
                    Facts =
                    [
                        new Fact("Profile", session.Profile.Name),
                        new Fact("Brew Time", duration),
                        new Fact("Completed At", $"{completedAt} (UTC)"),
                    ],
                },
                new TextBlock
                {
                    Text = "Don't let it get cold! ☕",
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

    public static string BuildTestPayload()
    {
        var card = new AdaptiveCard
        {
            Body =
            [
                new TextBlock
                {
                    Text = "✅ BrewAlert — Connection Test Successful!",
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
