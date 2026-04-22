using System.Text.Json;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Notifications;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsMessageBuilderTests
{
    [Fact]
    public void BuildBrewCompletedPayload_ShouldContainProfileName()
    {
        var session = new BrewSession
        {
            Profile = new BrewProfile
            {
                Name = "Turkish Tea",
                Type = BrewType.Tea,
                BrewDuration = TimeSpan.FromMinutes(8),
                Icon = "🍵"
            },
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-8),
            EndsAtUtc = DateTime.UtcNow,
            Remaining = TimeSpan.Zero,
            State = BrewSessionState.Completed
        };

        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);

        Assert.Contains("Turkish Tea", payload);
        Assert.Contains("Tea", payload);
        Assert.Contains("8 min", payload);
        Assert.Contains("AdaptiveCard", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldHandleShortDuration()
    {
        var session = new BrewSession
        {
            Profile = new BrewProfile
            {
                Name = "Espresso",
                Type = BrewType.Coffee,
                BrewDuration = TimeSpan.FromSeconds(25),
                Icon = "☕"
            },
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddSeconds(25),
        };

        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);

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
    public void BuildBrewCompletedPayload_ShouldEscapeSpecialCharacters()
    {
        var session = new BrewSession
        {
            Profile = new BrewProfile
            {
                Name = "Test \"Special\" Brew",
                Type = BrewType.Custom,
                BrewDuration = TimeSpan.FromMinutes(5),
            },
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);

        Assert.DoesNotContain("\"Special\"", payload);
        Assert.Contains("\\\"Special\\\"", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldProduceValidJson()
    {
        var session = new BrewSession
        {
            Profile = new BrewProfile
            {
                Name = "Green Tea",
                Type = BrewType.Tea,
                BrewDuration = TimeSpan.FromMinutes(3),
                Icon = "🍵"
            },
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddMinutes(3),
        };

        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);

        var doc = JsonDocument.Parse(payload);
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("attachments").ValueKind);
    }

    [Fact]
    public void BuildTestPayload_ShouldProduceValidJson()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload();

        var doc = JsonDocument.Parse(payload);
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldEscapeBackslashes()
    {
        var session = new BrewSession
        {
            Profile = new BrewProfile
            {
                Name = @"Brew\Test",
                Type = BrewType.Custom,
                BrewDuration = TimeSpan.FromMinutes(2),
            },
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddMinutes(2),
        };

        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(session);

        var ex = Record.Exception(() => JsonDocument.Parse(payload));
        Assert.Null(ex);
    }
}
