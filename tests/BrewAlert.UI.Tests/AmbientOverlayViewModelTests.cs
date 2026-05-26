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
    }

    private AmbientOverlayViewModel CreateVm() => new(_history, _loc);

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

        // Assert — the 5h-old tea is dropped; only one tea card (Earl Grey) and one coffee card remain.
        Assert.DoesNotContain(scenes, s => s.Headline.Contains("Green Tea"));
        Assert.Contains(scenes, s => s.Headline.Contains("Earl Grey"));
        Assert.Contains(scenes, s => s.Headline.Contains("Espresso"));
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

        Assert.Contains(scenes, s => s.Headline.Contains("Filter Coffee"));
        Assert.Contains(scenes, s => s.Headline.Contains("Chamomile"));
    }

    [Fact]
    public void BuildScenes_EmptyToday_AddsNoBrewsScene()
    {
        var now = DateTime.UtcNow;
        var vm = CreateVm();

        var scenes = vm.BuildScenes(Array.Empty<BrewHistoryEntry>(), now);

        Assert.Contains(scenes, s => s.Headline == "AmbientNoBrewsTitle");
    }
}
