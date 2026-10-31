using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
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
    [InlineData(BrewTheme.Obsidian)]
    [InlineData(BrewTheme.Porcelain)]
    [InlineData(BrewTheme.Sumi)]
    [InlineData(BrewTheme.Ibre)]
    [InlineData(BrewTheme.Aizome)]
    public async Task EveryTheme_KeepsAllModesAvailableAndRotating(BrewTheme theme)
    {
        using var vm = CreateViewModel();
        vm.SetAppearance(theme);
        await vm.ShowAsync();
        foreach (var mode in Enum.GetValues<AmbientMode>())
        {
            vm.Mode = mode;
            var visibleModes = new[] { vm.IsCardMode, vm.IsDvdMode, vm.IsOrbitMode,
                vm.IsAuroraMode, vm.IsBrewRainMode, vm.IsKaleidoscopeMode, vm.IsMeteorMode,
                vm.IsLiquidMode, vm.IsHyperspaceMode, vm.IsRadarMode, vm.IsFirefliesMode,
                vm.IsPrismMode, vm.IsSilkMode, vm.IsThemeBackdropMode };
            Assert.Single(visibleModes, active => active);
            Assert.True(vm.IsModeRotationRunning);
            Assert.Equal(mode == AmbientMode.Silk, vm.IsSilkAnimationActive);
            Assert.Equal(mode is AmbientMode.SilkWeave or AmbientMode.OrbitalDots or AmbientMode.MoonWaves,
                vm.IsThemeBackdropActive);
        }
        vm.Hide();
        Assert.False(vm.IsModeRotationRunning);
        Assert.False(vm.IsThemeBackdropActive);
        Assert.False(vm.IsSilkAnimationActive);
    }

    [AvaloniaFact]
    public async Task ThemeSwitch_ChangesOnlyColoursWithoutResettingModeOrFade()
    {
        using var vm = CreateViewModel();
        vm.SetAppearance(BrewTheme.Porcelain);
        await vm.ShowAsync();
        foreach (var mode in Enum.GetValues<AmbientMode>())
        {
            vm.Mode = mode;
            foreach (var theme in Enum.GetValues<BrewTheme>())
            {
                vm.ModeTransitionOpacity = 0.08;
                vm.SetAppearance(theme);
                Assert.True(vm.IsVisible);
                Assert.Equal(mode, vm.Mode);
                Assert.Equal(0.08, vm.ModeTransitionOpacity);
                Assert.True(vm.IsModeRotationRunning);
                Assert.Same(AmbientPalette.ForAppearance(theme)[0].Background, vm.BackgroundBrush);
                var brush = vm.BackgroundBrush;
                vm.SetAppearance(theme);
                Assert.Same(brush, vm.BackgroundBrush);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(BrewTheme.Sumi, "#44657a4a")]
    [InlineData(BrewTheme.Ibre, "#44a6b9ac")]
    [InlineData(BrewTheme.Aizome, "#449cbac1")]
    public void ThemeResources_OverrideEveryEffectWithMatchingAlpha(BrewTheme theme, string accent)
    {
        var include = new ResourceInclude(new Uri("avares://BrewAlert.UI/"))
        {
            Source = new Uri("avares://BrewAlert.UI/Themes/AmbientColors.axaml")
        };
        var resources = Assert.IsType<ResourceDictionary>(include.Loaded);
        var baseline = Assert.IsType<ResourceDictionary>(resources.ThemeDictionaries[ThemeVariant.Dark]);
        var themed = Assert.IsType<ResourceDictionary>(resources.ThemeDictionaries[BrewThemeVariants.For(theme)]);
        Assert.Equal(baseline.Keys.OrderBy(key => key.ToString()), themed.Keys.OrderBy(key => key.ToString()));
        foreach (var key in baseline.Keys.OfType<string>().Where(key => key.StartsWith("AmbientColor")))
        {
            var colour = Assert.IsType<Color>(themed[key]);
            Assert.Equal(Assert.IsType<Color>(baseline[key]).A, colour.A);
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(themed[key.Replace("Color", "Brush")]);
            Assert.Equal(colour, brush.Color);
        }
        Assert.Equal(Color.Parse(accent), Assert.IsType<Color>(themed["AmbientColor4484"]));
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(AmbientPalette.ForAppearance(theme)[0].Background).Color,
            Assert.IsType<Color>(themed["AmbientColorFF00"]));
    }

    private static AmbientOverlayViewModel CreateViewModel()
    {
        var history = Substitute.For<IBrewHistoryService>();
        history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<BrewHistoryEntry>());
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        return new AmbientOverlayViewModel(history, loc);
    }
}
