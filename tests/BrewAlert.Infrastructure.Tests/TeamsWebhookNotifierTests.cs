using System.Net;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsWebhookNotifierTests
{
    private const string ValidWebhookUrl = "https://teams.webhook.example.com/hook";

    private static BrewSession CreateSession() => new()
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

    private static (TeamsWebhookNotifier sut, FakeHttpMessageHandler handler) CreateSut(
        bool enabled = true,
        string webhookUrl = ValidWebhookUrl)
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new TeamsNotificationOptions
        {
            Enabled = enabled,
            WebhookUrl = webhookUrl,
            TimeoutSeconds = 30
        });
        var sut = new TeamsWebhookNotifier(
            httpClient,
            options,
            NullLogger<TeamsWebhookNotifier>.Instance);
        return (sut, handler);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenDisabled_ReturnsSuccessWithoutPosting()
    {
        var (sut, handler) = CreateSut(enabled: false);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenWebhookUrlEmpty_ReturnsFailure()
    {
        var (sut, handler) = CreateSut(webhookUrl: "");

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenWebhookUrlWhitespace_ReturnsFailure()
    {
        var (sut, handler) = CreateSut(webhookUrl: "   ");

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenHttpSuccess_ReturnsSuccess()
    {
        var (sut, handler) = CreateSut();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenHttpSuccess_PostsJsonContentType()
    {
        var (sut, handler) = CreateSut();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);

        await sut.SendBrewCompletedAsync(CreateSession());

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("application/json", handler.LastRequest!.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenHttpErrorStatus_ReturnsFailureWithStatusCode()
    {
        var (sut, handler) = CreateSut();
        handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("500", result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenHttpRequestException_ReturnsFailure()
    {
        var (sut, handler) = CreateSut();
        handler.ThrowOnSend = new HttpRequestException("Network unreachable");

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("HTTP error", result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenTaskCanceledException_ReturnsFailure()
    {
        var (sut, handler) = CreateSut();
        handler.ThrowOnSend = new TaskCanceledException("Timeout");

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("HTTP error", result.ErrorMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenWebhookUrlEmpty_ReturnsFalse()
    {
        var (sut, _) = CreateSut(webhookUrl: "");

        var result = await sut.TestConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenHttpSuccess_ReturnsTrue()
    {
        var (sut, handler) = CreateSut();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await sut.TestConnectionAsync();

        Assert.True(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenHttpError_ReturnsFalse()
    {
        var (sut, handler) = CreateSut();
        handler.Response = new HttpResponseMessage(HttpStatusCode.BadRequest);

        var result = await sut.TestConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenException_ReturnsFalse()
    {
        var (sut, handler) = CreateSut();
        handler.ThrowOnSend = new HttpRequestException("Connection refused");

        var result = await sut.TestConnectionAsync();

        Assert.False(result);
    }
}
