using Avalonia.Headless.XUnit;
using Avalonia.Media;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class AmbientAppearanceTests
{
    [Fact]
    public void PaletteFamilies_AreNeutralAndCached()
    {
        foreach (var theme in Enum.GetValues<BrewTheme>())
        {
            var palettes = AmbientPalette.ForAppearance(theme);
            Assert.Same(palettes, AmbientPalette.ForAppearance(theme));
            foreach (var palette in palettes)
            {
                var background = Assert.IsAssignableFrom<ISolidColorBrush>(palette.Background);
                Assert.InRange(Math.Max(background.Color.R, Math.Max(background.Color.G, background.Color.B))
                    - Math.Min(background.Color.R, Math.Min(background.Color.G, background.Color.B)), 0, 8);
                if (theme == BrewTheme.Porcelain) Assert.True(background.Color.R > 220);
                else Assert.True(background.Color.R < 30);
            }
        }
    }

    [AvaloniaFact]
    public async Task ThemeSwitch_UpdatesVisibleAmbientWithoutResettingItsMode()
    {
        var history = Substitute.For<IBrewHistoryService>();
        history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<BrewHistoryEntry>());
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        using var vm = new AmbientOverlayViewModel(history, loc);
        await vm.ShowAsync();
        vm.Mode = AmbientMode.Orbit;
        vm.SetAppearance(BrewTheme.Porcelain);
        Assert.True(vm.IsVisible);
        Assert.Equal(AmbientMode.Orbit, vm.Mode);
        Assert.Same(AmbientPalette.Porcelain[0].Background, vm.BackgroundBrush);
        var brush = vm.BackgroundBrush;
        vm.SetAppearance(BrewTheme.Porcelain);
        Assert.Same(brush, vm.BackgroundBrush);
        vm.SetAppearance(BrewTheme.Obsidian);
        Assert.Same(AmbientPalette.All[0].Background, vm.BackgroundBrush);
    }
}
