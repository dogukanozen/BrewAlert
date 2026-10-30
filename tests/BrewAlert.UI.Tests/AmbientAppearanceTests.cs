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
    // The palettes cache Avalonia brushes; reading them off the UI thread throws once another
    // headless test has created them there, so this must run on the UI thread too.
    [AvaloniaFact]
    public void PaletteFamilies_AreNeutralAndCached()
    {
        foreach (var theme in new[] { BrewTheme.Obsidian, BrewTheme.Porcelain })
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

    [AvaloniaTheory]
    [InlineData(BrewTheme.Sumi, "#eceee5")]
    [InlineData(BrewTheme.Ibre, "#171d1c")]
    [InlineData(BrewTheme.Aizome, "#152a38")]
    public void ThemePalette_MatchesSignatureAndDisablesHiddenLegacyMotion(BrewTheme theme, string background)
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        using var vm = new AmbientOverlayViewModel(Substitute.For<IBrewHistoryService>(), loc);
        vm.Mode = AmbientMode.Orbit;
        vm.SetAppearance(theme);
        Assert.True(vm.HasThemeBackdrop);
        Assert.False(vm.IsThemeBackdropActive);
        vm.IsVisible = true;
        Assert.True(vm.IsThemeBackdropActive);
        Assert.False(vm.IsLegacyAnimationActive);
        Assert.False(vm.IsOrbitMode || vm.IsSilkAnimationActive || vm.IsCosmicBackdrop);
        Assert.Equal(Color.Parse(background), Assert.IsAssignableFrom<ISolidColorBrush>(vm.BackgroundBrush).Color);
        Assert.Same(AmbientPalette.ForAppearance(theme), AmbientPalette.ForAppearance(theme));
        vm.SetAppearance(BrewTheme.Obsidian);
        Assert.True(vm.IsLegacyAnimationActive);
        Assert.True(vm.IsOrbitMode);
        Assert.False(vm.IsThemeBackdropActive);
        vm.IsVisible = false;
        Assert.False(vm.IsLegacyAnimationActive);
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
        Assert.True(vm.IsModeRotationRunning);
        foreach (var theme in new[] { BrewTheme.Sumi, BrewTheme.Ibre, BrewTheme.Aizome })
        {
            vm.SetAppearance(theme);
            Assert.False(vm.IsModeRotationRunning);
            Assert.Equal(AmbientMode.Orbit, vm.Mode);
            vm.SetAppearance(BrewTheme.Porcelain);
            Assert.True(vm.IsModeRotationRunning);
        }
        var brush = vm.BackgroundBrush;
        vm.SetAppearance(BrewTheme.Porcelain);
        Assert.Same(brush, vm.BackgroundBrush);
        vm.SetAppearance(BrewTheme.Obsidian);
        Assert.Same(AmbientPalette.All[0].Background, vm.BackgroundBrush);
    }
}
