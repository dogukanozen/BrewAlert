using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
namespace BrewAlert.UI.Services;

/// <summary>Manual changes apply immediately; calendar changes wait for an idle home screen.</summary>
public sealed class AppearanceService : ObservableObject, IAppearanceService, IDisposable
{
    private readonly IPreferencesService _preferences;
    private readonly TimeProvider _clock;
    private readonly IDisposable? _subscription;
    private AppearanceOptions _options;
    private BrewTheme _currentTheme;
    private bool _disposed;
    public BrewTheme CurrentTheme => _currentTheme;
    public bool IsPorcelain => _currentTheme == BrewTheme.Porcelain;
    public AppearanceOptions Options => _options.Copy();

    public AppearanceService(IOptionsMonitor<AppearanceOptions> options,
        IPreferencesService preferences, TimeProvider clock)
    {
        _preferences = preferences;
        _clock = clock;
        _options = options.CurrentValue.Normalized();
        _currentTheme = _options.Resolve(_clock.GetLocalNow().DayOfWeek);
        _subscription = options.OnChange(value =>
        {
            var snapshot = value.Normalized();
            void Apply()
            {
                if (_disposed || _options.EquivalentTo(snapshot)) return;
                ApplyOptions(snapshot);
            }
            if (Dispatcher.UIThread.CheckAccess()) Apply();
            else Dispatcher.UIThread.Post(Apply);
        });
    }

    public void Refresh(bool hasActiveBrews, bool isHome)
    {
        if (_disposed || hasActiveBrews || !isHome) return;
        SetTheme(_options.Resolve(_clock.GetLocalNow().DayOfWeek));
    }
    public async Task SaveAsync(AppearanceOptions options, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = options.Normalized();
        await _preferences.SaveAppearanceAsync(snapshot, ct);
        // A failed save must not pretend the choice is durable.
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (!_disposed) ApplyOptions(snapshot);
        }
        else await Dispatcher.UIThread.InvokeAsync(() => { if (!_disposed) ApplyOptions(snapshot); });
    }
    private void ApplyOptions(AppearanceOptions options)
    {
        _options = options;
        OnPropertyChanged(nameof(Options));
        SetTheme(_options.Resolve(_clock.GetLocalNow().DayOfWeek));
    }
    private void SetTheme(BrewTheme theme)
    {
        if (!SetProperty(ref _currentTheme, theme, nameof(CurrentTheme))) return;
        OnPropertyChanged(nameof(IsPorcelain));
    }
    public void Dispose() { _disposed = true; _subscription?.Dispose(); }
}
