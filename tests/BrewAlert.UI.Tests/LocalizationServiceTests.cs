using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public class LocalizationServiceTests
{
    private static IOptionsMonitor<LanguageOptions> BuildMonitor(string initialLanguage)
    {
        var monitor = Substitute.For<IOptionsMonitor<LanguageOptions>>();
        monitor.CurrentValue.Returns(new LanguageOptions { Language = initialLanguage });
        monitor.OnChange(Arg.Any<Action<LanguageOptions, string?>>())
            .Returns(Substitute.For<IDisposable>());
        return monitor;
    }

    [Fact]
    public void Constructor_ReadsInitialLanguageFromOptions()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.Turkish));

        Assert.Equal(AppLanguage.Turkish, sut.CurrentLanguage);
    }

    [Fact]
    public void Get_ReturnsEnglishString_WhenLanguageIsEnglish()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.English));

        Assert.Equal("⏸ Pause", sut.Get("PauseButton"));
        Assert.Equal("Brewing...", sut.Get("Brewing"));
    }

    [Fact]
    public void Get_ReturnsTurkishString_WhenLanguageIsTurkish()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.Turkish));

        Assert.Equal("⏸ Duraklat", sut.Get("PauseButton"));
        Assert.Equal("Demliyor...", sut.Get("Brewing"));
    }

    [Fact]
    public void Get_ReturnsKey_WhenKeyIsUnknown()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.English));

        // Missing key falls back to returning the key itself so the UI shows
        // something diagnosable instead of crashing.
        Assert.Equal("NoSuchKey", sut.Get("NoSuchKey"));
    }

    [Fact]
    public void SetLanguage_WhenLanguageDiffers_FiresLanguageChangedAndUpdatesCurrent()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.English));

        string? notified = null;
        sut.LanguageChanged += lang => notified = lang;

        sut.SetLanguage(AppLanguage.Turkish);

        Assert.Equal(AppLanguage.Turkish, sut.CurrentLanguage);
        Assert.Equal(AppLanguage.Turkish, notified);
    }

    [Fact]
    public void SetLanguage_WhenLanguageMatches_DoesNotFireEvent()
    {
        using var sut = new LocalizationService(BuildMonitor(AppLanguage.English));

        var notifyCount = 0;
        sut.LanguageChanged += _ => notifyCount++;

        sut.SetLanguage(AppLanguage.English);

        Assert.Equal(0, notifyCount);
    }

    [AvaloniaFact]
    public async Task OptionsChange_PropagatesViaUiThread_AndFiresLanguageChanged()
    {
        var monitor = Substitute.For<IOptionsMonitor<LanguageOptions>>();
        monitor.CurrentValue.Returns(new LanguageOptions { Language = AppLanguage.English });

        Action<LanguageOptions, string?>? listener = null;
        monitor
            .OnChange(Arg.Do<Action<LanguageOptions, string?>>(cb => listener = cb))
            .Returns(Substitute.For<IDisposable>());

        using var sut = new LocalizationService(monitor);
        string? notified = null;
        sut.LanguageChanged += lang => notified = lang;

        // Simulate IOptionsMonitor firing on a non-UI thread.
        Assert.NotNull(listener);
        await Task.Run(() => listener!(new LanguageOptions { Language = AppLanguage.Turkish }, null));

        // Drain the dispatcher so the marshalled SetLanguage runs.
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal(AppLanguage.Turkish, sut.CurrentLanguage);
        Assert.Equal(AppLanguage.Turkish, notified);
    }

    [Fact]
    public void Dispose_DisposesOnChangeRegistration()
    {
        var monitor = Substitute.For<IOptionsMonitor<LanguageOptions>>();
        monitor.CurrentValue.Returns(new LanguageOptions { Language = AppLanguage.English });
        var registration = Substitute.For<IDisposable>();
        monitor.OnChange(Arg.Any<Action<LanguageOptions, string?>>()).Returns(registration);

        var sut = new LocalizationService(monitor);
        sut.Dispose();

        registration.Received(1).Dispose();
    }
}
