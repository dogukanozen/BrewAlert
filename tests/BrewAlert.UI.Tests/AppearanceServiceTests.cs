using Avalonia.Headless.XUnit;
using BrewAlert.UI.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class AppearanceServiceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 20, 59, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("TestIstanbul", TimeSpan.FromHours(3), "Test", "Test");
    }
    private static AppearanceService Create(Clock clock, IPreferencesService? preferences = null, AppearanceOptions? options = null)
    {
        var monitor = Substitute.For<IOptionsMonitor<AppearanceOptions>>();
        monitor.CurrentValue.Returns(options ?? new AppearanceOptions());
        return new AppearanceService(monitor, preferences ?? Substitute.For<IPreferencesService>(), clock);
    }
    [Theory]
    [InlineData(DayOfWeek.Monday, BrewTheme.Obsidian)]
    [InlineData(DayOfWeek.Tuesday, BrewTheme.Porcelain)]
    [InlineData(DayOfWeek.Wednesday, BrewTheme.Sumi)]
    [InlineData(DayOfWeek.Thursday, BrewTheme.Ibre)]
    [InlineData(DayOfWeek.Friday, BrewTheme.Aizome)]
    [InlineData(DayOfWeek.Saturday, BrewTheme.Porcelain)]
    [InlineData(DayOfWeek.Sunday, BrewTheme.Obsidian)]
    public void DefaultWeek_UsesTheApprovedFiveThemes(DayOfWeek day, BrewTheme expected) =>
        Assert.Equal(expected, new AppearanceOptions().Resolve(day));

    [AvaloniaTheory]
    [InlineData("sumi", BrewTheme.Sumi)]
    [InlineData("IBRE", BrewTheme.Ibre)]
    [InlineData("aizome", BrewTheme.Aizome)]
    public async Task NewManualThemes_AreSavedCanonicallyAndRemainSelectedAcrossDays(string mode, BrewTheme expected)
    {
        var clock = new Clock();
        var preferences = Substitute.For<IPreferencesService>();
        using var service = Create(clock, preferences);
        await service.SaveAsync(new AppearanceOptions { Mode = mode, Friday = "Porcelain" });
        Assert.Equal(expected, service.CurrentTheme);
        Assert.Equal(expected.ToString(), service.Options.Mode);
        clock.Now = clock.Now.AddDays(4);
        service.Refresh(false, true);
        Assert.Equal(expected, service.CurrentTheme);
        Assert.Equal(BrewTheme.Porcelain, service.Options.ForDay(DayOfWeek.Friday));
        await preferences.Received(1).SaveAppearanceAsync(
            Arg.Is<AppearanceOptions>(x => x.Mode == expected.ToString()), Arg.Any<CancellationToken>());
    }

    [AvaloniaFact]
    public void Midnight_UsesLocalDay_AndWaitsForIdleHome()
    {
        var clock = new Clock();
        using var service = Create(clock);
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
        clock.Now = clock.Now.AddMinutes(2); // Tuesday locally, still Monday UTC.
        service.Refresh(hasActiveBrews: true, isHome: true);
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
        service.Refresh(hasActiveBrews: false, isHome: false);
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
        service.Refresh(hasActiveBrews: false, isHome: true);
        Assert.Equal(BrewTheme.Porcelain, service.CurrentTheme);
    }
    [AvaloniaFact]
    public async Task ManualChoice_IsImmediate_AndKeepsWeeklyAssignments()
    {
        var clock = new Clock();
        var preferences = Substitute.For<IPreferencesService>();
        using var service = Create(clock, preferences);
        var options = service.Options;
        options.Mode = nameof(BrewTheme.Porcelain);
        options.Wednesday = nameof(BrewTheme.Porcelain);
        await service.SaveAsync(options);
        Assert.True(service.IsPorcelain);
        await preferences.Received(1).SaveAppearanceAsync(
            Arg.Is<AppearanceOptions>(x => x.Mode == "Porcelain" && x.Wednesday == "Porcelain"), Arg.Any<CancellationToken>());
        clock.Now = clock.Now.AddDays(3);
        service.Refresh(false, true);
        Assert.True(service.IsPorcelain);
        options = service.Options;
        options.Mode = AppearanceOptions.Weekly;
        await service.SaveAsync(options);
        Assert.Equal(service.Options.ForDay(clock.GetLocalNow().DayOfWeek), service.CurrentTheme);
        Assert.Equal("Porcelain", service.Options.Wednesday);
    }
    [AvaloniaFact]
    public async Task FailedSave_DoesNotChangeThemeOrProgram()
    {
        var preferences = Substitute.For<IPreferencesService>();
        preferences.SaveAppearanceAsync(Arg.Any<AppearanceOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Read only")));
        using var service = Create(new Clock(), preferences);
        var options = service.Options;
        options.Mode = "Porcelain";
        await Assert.ThrowsAsync<IOException>(() => service.SaveAsync(options));
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
        Assert.Equal(AppearanceOptions.Weekly, service.Options.Mode);
    }
    [AvaloniaFact]
    public void UnrelatedConfigurationReload_DoesNotApplyPendingCalendarChange()
    {
        var clock = new Clock();
        var monitor = Substitute.For<IOptionsMonitor<AppearanceOptions>>();
        monitor.CurrentValue.Returns(new AppearanceOptions());
        Action<AppearanceOptions, string?>? changed = null;
        var subscription = Substitute.For<IDisposable>();
        monitor.OnChange(Arg.Do<Action<AppearanceOptions, string?>>(callback => changed = callback)).Returns(subscription);
        var service = new AppearanceService(monitor, Substitute.For<IPreferencesService>(), clock);
        clock.Now = clock.Now.AddMinutes(2);
        service.Refresh(true, true);
        changed!(new AppearanceOptions(), null);
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
        service.Dispose();
        subscription.Received(1).Dispose();
        changed(new AppearanceOptions { Mode = "Porcelain" }, null);
        Assert.Equal(BrewTheme.Obsidian, service.CurrentTheme);
    }
    [Fact]
    public void Options_AreNormalized_AndAllSevenDaysAreIndependent()
    {
        var options = new AppearanceOptions { Mode = "weekly", Monday = "PORCELAIN", Tuesday = "invalid" }.Normalized();
        Assert.Equal(AppearanceOptions.Weekly, options.Mode);
        Assert.Equal(BrewTheme.Porcelain, options.ForDay(DayOfWeek.Monday));
        Assert.Equal(BrewTheme.Obsidian, options.ForDay(DayOfWeek.Tuesday));
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var copy = options.Copy();
            copy.SetDay(day, BrewTheme.Porcelain);
            Assert.Equal(BrewTheme.Porcelain, copy.ForDay(day));
            foreach (var other in Enum.GetValues<DayOfWeek>().Where(x => x != day))
                Assert.Equal(options.ForDay(other), copy.ForDay(other));
        }
    }
}
