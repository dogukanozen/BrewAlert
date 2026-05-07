using System.Text.Json;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Notifications;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsMessageBuilderTests
{
    private const string English = "English";
    private const string Turkish = "Turkish";

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
            MakeSession("Turkish Tea", BrewType.Tea, TimeSpan.FromMinutes(8)), English);

        Assert.Contains("Turkish Tea", payload);
        Assert.Contains("Tea", payload);
        Assert.Contains("8 min", payload);
        Assert.Contains("AdaptiveCard", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldHandleShortDuration()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Espresso", BrewType.Coffee, TimeSpan.FromSeconds(25), "☕"), English);

        Assert.Contains("25 sec", payload);
    }

    [Fact]
    public void BuildTestPayload_ShouldContainSuccessMessage()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload(English);

        Assert.Contains("Connection Test Successful", payload);
        Assert.Contains("AdaptiveCard", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldHandleSpecialCharactersWithoutThrowingOrBreakingJson()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Test \"Special\" Brew", BrewType.Custom, TimeSpan.FromMinutes(5)), English);

        // Outer payload must be valid JSON
        var ex = Record.Exception(() => JsonDocument.Parse(payload));
        Assert.Null(ex);
        // Name appears somewhere in the output (may be escaped inside the nested card string)
        Assert.Contains("Special", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldProduceCardAtRoot()
    {
        // Power Automate flow uses triggerBody() as the Adaptive Card field,
        // so the HTTP body must be the card object itself — not an attachments envelope.
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Green Tea", BrewType.Tea, TimeSpan.FromMinutes(3)), English);

        using var doc = JsonDocument.Parse(payload);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("body").ValueKind);
    }

    [Fact]
    public void BuildTestPayload_ShouldProduceCardAtRoot()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload(English);

        using var doc = JsonDocument.Parse(payload);
        Assert.Equal("AdaptiveCard", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("body").ValueKind);
    }

    [Fact]
    public void BuildBrewCompletedPayload_ShouldEscapeBackslashesWithoutThrowingOrBreakingJson()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession(@"Brew\Test", BrewType.Custom, TimeSpan.FromMinutes(2)), English);

        var ex = Record.Exception(() => JsonDocument.Parse(payload));
        Assert.Null(ex);
    }

    [Fact]
    public void BuildBrewCompletedPayload_Turkish_ContainsTurkishStrings()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Yeşil Çay", BrewType.Tea, TimeSpan.FromMinutes(3)), Turkish);

        Assert.Contains("hazır", payload);
        Assert.Contains("Demleme Süresi", payload);
        Assert.Contains("Tamamlanma", payload);
        Assert.Contains("3 dk", payload);
        Assert.Contains("Soğutmayın", payload);
        Assert.Contains("Çay", payload);
    }

    [Fact]
    public void BuildBrewCompletedPayload_Turkish_ShortDurationUsesTurkishUnit()
    {
        var payload = TeamsMessageBuilder.BuildBrewCompletedPayload(
            MakeSession("Espresso", BrewType.Coffee, TimeSpan.FromSeconds(30), "☕"), Turkish);

        Assert.Contains("30 sn", payload);
    }

    [Fact]
    public void BuildTestPayload_Turkish_ContainsTurkishSuccessMessage()
    {
        var payload = TeamsMessageBuilder.BuildTestPayload(Turkish);

        Assert.Contains("Bağlantı Testi Başarılı", payload);
    }
}
