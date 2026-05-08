namespace BrewAlert.UI.Services;

using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;

/// <summary>
/// Singleton that subscribes once to <see cref="IBrewTimerService.BrewCompleted"/> and
/// forwards exactly one notification per session id regardless of how many VMs are alive.
/// The dedupe set is bounded to <see cref="MaxRememberedSessionIds"/> entries so the
/// service does not accumulate unbounded memory over a long app lifetime.
/// </summary>
public sealed class BrewCompletionNotificationService : IBrewCompletionNotificationService, IDisposable
{
    private const int MaxRememberedSessionIds = 256;

    private readonly IBrewTimerService _timerService;
    private readonly INotificationService _notificationService;
    private readonly HashSet<Guid> _notifiedSessionIds = [];
    private readonly Queue<Guid> _sessionIdQueue = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<BrewNotificationResult>? NotificationCompleted;

    public BrewCompletionNotificationService(
        IBrewTimerService timerService,
        INotificationService notificationService)
    {
        _timerService = timerService;
        _notificationService = notificationService;
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
            var result = await _notificationService.SendBrewCompletedAsync(e.Session);
            NotificationCompleted?.Invoke(this, new BrewNotificationResult(e.Session.Id, result.IsSuccess, result.ErrorMessage));
        }
        catch (Exception ex)
        {
            NotificationCompleted?.Invoke(this, new BrewNotificationResult(e.Session.Id, false, ex.Message));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerService.BrewCompleted -= OnBrewCompleted;
    }
}
