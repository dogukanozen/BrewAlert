using System.Globalization;
using Avalonia.Headless.XUnit;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class AppearanceSettingsViewModelTests
{
    private static AppearanceService Appearance(IPreferencesService preferences)
    {
        var monitor = Substitute.For<IOptionsMonitor<AppearanceOptions>>();
        monitor.CurrentValue.Returns(new AppearanceOptions());
        return new AppearanceService(monitor, preferences, TimeProvider.System);
    }

    private static ILocalizationService Localization()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns(CultureInfo.GetCultureInfo("tr-TR"));
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        loc.Get("CurrentAppearance").Returns("Etkin: {0}");
        loc.Get("SaveFailed").Returns("Kaydedilemedi: {0}");
        loc.Get("ThemeIbre").Returns("İbre");
        loc.Get("ThemeAizome").Returns("Aizome");
        return loc;
    }

    [AvaloniaFact]
    public async Task DayPicker_ShowsAllThemesAndSavesOnlyTheChosenDay()
    {
        var preferences = Substitute.For<IPreferencesService>();
        using var appearance = Appearance(preferences);
        using var vm = new AppearanceSettingsViewModel(appearance, Localization());
        var day = vm.Days.Single(x => x.Day == DayOfWeek.Wednesday);
        Assert.Equal(5, day.Themes.Count);
        await preferences.DidNotReceive().SaveAppearanceAsync(Arg.Any<AppearanceOptions>(), Arg.Any<CancellationToken>());
        day.SelectedTheme = day.Themes.Single(x => x.Theme == BrewTheme.Ibre);
        Assert.Equal(BrewTheme.Ibre, appearance.Options.ForDay(DayOfWeek.Wednesday));
        Assert.Equal(BrewTheme.Aizome, appearance.Options.ForDay(DayOfWeek.Friday));
        await preferences.Received(1).SaveAppearanceAsync(Arg.Any<AppearanceOptions>(), Arg.Any<CancellationToken>());
        await vm.SetModeCommand.ExecuteAsync("Aizome");
        Assert.True(vm.IsAizome);
        Assert.Equal("Etkin: Aizome", vm.ActiveText);
    }

    [AvaloniaFact]
    public void DayPicker_RollsBackSelectionAndReportsFailedSave()
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.SaveAppearanceAsync(Arg.Any<AppearanceOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Read only")));
        using var appearance = Appearance(preferences);
        using var vm = new AppearanceSettingsViewModel(appearance, Localization());
        var day = vm.Days.Single(x => x.Day == DayOfWeek.Wednesday);
        day.SelectedTheme = day.Themes.Single(x => x.Theme == BrewTheme.Ibre);
        Assert.Equal(BrewTheme.Sumi, day.SelectedTheme!.Theme);
        Assert.Equal(BrewTheme.Sumi, appearance.Options.ForDay(DayOfWeek.Wednesday));
        Assert.Contains("Read only", vm.Error);
        Assert.False(vm.IsSaving);
    }
}
