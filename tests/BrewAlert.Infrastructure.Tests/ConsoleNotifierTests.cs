using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class ConsoleNotifierTests
{
    private static BrewSession CreateSession(string name = "Green Tea", int minutes = 3) => new()
    {
        Profile = new BrewProfile
        {
            Name = name,
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(minutes),
            Icon = "🍵"
        },
        Remaining = TimeSpan.Zero,
        State = BrewSessionState.Completed
    };

    [Fact]
    public async Task SendBrewCompletedAsync_AlwaysReturnsSuccess()
    {
        var sut = new ConsoleNotifier(NullLogger<ConsoleNotifier>.Instance);

        var result = await sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendBrewCompletedAsync_ReturnsImmediately()
    {
        var sut = new ConsoleNotifier(NullLogger<ConsoleNotifier>.Instance);

        var task = sut.SendBrewCompletedAsync(CreateSession());

        Assert.True(task.IsCompleted);
        var result = await task;
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TestConnectionAsync_AlwaysReturnsTrue()
    {
        var sut = new ConsoleNotifier(NullLogger<ConsoleNotifier>.Instance);

        var result = await sut.TestConnectionAsync();

        Assert.True(result);
    }
}
