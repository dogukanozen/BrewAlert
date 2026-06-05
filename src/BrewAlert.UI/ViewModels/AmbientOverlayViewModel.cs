using Avalonia.Media;
using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
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
    // Animation "modes" swap every period so the screen isn't one look all day: a random
    // mode (info card, retro DVD-logo bounce, or orbiting icons — see PickNextMode) holds the
    // screen for its dwell time. The view owns the bounce geometry (needs layout bounds); this
    // VM owns the timing. Backgrounds within a mode are picked at random (PickNextPalette).
    private static readonly TimeSpan ModePeriod = TimeSpan.FromMinutes(3);
    // The DVD bounce is the most "watchable" mode, so it lingers a bit longer than the rest.
    private static readonly TimeSpan DvdModePeriod = TimeSpan.FromMinutes(5);

    private readonly IBrewHistoryService _history;
    private readonly ILocalizationService _loc;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _sceneTimer;
    private readonly DispatcherTimer _modeTimer;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IReadOnlyList<AmbientPalette> _palettes = AmbientPalette.All;
    private readonly Random _random = new();
    private AmbientPalette _currentPalette = AmbientPalette.All[0];

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
    // Icons are semantic *keys* ("coffee"/"tea"/"teapot"/"steam"/…), not emoji — the view
    // resolves them to vector geometries via AmbientIconConverter. Raspberry Pi OS has no
    // emoji font, so glyphs rendered as tofu boxes; vector paths carry no font dependency.
    [ObservableProperty] private string _heroIcon = "coffee";
    [ObservableProperty] private string _mood = "calm";
    [ObservableProperty] private string _tapHint = string.Empty;
    [ObservableProperty] private string _clockTime = string.Empty;
    [ObservableProperty] private string _floatIconA = "coffee";
    [ObservableProperty] private string _floatIconB = "tea";
    [ObservableProperty] private string _floatIconC = "steam";

    // Background palette — changes to a random palette per scene (see PickNextPalette) so the
    // overlay never sits on the same colours. Brushes are shared instances from AmbientPalette,
    // so re-assigning the same palette on the 500ms animation tick is deduped by ObservableProperty.
    [ObservableProperty] private IBrush _backgroundBrush = AmbientPalette.All[0].Background;
    [ObservableProperty] private IBrush _glowTopBrush = AmbientPalette.All[0].GlowTop;
    [ObservableProperty] private IBrush _glowBottomBrush = AmbientPalette.All[0].GlowBottom;
    [ObservableProperty] private IBrush _haloBrush = AmbientPalette.All[0].Halo;

    // The animation mode currently on screen. Each ModePeriod a *random* different mode is
    // picked (PickNextMode) so the idle screen doesn't just recolour — it switches between
    // distinct motions: the info card, the retro DVD bounce, and the orbiting brew icons.
    // The derived flags drive which layer the view shows; the clock is always on top.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardMode))]
    [NotifyPropertyChangedFor(nameof(IsDvdMode))]
    [NotifyPropertyChangedFor(nameof(IsOrbitMode))]
    [NotifyPropertyChangedFor(nameof(CardOpacity))]
    [NotifyPropertyChangedFor(nameof(DvdDimOpacity))]
    private AmbientMode _mode = AmbientMode.Card;

    public bool IsCardMode => Mode == AmbientMode.Card;
    public bool IsDvdMode => Mode == AmbientMode.Dvd;
    public bool IsOrbitMode => Mode == AmbientMode.Orbit;

    /// <summary>Card text is only shown in card mode; other modes fade it out (Opacity, not
    /// IsVisible) so the always-on clock above never shifts when the mode flips.</summary>
    public double CardOpacity => IsCardMode ? 1.0 : 0.0;

    /// <summary>Opacity of the scrim that dims the lively backdrop in DVD mode so the
    /// bouncing logo reads clearly. The clock sits above the scrim and stays bright.</summary>
    public double DvdDimOpacity => IsDvdMode ? 0.5 : 0.0;

    // Vector brew icons that revolve around the clock in Orbit mode. Keys (not emoji) — the
    // view resolves them through AmbientIconConverter, so they render without an emoji font.
    [ObservableProperty] private string _orbitIconA = "coffee";
    [ObservableProperty] private string _orbitIconB = "tea";
    [ObservableProperty] private string _orbitIconC = "teapot";
    [ObservableProperty] private string _orbitIconD = "steam";

    /// <summary>Text that bounces around the screen in DVD-logo mode.</summary>
    public string DvdLogoText => "ozbilim";

    public AmbientOverlayViewModel(IBrewHistoryService history, ILocalizationService loc)
    {
        _history = history;
        _loc = loc;

        _animationTimer = new DispatcherTimer { Interval = AnimationTickInterval };
        _animationTimer.Tick += OnAnimationTick;

        _sceneTimer = new DispatcherTimer { Interval = SceneRotationInterval };
        _sceneTimer.Tick += OnSceneTick;

        _modeTimer = new DispatcherTimer { Interval = ModePeriod };
        _modeTimer.Tick += OnModeTick;

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
        Mode = AmbientMode.Card;
        _animationTimer.Start();
        _sceneTimer.Start();
        _modeTimer.Interval = PeriodFor(Mode);
        _modeTimer.Start();
        PickNextPalette();
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
        Mode = AmbientMode.Card;
        _animationTimer.Stop();
        _sceneTimer.Stop();
        _modeTimer.Stop();
    }

    // Each period, jump to a random *different* animation mode and reroll the backdrop, so the
    // next stretch always looks different in both motion and colour. The next mode sets its own
    // dwell time (DVD lingers longer); Stop/Start guarantees the new interval takes effect from
    // now rather than after another full old-interval tick.
    private void OnModeTick(object? sender, EventArgs e)
    {
        Mode = PickNextMode();
        _modeTimer.Stop();
        _modeTimer.Interval = PeriodFor(Mode);
        _modeTimer.Start();
        PickNextPalette();
    }

    private static TimeSpan PeriodFor(AmbientMode mode) =>
        mode == AmbientMode.Dvd ? DvdModePeriod : ModePeriod;

    // Pick a random mode that isn't the current one, so modes appear shuffled rather than in
    // a predictable cycle (mirrors PickNextPalette).
    private AmbientMode PickNextMode()
    {
        var modes = Enum.GetValues<AmbientMode>();
        AmbientMode next;
        do
        {
            next = modes[_random.Next(modes.Length)];
        }
        while (next == Mode);
        return next;
    }

    // Pick a random backdrop that isn't the current one, so palettes appear in a shuffled
    // order rather than a predictable cycle. Brushes are shared instances, so assigning the
    // chosen palette on each 500ms tick is deduped by ObservableProperty.
    private void PickNextPalette()
    {
        if (_palettes.Count <= 1) return;
        AmbientPalette next;
        do
        {
            next = _palettes[_random.Next(_palettes.Count)];
        }
        while (ReferenceEquals(next, _currentPalette));
        _currentPalette = next;
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
                "coffee",
                _loc.Get("AmbientWelcomeTitle"),
                string.Format(appCulture, _loc.Get("AmbientWelcomeSubtitle"), nowUtc.ToLocalTime().ToString("dddd", appCulture)),
                "calm")
        };

        if (today.Count == 0)
        {
            scenes.Add(new AmbientSceneItem(
                "teapot",
                _loc.Get("AmbientNoBrewsTitle"),
                _loc.Get("AmbientNoBrewsSubtitle"),
                "calm"));
            return scenes;
        }

        scenes.Add(new AmbientSceneItem(
            IconKeyFor(today[0].Type),
            string.Format(_loc.CurrentCulture, _loc.Get("AmbientTodayCount"), today.Count),
            _loc.Get("AmbientTodayCountHint"),
            "fresh"));

        // One freshness card per brew type — older repeats of the same type would only
        // make the rotation stale (an hour-old tea is irrelevant if a fresh tea exists).
        // MaxBy makes the latest-per-type selection independent of the caller's sort order.
        var latestPerType = today
            .GroupBy(e => e.Type)
            .Select(g => g.MaxBy(e => e.CompletedAtUtc)!);

        foreach (var entry in latestPerType)
            scenes.Add(BuildFreshnessScene(entry, nowUtc));

        return scenes;
    }

    /// <summary>
    /// Renders a single freshness card for <paramref name="entry"/> against
    /// <paramref name="nowUtc"/>. Called both at scene-build time and live on every
    /// apply, so the relative-time label and mood track the real clock instead of
    /// freezing at the moment the rotation was last rebuilt.
    /// </summary>
    private AmbientSceneItem BuildFreshnessScene(BrewHistoryEntry entry, DateTime nowUtc)
    {
        var age = nowUtc - entry.CompletedAtUtc;
        var level = BrewFreshnessEvaluator.Evaluate(entry.Type, age);
        var relative = FormatRelative(age);
        return level switch
        {
            BrewFreshnessLevel.Stale => BuildStaleScene(entry, relative),
            BrewFreshnessLevel.Cooling => BuildWarningScene(entry, relative),
            _ => new AmbientSceneItem(
                IconKeyFor(entry.Type),
                string.Format(_loc.CurrentCulture, _loc.Get("AmbientStillGood"), entry.ProfileName),
                string.Format(_loc.CurrentCulture, _loc.Get("AmbientStillGoodHint"), relative),
                "fresh",
                entry)
        };
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
            IconKeyFor(entry.Type),
            string.Format(_loc.CurrentCulture, _loc.Get(key), entry.ProfileName, relative),
            _loc.Get("AmbientStaleHint"),
            "stale",
            entry);
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
            IconKeyFor(entry.Type),
            string.Format(_loc.CurrentCulture, _loc.Get(key), entry.ProfileName, relative),
            _loc.Get("AmbientWarningHint"),
            "warning",
            entry);
    }

    private string FormatRelative(TimeSpan delta)
    {
        if (delta < TimeSpan.FromMinutes(1)) return _loc.Get("JustNow");
        if (delta < TimeSpan.FromHours(1))
            return string.Format(_loc.CurrentCulture, _loc.Get("MinutesAgo"), (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromDays(1))
            return string.Format(_loc.CurrentCulture, _loc.Get("HoursAgo"), (int)delta.TotalHours);
        return string.Format(_loc.CurrentCulture, _loc.Get("DaysAgo"), (int)delta.TotalDays);
    }

    private void PickFloatIcons(IReadOnlyList<BrewHistoryEntry> today)
    {
        if (today.Count > 0)
        {
            FloatIconA = IconKeyFor(today[0].Type);
            FloatIconB = today.Count > 1 ? IconKeyFor(today[1].Type) : "tea";
            FloatIconC = today.Any(e => e.Type == BrewType.Tea) ? "tea" : "coffee";
            return;
        }

        FloatIconA = "coffee";
        FloatIconB = "tea";
        FloatIconC = "steam";
    }

    // Maps a brew type to a semantic vector-icon key consumed by AmbientIconConverter.
    // Profile emoji are deliberately ignored here: the Pi has no emoji font, so the overlay
    // draws font-independent line art keyed off the type instead.
    internal static string IconKeyFor(BrewType type) => type switch
    {
        BrewType.Tea => "tea",
        BrewType.Coffee => "coffee",
        _ => "cup"
    };

    private void AdvanceScene()
    {
        if (_scenes.Count == 0) return;
        _sceneIndex = (_sceneIndex + 1) % _scenes.Count;
        PickNextPalette();
        ApplyScene(_sceneIndex);
        UpdateClock();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        AnimationPhase = (AnimationPhase + 1) & 7;
        // Keep the relative-time label and freshness mood of the card currently on
        // screen advancing live, not just when the 10s rotation re-applies it.
        // ObservableProperty dedups unchanged values, so this only repaints when a
        // minute boundary (or freshness threshold) is actually crossed.
        ApplyScene(_sceneIndex);
    }

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
        if (index < 0 || index >= _scenes.Count) return;

        // Apply the current (randomly chosen) palette. The view crossfades (BrushTransition)
        // when these change on rotation, and dedups when the animation tick re-applies the
        // same scene — so this assignment is safe to run on every 500ms tick.
        BackgroundBrush = _currentPalette.Background;
        GlowTopBrush = _currentPalette.GlowTop;
        GlowBottomBrush = _currentPalette.GlowBottom;
        HaloBrush = _currentPalette.Halo;

        var scene = _scenes[index];
        // Freshness cards carry their source entry — re-render against the current
        // clock so age-dependent text and mood reflect "now", not build time.
        if (scene.Entry is not null)
            scene = BuildFreshnessScene(scene.Entry, DateTime.UtcNow);
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
        _modeTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _sceneTimer.Tick -= OnSceneTick;
        _modeTimer.Tick -= OnModeTick;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}
