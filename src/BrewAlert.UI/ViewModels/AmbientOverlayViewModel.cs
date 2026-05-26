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

    private readonly IBrewHistoryService _history;
    private readonly ILocalizationService _loc;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _sceneTimer;
    private readonly CancellationTokenSource _disposeCts = new();

    private IReadOnlyList<AmbientSceneItem> _scenes = Array.Empty<AmbientSceneItem>();
    private int _sceneIndex;
    private int _showRequestId;
    private bool _disposed;

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
        var requestId = ++_showRequestId;
        await RefreshScenesAsync(requestId);
        if (_disposed || requestId != _showRequestId) return;

        IsVisible = true;
        _animationTimer.Start();
        _sceneTimer.Start();
        ApplyScene(0);
        UpdateClock();
    }

    public void Hide()
    {
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
    }

    private IReadOnlyList<AmbientSceneItem> BuildScenes(
        IReadOnlyList<BrewHistoryEntry> today,
        DateTime nowUtc)
    {
        var scenes = new List<AmbientSceneItem>
        {
            new(
                "☕",
                _loc.Get("AmbientWelcomeTitle"),
                string.Format(CultureInfo.CurrentCulture, _loc.Get("AmbientWelcomeSubtitle"), DateTime.Now.ToString("dddd", CultureInfo.CurrentCulture)),
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

        foreach (var entry in today)
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

    private void OnSceneTick(object? sender, EventArgs e) => AdvanceScene();

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
