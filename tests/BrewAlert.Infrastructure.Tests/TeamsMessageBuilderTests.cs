using System.Text.Json;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Notifications;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsMessageBuilderTests
{
    private static BrewSession MakeSession(string name, BrewType type, TimeSpan duration, string icon = "🍵")
        => new()
        {
            Profile = new BrewProfile { Name = name, Type = type, BrewDuration = duration, Icon = icon },
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.Add(duration),
        };

    [Fact]
    public void BuildBrewCompletedPayload_ShouldContainProfileName()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Turkish Tea", BrewType.Tea, TimeSpan.FromMinutes(8)));

        Assert.Contains("Turkish Tea", payload);
        Assert.Contains("Tea", payload);
        Assert.Contains("8 min", payload);
        Assert.Contains("AdaptiveCard", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldHandleShortDuration()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Espresso", BrewType.Coffee, TimeSpan.FromSeconds(25), "☕"));

        Assert.Contains("25 sec", payload);
    }

    [Fact]
    public void BuildTestPayload_ShouldContainSuccessMessage()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload();

        Assert.Contains("Connection Test Successful", payload);
        Assert.Contains("AdaptiveCard", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldHandleSpecialCharactersWithoutThrowingOrBreakingJson()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Test \"Special\" Brew", BrewType.Custom, TimeSpan.FromMinutes(5)));

        // Outer payload must be valid JSON
        var ex = Record.Exception(() => JsonDocument.Parse(payload));
        Assert.Null(ex);
        // Name appears somewhere in the output (may be escaped inside the nested card string)
        Assert.Contains("Special", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldProduceValidOuterJson()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Green Tea", BrewType.Tea, TimeSpan.FromMinutes(3)));

        using var doc = JsonDocument.Parse(payload);
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("attachments").ValueKind);
        // The attachment content is a serialized string (nested JSON)
        var contentStr = doc.RootElement.GetProperty("attachments")[0].GetProperty("content").GetString();
        Assert.NotNull(contentStr);
        Assert.Contains("AdaptiveCard", contentStr);
    }

    [Fact]
    public void BuildTestPayload_ShouldProduceValidOuterJson()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload();

        using var doc = JsonDocument.Parse(payload);
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
        var contentStr = doc.RootElement.GetProperty("attachments")[0].GetProperty("content").GetString();
        Assert.NotNull(contentStr);
        Assert.Contains("AdaptiveCard", contentStr);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldEscapeBackslashesWithoutThrowingOrBreakingJson()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession(@"Brew\Test", BrewType.Custom, TimeSpan.FromMinutes(2)));

        var ex = Record.Exception(() => JsonDocument.Parse(payload));
        Assert.Null(ex);
    }
}
