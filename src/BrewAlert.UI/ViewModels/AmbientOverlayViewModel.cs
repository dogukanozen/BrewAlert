using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Full-screen ambient display: today's brew insights, freshness warnings, light motion.
/// Visibility is controlled by <see cref="MainWindowViewModel"/>.
/// </summary>
public partial class AmbientOverlayViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan SceneRotationInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AnimationTickInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SceneRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly IBrewHistoryService _history;
    private readonly ILocalizationService _loc;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _sceneTimer;
    private readonly CancellationTokenSource _disposeCts = new();

    private IReadOnlyList<AmbientSceneItem> _scenes = Array.Empty<AmbientSceneItem>();
    private int _sceneIndex;
    private int _showRequestId;
    private bool _disposed;
    private bool _showPending;
    private DateTime _lastRefreshUtc;

    public bool ShowPending => _showPending;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private int _animationPhase;
    [ObservableProperty] private string _headline = string.Empty;
    [ObservableProperty] private string _subheadline = string.Empty;
    [ObservableProperty] private string _heroIcon = "☕";
    [ObservableProperty] private string _mood = "calm";
    [ObservableProperty] private string _tapHint = string.Empty;
    [ObservableProperty] private string _clockTime = string.Empty;
    [ObservableProperty] private string _floatIconA = "☕";
    [ObservableProperty] private string _floatIconB = "🍵";
    [ObservableProperty] private string _floatIconC = "💨";

    public AmbientOverlayViewModel(IBrewHistoryService history, ILocalizationService loc)
    {
        _history = history;
        _loc = loc;

        _animationTimer = new DispatcherTimer { Interval = AnimationTickInterval };
        _animationTimer.Tick += OnAnimationTick;

        _sceneTimer = new DispatcherTimer { Interval = SceneRotationInterval };
        _sceneTimer.Tick += OnSceneTick;

        _loc.LanguageChanged += OnLanguageChanged;
        RefreshTapHint();
    }

    public async Task ShowAsync()
    {
        if (_disposed) return;
        _showPending = true;
        var requestId = ++_showRequestId;
        try
        {
            await RefreshScenesAsync(requestId);
        }
        finally
        {
            _showPending = false;
        }
        if (_disposed || requestId != _showRequestId) return;

        IsVisible = true;
        _animationTimer.Start();
        _sceneTimer.Start();
        ApplyScene(0);
        UpdateClock();
        _lastRefreshUtc = DateTime.UtcNow;
    }

    public void Hide()
    {
        // No-op when idle so frequent user input doesn't churn the request id or timers.
        if (!IsVisible && !_showPending) return;

        _showRequestId++;
        IsVisible = false;
        _animationTimer.Stop();
        _sceneTimer.Stop();
    }

    public void UpdateClock() =>
        ClockTime = DateTime.Now.ToString("HH:mm");

    private void OnLanguageChanged(string language)
    {
        RefreshTapHint();
        if (IsVisible)
        {
            var requestId = ++_showRequestId;
            _ = RefreshScenesAsync(requestId);
        }
    }

    private void RefreshTapHint() => TapHint = _loc.Get("AmbientTapToWake");

    private async Task RefreshScenesAsync(int requestId)
    {
        try
        {
            var entries = await _history.GetRecentAsync(50, _disposeCts.Token);
            if (_disposed || requestId != _showRequestId) return;

            var todayStart = DateTime.Today.ToUniversalTime();
            var todayEnd = DateTime.Today.AddDays(1).ToUniversalTime();
            var today = entries
                .Where(e => e.CompletedAtUtc >= todayStart && e.CompletedAtUtc < todayEnd)
                .OrderByDescending(e => e.CompletedAtUtc)
                .ToList();

            ApplyScenesFromToday(today);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (_disposed || requestId != _showRequestId) return;
            ApplyScenesFromToday(Array.Empty<BrewHistoryEntry>());
        }
    }

    private void ApplyScenesFromToday(IReadOnlyList<BrewHistoryEntry> today)
    {
        _scenes = BuildScenes(today, DateTime.UtcNow);
        _sceneIndex = 0;
        PickFloatIcons(today);
        // Language change & periodic refresh happen while visible — repaint scene 0 immediately
        // so the user doesn't see stale text until the next 10s rotation.
        if (IsVisible)
            ApplyScene(0);
    }

    internal IReadOnlyList<AmbientSceneItem> BuildScenes(
        IReadOnlyList<BrewHistoryEntry> today,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(today);

        // Weekday name must follow the *app* language, not the OS locale — on an en-US
        // host with the UI switched to Turkish we'd otherwise render "Güzel bir Tuesday günü".
        var appCulture = _loc.CurrentCulture;
        var scenes = new List<AmbientSceneItem>
        {
            new(
                "☕",
                _loc.Get("AmbientWelcomeTitle"),
                string.Format(appCulture, _loc.Get("AmbientWelcomeSubtitle"), DateTime.Now.ToString("dddd", appCulture)),
                "calm")
        };

        if (today.Count == 0)
        {
            scenes.Add(new AmbientSceneItem(
                "🫖",
                _loc.Get("AmbientNoBrewsTitle"),
                _loc.Get("AmbientNoBrewsSubtitle"),
                "calm"));
            return scenes;
        }

        scenes.Add(new AmbientSceneItem(
            today[0].Icon,
            string.Format(CultureInfo.CurrentCulture, _loc.Get("AmbientTodayCount"), today.Count),
            _loc.Get("AmbientTodayCountHint"),
            "fresh"));

        // One freshness card per brew type — older repeats of the same type would only
        // make the rotation stale (an hour-old tea is irrelevant if a fresh tea exists).
        // MaxBy makes the latest-per-type selection independent of the caller's sort order.
        var latestPerType = today
            .GroupBy(e => e.Type)
            .Select(g => g.MaxBy(e => e.CompletedAtUtc)!);

        foreach (var entry in latestPerType)
        {
            var age = nowUtc - entry.CompletedAtUtc;
            var level = BrewFreshnessEvaluator.Evaluate(entry.Type, age);
            var relative = FormatRelative(age);
            var scene = level switch
            {
                BrewFreshnessLevel.Stale => BuildStaleScene(entry, relative),
                BrewFreshnessLevel.Cooling => BuildWarningScene(entry, relative),
                _ => new AmbientSceneItem(
                    entry.Icon,
                    string.Format(CultureInfo.CurrentCulture, _loc.Get("AmbientStillGood"), entry.ProfileName),
                    string.Format(CultureInfo.CurrentCulture, _loc.Get("AmbientStillGoodHint"), relative),
                    "fresh")
            };
            scenes.Add(scene);
        }

        return scenes;
    }

    private AmbientSceneItem BuildStaleScene(BrewHistoryEntry entry, string relative)
    {
        var key = entry.Type switch
        {
            BrewType.Tea => "AmbientTeaStale",
            BrewType.Coffee => "AmbientCoffeeStale",
            _ => "AmbientCustomStale"
        };
        return new AmbientSceneItem(
            entry.Icon,
            string.Format(CultureInfo.CurrentCulture, _loc.Get(key), entry.ProfileName, relative),
            _loc.Get("AmbientStaleHint"),
            "stale");
    }

    private AmbientSceneItem BuildWarningScene(BrewHistoryEntry entry, string relative)
    {
        var key = entry.Type switch
        {
            BrewType.Tea => "AmbientTeaWarning",
            BrewType.Coffee => "AmbientCoffeeWarning",
            _ => "AmbientCustomWarning"
        };
        return new AmbientSceneItem(
            entry.Icon,
            string.Format(CultureInfo.CurrentCulture, _loc.Get(key), entry.ProfileName, relative),
            _loc.Get("AmbientWarningHint"),
            "warning");
    }

    private string FormatRelative(TimeSpan delta)
    {
        if (delta < TimeSpan.FromMinutes(1)) return _loc.Get("JustNow");
        if (delta < TimeSpan.FromHours(1))
            return string.Format(CultureInfo.CurrentCulture, _loc.Get("MinutesAgo"), (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromDays(1))
            return string.Format(CultureInfo.CurrentCulture, _loc.Get("HoursAgo"), (int)delta.TotalHours);
        return string.Format(CultureInfo.CurrentCulture, _loc.Get("DaysAgo"), (int)delta.TotalDays);
    }

    private void PickFloatIcons(IReadOnlyList<BrewHistoryEntry> today)
    {
        if (today.Count > 0)
        {
            FloatIconA = today[0].Icon;
            FloatIconB = today.Count > 1 ? today[1].Icon : "🍵";
            FloatIconC = today.Any(e => e.Type == BrewType.Tea) ? "🍵" : "☕";
            return;
        }

        FloatIconA = "☕";
        FloatIconB = "🍵";
        FloatIconC = "💨";
    }

    private void AdvanceScene()
    {
        if (_scenes.Count == 0) return;
        _sceneIndex = (_sceneIndex + 1) % _scenes.Count;
        ApplyScene(_sceneIndex);
        UpdateClock();
    }

    private void OnAnimationTick(object? sender, EventArgs e) =>
        AnimationPhase = (AnimationPhase + 1) & 7;

    private void OnSceneTick(object? sender, EventArgs e)
    {
        AdvanceScene();
        // Long kiosk sessions cause relative-time labels and the weekday header to drift; refresh periodically.
        if (DateTime.UtcNow - _lastRefreshUtc > SceneRefreshInterval)
        {
            _lastRefreshUtc = DateTime.UtcNow;
            var requestId = ++_showRequestId;
            _ = RefreshScenesAsync(requestId);
        }
    }

    private void ApplyScene(int index)
    {
        if (_scenes.Count == 0) return;
        var scene = _scenes[index];
        HeroIcon = scene.Icon;
        Headline = scene.Headline;
        Subheadline = scene.Subheadline;
        Mood = scene.Mood;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _animationTimer.Stop();
        _sceneTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _sceneTimer.Tick -= OnSceneTick;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
