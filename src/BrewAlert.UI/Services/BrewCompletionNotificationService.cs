namespace BrewAlert.UI.Services;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Singleton that subscribes once to <see cref="IBrewTimerService.BrewCompleted"/> and
/// forwards exactly one notification per session id regardless of how many VMs are alive.
/// Transient delivery failures (Wi-Fi drop, timeout, 5xx) are retried with back-off before
/// the final result is reported.
/// The dedupe set is bounded to <see cref="MaxRememberedSessionIds"/> entries so the
/// service does not accumulate unbounded memory over a long app lifetime.
/// </summary>
public sealed class BrewCompletionNotificationService : IBrewCompletionNotificationService, IDisposable
{
    private const int MaxRememberedSessionIds = 256;

    internal static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
    ];

    private readonly IBrewTimerService _timerService;
    private readonly INotificationService _notificationService;
    private readonly TimeProvider _time;
    private readonly HashSet<Guid> _notifiedSessionIds = [];
    private readonly Queue<Guid> _sessionIdQueue = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<BrewNotificationResult>? NotificationCompleted;

    public BrewCompletionNotificationService(
        IBrewTimerService timerService,
        INotificationService notificationService)
        : this(timerService, notificationService, TimeProvider.System)
    {
    }

    public BrewCompletionNotificationService(
        IBrewTimerService timerService,
        INotificationService notificationService,
        TimeProvider timeProvider)
    {
        _timerService = timerService;
        _notificationService = notificationService;
        _time = timeProvider;
        _timerService.BrewCompleted += OnBrewCompleted;
    }

    private async void OnBrewCompleted(object? sender, BrewCompletedEvent e)
    {
        lock (_lock)
        {
            if (!_notifiedSessionIds.Add(e.Session.Id)) return;
            _sessionIdQueue.Enqueue(e.Session.Id);
            while (_sessionIdQueue.Count > MaxRememberedSessionIds)
                _notifiedSessionIds.Remove(_sessionIdQueue.Dequeue());
        }

        try
        {
            var result = await SendWithRetryAsync(e.Session, _disposeCts.Token);
            NotificationCompleted?.Invoke(this, new BrewNotificationResult(e.Session.Id, result.IsSuccess, result.ErrorMessage));
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            NotificationCompleted?.Invoke(this, new BrewNotificationResult(e.Session.Id, false, ex.Message));
        }
    }

    private async Task<NotificationResult> SendWithRetryAsync(BrewSession session, CancellationToken ct)
    {
        var result = await _notificationService.SendBrewCompletedAsync(session, ct);
        foreach (var delay in RetryDelays)
        {
            if (result.IsSuccess || !result.IsTransient) break;
            await Task.Delay(delay, _time, ct);
            result = await _notificationService.SendBrewCompletedAsync(session, ct);
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerService.BrewCompleted -= OnBrewCompleted;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
