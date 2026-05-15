using System.Reflection;
using System.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class BrewCompletionNotificationServiceTests
{
    private readonly IBrewTimerService _timerService = Substitute.For<IBrewTimerService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();

    private BrewCompletionNotificationService CreateCoordinator() =>
        new(_timerService, _notificationService);

    private static BrewSession MakeSession(string name = "Coffee") =>
        new() { Profile = new BrewProfile { Name = name, BrewDuration = TimeSpan.FromMinutes(4) } };

    [Fact]
    public async Task BrewCompleted_SendsNotificationAndFiresEvent()
    {
        var session = MakeSession();
        _notificationService
            .SendBrewCompletedAsync(session, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(NotificationResult.Success()));

        var tcs = new TaskCompletionSource<BrewNotificationResult>();
        using var coordinator = CreateCoordinator();
        coordinator.NotificationCompleted += (_, r) => tcs.TrySetResult(r);

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await _notificationService.Received(1).SendBrewCompletedAsync(session, Arg.Any<CancellationToken>());
        Assert.Equal(session.Id, result.SessionId);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task BrewCompleted_SameSessionFiredTwice_SendsOnlyOnce()
    {
        var session = MakeSession();
        _notificationService
            .SendBrewCompletedAsync(session, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(NotificationResult.Success()));

        var callCount = 0;
        var tcs = new TaskCompletionSource();
        using var coordinator = CreateCoordinator();
        coordinator.NotificationCompleted += (_, _) => { callCount++; tcs.TrySetResult(); };

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Allow a moment for a spurious second call to show up
        await Task.Delay(50);

        Assert.Equal(1, callCount);
        await _notificationService.Received(1).SendBrewCompletedAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BrewCompleted_DifferentSessions_SendsBoth()
    {
        var s1 = MakeSession("Coffee");
        var s2 = MakeSession("Tea");
        _notificationService
            .SendBrewCompletedAsync(Arg.Any<BrewSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(NotificationResult.Success()));

        var results = new List<BrewNotificationResult>();
        var tcs = new TaskCompletionSource();
        using var coordinator = CreateCoordinator();
        coordinator.NotificationCompleted += (_, r) =>
        {
            results.Add(r);
            if (results.Count == 2) tcs.TrySetResult();
        };

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(s1));
        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(s2));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, results.Count);
        await _notificationService.Received(2).SendBrewCompletedAsync(Arg.Any<BrewSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BrewCompleted_WhenNoActiveBrewViewIsPresent_StillSendsNotification()
    {
        // Coordinator exists but no ActiveBrewsViewModel/BrewItemViewModel is listening to NotificationCompleted
        var session = MakeSession();
        _notificationService
            .SendBrewCompletedAsync(session, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(NotificationResult.Success()));

        using var coordinator = CreateCoordinator();
        // No subscriber on NotificationCompleted — simulates no timer view visible

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        // Allow async void to complete
        await Task.Delay(100);

        await _notificationService.Received(1).SendBrewCompletedAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BrewCompleted_MoreThanMaxSessions_SendsAllAndBoundsInternalSet()
    {
        // 257 distinct sessions — one more than the 256-entry cap
        const int count = 257;
        _notificationService
            .SendBrewCompletedAsync(Arg.Any<BrewSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(NotificationResult.Success()));

        var sessions = Enumerable.Range(0, count)
            .Select(_ => MakeSession())
            .ToList();

        var received = 0;
        var tcs = new TaskCompletionSource();
        using var coordinator = CreateCoordinator();
        coordinator.NotificationCompleted += (_, _) =>
        {
            if (Interlocked.Increment(ref received) == count) tcs.TrySetResult();
        };

        foreach (var s in sessions)
            _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(s));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(count, received);

        // Internal dedupe set must be bounded to 256 entries
        var setField = typeof(BrewCompletionNotificationService)
            .GetField("_notifiedSessionIds", BindingFlags.NonPublic | BindingFlags.Instance);
        var set = (HashSet<Guid>)setField!.GetValue(coordinator)!;
        Assert.True(set.Count <= 256, $"Expected ≤256 remembered ids, got {set.Count}");
    }

    [Fact]
    public async Task BrewCompleted_WhenNotificationServiceThrows_FiresFailureResult()
    {
        var session = MakeSession();
        _notificationService
            .SendBrewCompletedAsync(session, Arg.Any<CancellationToken>())
            .Returns<Task<NotificationResult>>(_ => throw new InvalidOperationException("Teams unreachable"));

        var tcs = new TaskCompletionSource<BrewNotificationResult>();
        using var coordinator = CreateCoordinator();
        coordinator.NotificationCompleted += (_, r) => tcs.TrySetResult(r);

        _timerService.BrewCompleted += Raise.Event<EventHandler<BrewCompletedEvent>>(this, new BrewCompletedEvent(session));

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(session.Id, result.SessionId);
        Assert.False(result.IsSuccess);
        Assert.Contains("Teams unreachable", result.ErrorMessage);
    }
}
