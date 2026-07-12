namespace BrewAlert.TeamsBot;

using System.Text.Json;
using Microsoft.Teams.Cards;

public static class AdaptiveCardPayload
{
    public static bool TryDeserialize(string json, out AdaptiveCard? card)
    {
        card = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "AdaptiveCard", StringComparison.Ordinal) ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(version.GetString()) ||
                !root.TryGetProperty("body", out var body) ||
                body.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            card = AdaptiveCard.Deserialize(json);
            return card is not null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }
}
