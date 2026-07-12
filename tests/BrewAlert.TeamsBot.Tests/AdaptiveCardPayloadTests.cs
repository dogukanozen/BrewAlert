using BrewAlert.TeamsBot;
using Xunit;

namespace BrewAlert.TeamsBot.Tests;

public sealed class AdaptiveCardPayloadTests
{
    [Fact]
    public void TryDeserialize_WhenPayloadIsAdaptiveCard_ReturnsTrue()
    {
        const string json = """
            {
              "type": "AdaptiveCard",
              "version": "1.4",
              "body": []
            }
            """;

        var result = AdaptiveCardPayload.TryDeserialize(json, out var card);

        Assert.True(result);
        Assert.NotNull(card);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"MessageCard\",\"version\":\"1.4\",\"body\":[]}")]
    [InlineData("{\"type\":\"AdaptiveCard\",\"version\":\"1.4\",\"body\":{}}")]
    public void TryDeserialize_WhenPayloadIsNotAdaptiveCard_ReturnsFalse(string json)
    {
        var result = AdaptiveCardPayload.TryDeserialize(json, out var card);

        Assert.False(result);
        Assert.Null(card);
    }
}
