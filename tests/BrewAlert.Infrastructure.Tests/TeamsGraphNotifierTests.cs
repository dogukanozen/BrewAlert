using System.Net;
using System.Text;
using System.Text.Json;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class TeamsGraphNotifierTests
{
    private static BrewSession CreateSession() => new()
    {
        Profile = new BrewProfile
        {
            Name = "Morning Espresso",
            Type = BrewType.Coffee,
            BrewDuration = TimeSpan.FromMinutes(3),
            Icon = "☕"
        },
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-3),
        EndsAtUtc = DateTime.UtcNow,
        Remaining = TimeSpan.Zero,
        State = BrewSessionState.Completed
    };

    private static TeamsGraphOptions ValidOptions() => new()
    {
        Enabled = true,
        TenantId = "tenant-id",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ChatId = "chat-id",
        TimeoutSeconds = 30
    };

    private static string BuildTokenResponse(int expiresIn = 3600) =>
        JsonSerializer.Serialize(new { access_token = "fake-token", expires_in = expiresIn });

    private static (TeamsGraphNotifier sut, SequentialFakeHttpHandler handler) CreateSut(
        TeamsGraphOptions? opts = null)
    {
        var handler = new SequentialFakeHttpHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(opts ?? ValidOptions());
        var sut = new TeamsGraphNotifier(httpClient, options, NullLogger<TeamsGraphNotifier>.Instance);
        return (sut, handler);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenDisabled_ReturnsSuccessWithoutPosting()
    {
        var opts = ValidOptions();
        opts.Enabled = false;
        var (sut, handler) = CreateSut(opts);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenMissingClientId_ReturnsFailure()
    {
        var opts = ValidOptions();
        opts.ClientId = "";
        var (sut, handler) = CreateSut(opts);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("ClientId", result.ErrorMessage);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenMissingChatId_ReturnsFailure()
    {
        var opts = ValidOptions();
        opts.ChatId = "";
        var (sut, handler) = CreateSut(opts);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("ChatId", result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenBothCallsSucceed_ReturnsSuccess()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenTokenRequestFails_ThrowsWrappedAsFailure()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"invalid_client\"}", Encoding.UTF8, "application/json")
        });

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("HTTP error", result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenGraphApiFails_ReturnsFailureWithStatusCode()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":{\"code\":\"Forbidden\"}}", Encoding.UTF8, "application/json")
        });

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("403", result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_TokenIsCachedOnSecondCall()
    {
        var (sut, handler) = CreateSut();
        // First call: token + message
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));
        // Second call: only message (token from cache)
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        await sut.SendBrewCompletedAsync(CreateSession());
        await sut.SendBrewCompletedAsync(CreateSession());

        Assert.Equal(3, handler.CallCount); // 1 token + 2 messages
    }

    [Fact]
    public async Task SendBrewCompletedAsync_WhenHttpRequestException_ReturnsFailure()
    {
        var (sut, handler) = CreateSut();
        handler.ThrowOnNext = new HttpRequestException("Network failure");

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.False(result.IsSuccess);
        Assert.Contains("HTTP error", result.ErrorMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenMissingConfig_ReturnsFalse()
    {
        var opts = ValidOptions();
        opts.TenantId = "";
        var (sut, _) = CreateSut(opts);

        var result = await sut.TestConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenBothCallsSucceed_ReturnsTrue()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        var result = await sut.TestConnectionAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task GraphRequest_UsesCorrectChatIdInUrl()
    {
        var opts = ValidOptions();
        opts.ChatId = "my-specific-chat-id";
        var (sut, handler) = CreateSut(opts);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        await sut.SendBrewCompletedAsync(CreateSession());

        Assert.Contains("my-specific-chat-id", handler.LastRequestUrl);
    }

    [Fact]
    public async Task GraphRequest_SendsBearerToken()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse(), Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        await sut.SendBrewCompletedAsync(CreateSession());

        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("fake-token", handler.LastAuthParameter);
    }
}

/// <summary>Returns queued responses in order; supports a throw-on-next escape hatch.</summary>
internal sealed class SequentialFakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _queue = new();
    public int CallCount { get; private set; }
    public string? LastRequestUrl { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }
    public Exception? ThrowOnNext { get; set; }

    public void Enqueue(HttpResponseMessage response) => _queue.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequestUrl = request.RequestUri?.ToString();
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        if (ThrowOnNext is not null)
            throw ThrowOnNext;

        if (_queue.Count == 0)
            throw new InvalidOperationException("No more queued responses.");

        return Task.FromResult(_queue.Dequeue());
    }
}
