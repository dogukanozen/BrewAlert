using System;
using System.Globalization;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Notifications;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsGraphMessageBuilderTests
{
    [Fact]
    public void BuildBrewCompletedPayload_ReturnsValidJsonStructure()
    {
        // Arrange
        var profile = new BrewProfile
        {
            Name = "Green Tea",
            Icon = "🍵",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(3)
        };
        var session = new BrewSession
        {
            Profile = profile,
            StartedAtUtc = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var json = TeamsGraphMessageBuilder.BuildBrewCompletedPayload(session);

        // Assert
        Assert.Contains("\"contentType\":\"html\"", json);
        Assert.Contains("brewalert-complete", json);
        Assert.Contains("Green Tea", json);
        // Look for parts of the escape sequence to avoid C# string literal escaping hell
        Assert.Contains("uD83C", json); 
        Assert.Contains("Tea", json);
        Assert.Contains("3 min", json);
        
        var expectedTime = session.StartedAtUtc.Add(profile.BrewDuration).ToString("HH:mm", CultureInfo.InvariantCulture);
        Assert.Contains(expectedTime, json);
    }

    [Fact]
    public void BuildTestPayload_ReturnsValidJsonStructure()
    {
        // Act
        var json = TeamsGraphMessageBuilder.BuildTestPayload();

        // Assert
        Assert.Contains("Connection Test Successful!", json);
        Assert.Contains("application/vnd.microsoft.card.adaptive", json);
    }

    [Fact]
    public void BuildBrewCompletedPayload_EscapesSpecialCharacters()
    {
        // Arrange
        var profile = new BrewProfile
        {
            Name = "Special \"Brew\" \\ Tea",
            Icon = "🍵",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(3)
        };
        var session = new BrewSession { Profile = profile };

        // Act
        var json = TeamsGraphMessageBuilder.BuildBrewCompletedPayload(session);

        // Assert
        // Check for specific substrings that should be present after double escaping
        Assert.Contains("Brew", json);
        Assert.Contains("Tea", json);
        // Look for the escaped quotes in the nested JSON
        Assert.Contains(@"\\\""", json);
    }
}
