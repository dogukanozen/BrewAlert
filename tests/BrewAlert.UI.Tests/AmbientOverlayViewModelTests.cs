using System.Globalization;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class AmbientOverlayViewModelTests
{
    private readonly IBrewHistoryService _history = Substitute.For<IBrewHistoryService>();
    private readonly ILocalizationService _loc;

    public AmbientOverlayViewModelTests()
    {
        _loc = Substitute.For<ILocalizationService>();
        // Default: echo the key. Override the templates we care about with real placeholders
        // so headlines actually carry the profile name we assert on.
        _loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        _loc.Get("AmbientTeaStale").Returns("{0} stale {1}");
        _loc.Get("AmbientTeaWarning").Returns("{0} cooling {1}");
        _loc.Get("AmbientCoffeeStale").Returns("{0} stale {1}");
        _loc.Get("AmbientCoffeeWarning").Returns("{0} cooling {1}");
        _loc.Get("AmbientCustomStale").Returns("{0} stale {1}");
        _loc.Get("AmbientCustomWarning").Returns("{0} cooling {1}");
        _loc.Get("AmbientStillGood").Returns("{0} still good");
        _loc.CurrentLanguage.Returns("English");
        // ILocalizationService.CurrentCulture is non-nullable in production; stub it
        // here with an invariant culture so weekday formatting is deterministic across CI hosts.
        _loc.CurrentCulture.Returns(CultureInfo.InvariantCulture);
    }

    private AmbientOverlayViewModel CreateVm() => new(_history, _loc);

    [Fact]
    public void ModeTransitionOpacity_StartsFullyVisible()
    {
        var vm = CreateVm();

        Assert.Equal(1.0, vm.ModeTransitionOpacity);
    }

    [Fact]
    public void BuildScenes_KeepsOnlyLatestEntryPerBrewType()
    {
        // Arrange — two teas and one coffee today; older tea must not surface as a scene.
        var now = new DateTime(2026, 5, 26, 18, 0, 0, DateTimeKind.Utc);
        var today = new List<BrewHistoryEntry>
        {
            new(Guid.NewGuid(), now.AddMinutes(-60),  "Earl Grey",  BrewType.Tea,    "🍵", 240),
            new(Guid.NewGuid(), now.AddMinutes(-120), "Espresso",   BrewType.Coffee, "☕", 30),
            new(Guid.NewGuid(), now.AddMinutes(-300), "Green Tea",  BrewType.Tea,    "🍵", 180),
        }
        .OrderByDescending(e => e.CompletedAtUtc)
        .ToList();
        var vm = CreateVm();

        // Act
        var scenes = vm.BuildScenes(today, now);

        // Assert — the 5h-old tea is dropped; exactly one tea card (Earl Grey)
        // and exactly one coffee card remain. Use Assert.Single so accidentally
        // emitting two cards of the same type would fail the test.
        Assert.All(scenes, s => Assert.DoesNotContain("Green Tea", s.Headline));
        Assert.Single(scenes, s => s.Headline.Contains("Earl Grey"));
        Assert.Single(scenes, s => s.Headline.Contains("Espresso"));
    }

    [Fact]
    public void BuildScenes_OneEntryPerType_AllRetained()
    {
        var now = new DateTime(2026, 5, 26, 18, 0, 0, DateTimeKind.Utc);
        var today = new List<BrewHistoryEntry>
        {
            new(Guid.NewGuid(), now.AddMinutes(-10), "Filter Coffee", BrewType.Coffee, "☕", 300),
            new(Guid.NewGuid(), now.AddMinutes(-30), "Chamomile",     BrewType.Tea,    "🍵", 240),
        }
        .OrderByDescending(e => e.CompletedAtUtc)
        .ToList();
        var vm = CreateVm();

        var scenes = vm.BuildScenes(today, now);

        Assert.Single(scenes, s => s.Headline.Contains("Filter Coffee"));
        Assert.Single(scenes, s => s.Headline.Contains("Chamomile"));
    }

    [Fact]
    public void BuildScenes_LatestPerType_IsIndependentOfInputOrder()
    {
        // Caller's contract should not silently break if today arrives unsorted —
        // BuildScenes must compute "latest per type" by timestamp, not list position.
        var now = new DateTime(2026, 5, 26, 18, 0, 0, DateTimeKind.Utc);
        var unsorted = new List<BrewHistoryEntry>
        {
            new(Guid.NewGuid(), now.AddMinutes(-300), "Green Tea",  BrewType.Tea,    "🍵", 180),
            new(Guid.NewGuid(), now.AddMinutes(-60),  "Earl Grey",  BrewType.Tea,    "🍵", 240),
            new(Guid.NewGuid(), now.AddMinutes(-120), "Espresso",   BrewType.Coffee, "☕", 30),
        };
        var vm = CreateVm();

        var scenes = vm.BuildScenes(unsorted, now);

        Assert.All(scenes, s => Assert.DoesNotContain("Green Tea", s.Headline));
        Assert.Single(scenes, s => s.Headline.Contains("Earl Grey"));
        Assert.Single(scenes, s => s.Headline.Contains("Espresso"));
    }

    [Fact]
    public void BuildScenes_FreshnessScenes_CarrySourceEntryForLiveRefresh()
    {
        // The relative-time label and mood used to freeze at build time because the
        // formatted strings were baked into the scene. Freshness cards must keep a
        // reference to their source entry so the VM can re-render them against the
        // live clock; static cards must not carry one.
        var now = new DateTime(2026, 5, 26, 18, 0, 0, DateTimeKind.Utc);
        var today = new List<BrewHistoryEntry>
        {
            new(Guid.NewGuid(), now.AddMinutes(-10), "Filter Coffee", BrewType.Coffee, "☕", 300),
        };
        var vm = CreateVm();

        var scenes = vm.BuildScenes(today, now);

        var freshness = Assert.Single(scenes, s => s.Headline.Contains("Filter Coffee"));
        Assert.NotNull(freshness.Entry);
        Assert.Equal("Filter Coffee", freshness.Entry!.ProfileName);
        Assert.Null(scenes[0].Entry); // welcome card is static
    }

    [Fact]
    public void BuildScenes_RelativeTime_AdvancesAsTimePasses()
    {
        // Same entry rendered at two different "now" values must yield different
        // relative-time text — proving the age is computed live, not frozen.
        _loc.Get("MinutesAgo").Returns("{0} min ago");
        _loc.Get("AmbientStillGoodHint").Returns("{0}");
        var brewedAt = new DateTime(2026, 5, 26, 18, 0, 0, DateTimeKind.Utc);
        var entry = new BrewHistoryEntry(Guid.NewGuid(), brewedAt, "Filter Coffee", BrewType.Coffee, "☕", 300);
        var today = new List<BrewHistoryEntry> { entry };
        var vm = CreateVm();

        var at3 = vm.BuildScenes(today, brewedAt.AddMinutes(3)).Single(s => s.Headline.Contains("Filter Coffee"));
        var at9 = vm.BuildScenes(today, brewedAt.AddMinutes(9)).Single(s => s.Headline.Contains("Filter Coffee"));

        Assert.NotEqual(at3.Subheadline, at9.Subheadline);
    }

    [Theory]
    // mode, dedicated layer flags, card opacity, DVD dim opacity
    [InlineData(AmbientMode.Card,         true,  false, false, false, false, false, false, false, false, false, false, false, 1.0, 0.0)]
    [InlineData(AmbientMode.Dvd,          false, true,  false, false, false, false, false, false, false, false, false, false, 0.0, 0.5)]
    [InlineData(AmbientMode.Orbit,        false, false, true,  false, false, false, false, false, false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Aurora,       false, false, false, true,  false, false, false, false, false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.BrewRain,     false, false, false, false, true,  false, false, false, false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Kaleidoscope, false, false, false, false, false, true,  false, false, false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Meteor,       false, false, false, false, false, false, true,  false, false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Liquid,       false, false, false, false, false, false, false, true,  false, false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Hyperspace,   false, false, false, false, false, false, false, false, true,  false, false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Radar,        false, false, false, false, false, false, false, false, false, true,  false, false, 0.0, 0.0)]
    [InlineData(AmbientMode.Fireflies,    false, false, false, false, false, false, false, false, false, false, true,  false, 0.0, 0.0)]
    [InlineData(AmbientMode.Prism,        false, false, false, false, false, false, false, false, false, false, false, true,  0.0, 0.0)]
    public void Mode_DrivesLayerAndBackdropState(
        AmbientMode mode,
        bool card,
        bool dvd,
        bool orbit,
        bool aurora,
        bool rain,
        bool kaleidoscope,
        bool meteor,
        bool liquid,
        bool hyperspace,
        bool radar,
        bool fireflies,
        bool prism,
        double cardOpacity,
        double dimOpacity)
    {
        // The view shows exactly one dedicated layer per mode. Card text fades unless card mode
        // (CardOpacity), and only DVD dims the backdrop (DvdDimOpacity).
        var vm = CreateVm();

        vm.Mode = mode;

        Assert.Equal(card, vm.IsCardMode);
        Assert.Equal(dvd, vm.IsDvdMode);
        Assert.Equal(orbit, vm.IsOrbitMode);
        Assert.Equal(aurora, vm.IsAuroraMode);
        Assert.Equal(rain, vm.IsBrewRainMode);
        Assert.Equal(kaleidoscope, vm.IsKaleidoscopeMode);
        Assert.Equal(meteor, vm.IsMeteorMode);
        Assert.Equal(liquid, vm.IsLiquidMode);
        Assert.Equal(hyperspace, vm.IsHyperspaceMode);
        Assert.Equal(radar, vm.IsRadarMode);
        Assert.Equal(fireflies, vm.IsFirefliesMode);
        Assert.Equal(prism, vm.IsPrismMode);
        Assert.Equal(cardOpacity, vm.CardOpacity);
        Assert.Equal(dimOpacity, vm.DvdDimOpacity);
    }

    [Theory]
    [InlineData(AmbientMode.Card,         true,  false, false)]
    [InlineData(AmbientMode.Dvd,          true,  false, false)]
    [InlineData(AmbientMode.BrewRain,     true,  false, false)]
    [InlineData(AmbientMode.Liquid,       true,  false, false)]
    [InlineData(AmbientMode.Fireflies,    true,  false, false)]
    [InlineData(AmbientMode.Orbit,        false, true,  false)]
    [InlineData(AmbientMode.Meteor,       false, true,  false)]
    [InlineData(AmbientMode.Hyperspace,   false, true,  false)]
    [InlineData(AmbientMode.Radar,        false, true,  false)]
    [InlineData(AmbientMode.Aurora,       false, false, true)]
    [InlineData(AmbientMode.Kaleidoscope, false, false, true)]
    [InlineData(AmbientMode.Prism,        false, false, true)]
    public void Mode_SelectsExactlyOneBackdropFamily(
        AmbientMode mode, bool organic, bool cosmic, bool prismatic)
    {
        var vm = CreateVm();

        vm.Mode = mode;

        Assert.Equal(organic, vm.IsOrganicBackdrop);
        Assert.Equal(cosmic, vm.IsCosmicBackdrop);
        Assert.Equal(prismatic, vm.IsPrismaticBackdrop);
        Assert.Equal(1, new[] { organic, cosmic, prismatic }.Count(value => value));
    }

    [Fact]
    public void UpdateClock_SetsLocalisedDate_FollowingAppCulture()
    {
        // The date under the clock must format with the *app* culture, not the OS locale —
        // a Turkish UI shows "Haziran", an English one "June". Format carries no time
        // component, so comparing against a freshly computed "today" is stable except at
        // the midnight boundary (same assumption the other DateTime-based tests make).
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        _loc.CurrentCulture.Returns(tr);
        var vm = CreateVm();

        vm.UpdateClock();

        var expected = tr.TextInfo.ToTitleCase(DateTime.Now.ToString("dddd, d MMMM yyyy", tr));
        Assert.Equal(expected, vm.ClockDate);
        Assert.Matches(@"^\d{2}:\d{2}$", vm.ClockTime);
    }

    [Fact]
    public void BuildScenes_NullToday_Throws()
    {
        var vm = CreateVm();

        Assert.Throws<ArgumentNullException>(() => vm.BuildScenes(null!, DateTime.UtcNow));
    }

    [Fact]
    public void BuildScenes_EmptyToday_AddsNoBrewsScene()
    {
        var now = DateTime.UtcNow;
        var vm = CreateVm();

        var scenes = vm.BuildScenes(Array.Empty<BrewHistoryEntry>(), now);

        Assert.Single(scenes, s => s.Headline == "AmbientNoBrewsTitle");
    }

    [Fact]
    public void BuildScenes_WelcomeSubtitle_UsesProvidedNowUtcNotSystemClock()
    {
        // Locks in the determinism contract: the welcome weekday must come from
        // the nowUtc parameter, not DateTime.Now. Pick a fixed date far away from
        // any plausible test-run date so a regression to DateTime.Now produces a
        // different weekday and visibly fails this test.
        _loc.Get("AmbientWelcomeSubtitle").Returns("welcome {0}");
        var newYears2020 = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expectedWeekday = newYears2020.ToLocalTime().ToString("dddd", CultureInfo.InvariantCulture);
        var vm = CreateVm();

        var scenes = vm.BuildScenes(Array.Empty<BrewHistoryEntry>(), newYears2020);

        Assert.Equal($"welcome {expectedWeekday}", scenes[0].Subheadline);
    }
}
