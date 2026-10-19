using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using BrewAlert.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class AppearanceLayoutTests
{
    [AvaloniaFact]
    public async Task BothAppearances_RenderRealViewsAtPiAndNarrowSizes()
    {
        foreach (var width in new[] { 800, 320 })
        foreach (var theme in new[] { BrewTheme.Obsidian, BrewTheme.Porcelain })
        {
            using var fixture = new Fixture(width, 480);
            await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
            Layout(fixture.Window);
            var home = Assert.IsType<ProfileListViewModel>(fixture.Main.CurrentView);
            Assert.Equal(2, home.Profiles.Count);
            var choices = fixture.View.GetVisualDescendants().OfType<Button>().Where(x => x.Classes.Contains("profile-choice")).ToArray();
            Assert.Equal(2, choices.Length);
            Assert.All(choices, choice =>
            {
                Assert.True(choice.Bounds.Width >= 100);
                Assert.True(choice.Bounds.Height >= 100);
            });
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.View.Background);
            Assert.Equal(Color.Parse(theme == BrewTheme.Porcelain ? "#eeede8" : "#0d0e0f"), background.Color);
            Capture(fixture.Window, theme + "-home-" + width);
            home.SelectProfileCommand.Execute(home.Profiles[0]);
            home.SelectProfileCommand.Execute(home.Profiles[1]);
            Layout(fixture.Window);
            Assert.Equal(2, fixture.Timer.GetActiveSessions().Count);
            Assert.IsType<ActiveBrewsViewModel>(fixture.Main.CurrentView);
            Capture(fixture.Window, theme + "-timers-" + width);
            fixture.Main.NavigateToSettingsCommand.Execute(null);
            Layout(fixture.Window);
            var settings = Assert.IsType<SettingsViewModel>(fixture.Main.CurrentView);
            Assert.Equal(7, settings.Appearance.Days.Count);
            Assert.False(settings.Appearance.IsWeekly);
            Capture(fixture.Window, theme + "-settings-" + width);
            await settings.Appearance.SetModeCommand.ExecuteAsync(AppearanceOptions.Weekly);
            Layout(fixture.Window);
            Assert.True(settings.Appearance.IsWeekly);
            Capture(fixture.Window, theme + "-schedule-" + width);
        }
    }

    [AvaloniaFact]
    public async Task ManualSwitch_UpdatesAnOpenTimerWithoutRestartOrNavigation()
    {
        using var fixture = new Fixture(800, 480);
        var home = Assert.IsType<ProfileListViewModel>(fixture.Main.CurrentView);
        home.SelectProfileCommand.Execute(home.Profiles[0]);
        Layout(fixture.Window);
        var timerView = fixture.Main.CurrentView;
        var session = Assert.Single(fixture.Timer.GetActiveSessions());
        await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = "Porcelain" });
        Layout(fixture.Window);
        Assert.Same(timerView, fixture.Main.CurrentView);
        Assert.Equal(session.Id, Assert.Single(fixture.Timer.GetActiveSessions()).Id);
        Assert.Contains("porcelain", fixture.View.Classes);
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.View.Background);
        Assert.Equal(Color.Parse("#eeede8"), brush.Color);
    }

    [AvaloniaFact]
    public void SettingsThemeButton_AppliesImmediatelyDuringABrew()
    {
        using var fixture = new Fixture(800, 480);
        var home = Assert.IsType<ProfileListViewModel>(fixture.Main.CurrentView);
        home.SelectProfileCommand.Execute(home.Profiles[0]);
        var session = Assert.Single(fixture.Timer.GetActiveSessions());
        fixture.Main.NavigateToSettingsCommand.Execute(null);
        Layout(fixture.Window);
        var settings = Assert.IsType<SettingsViewModel>(fixture.Main.CurrentView);
        var button = fixture.View.GetVisualDescendants().OfType<Button>()
            .Single(x => x.IsEffectivelyVisible && x.Content?.ToString() == settings.Appearance.PorcelainText);
        Click(fixture.Window, button);
        Assert.True(fixture.Appearance.IsPorcelain);
        Assert.Same(settings, fixture.Main.CurrentView);
        Assert.Equal(session.Id, Assert.Single(fixture.Timer.GetActiveSessions()).Id);
    }

    [AvaloniaFact]
    public async Task DismissingAmbient_DoesNotClickTheBrewButtonUnderneath()
    {
        using var fixture = new Fixture(800, 480);
        var button = fixture.View.GetVisualDescendants().OfType<Button>().First(x => x.Classes.Contains("profile-choice"));
        var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window)!.Value;
        await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
        Layout(fixture.Window);
        Assert.True(fixture.Main.Ambient.IsVisible);
        fixture.Window.MouseDown(position, MouseButton.Left);
        fixture.Window.MouseUp(position, MouseButton.Left);
        Layout(fixture.Window);
        Assert.False(fixture.Main.Ambient.IsVisible);
        Assert.Empty(fixture.Timer.GetActiveSessions());
    }

    private static void Click(Window window, Button button)
    {
        var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
        Layout(window);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Capture(Window window, string name)
    {
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        var directory = Environment.GetEnvironmentVariable("BREWALERT_RENDER_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, name + ".png"));
    }

    private static IOptionsMonitor<T> Monitor<T>(T options) where T : class
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(options);
        return monitor;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        public Window Window { get; }
        public MainView View { get; }
        public MainWindowViewModel Main { get; }
        public IAppearanceService Appearance { get; }
        public IBrewTimerService Timer { get; }

        public Fixture(int width, int height)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var profiles = Substitute.For<IProfileRepository>();
            profiles.GetAllAsync(Arg.Any<CancellationToken>()).Returns(BrewProfileService.DefaultProfiles);
            var history = Substitute.For<IBrewHistoryService>();
            history.GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new[]
            {
                new BrewHistoryEntry(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-28), "Çay", BrewType.Tea, "♨", 900),
                new BrewHistoryEntry(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-54), "Filtre kahve", BrewType.Coffee, "☕", 240),
            });
            services.AddSingleton(profiles);
            services.AddSingleton(history);
            services.AddSingleton(Substitute.For<IPreferencesService>());
            services.AddSingleton<IBrewTimerService, BrewTimerService>();
            services.AddSingleton<BrewProfileService>();
            services.AddSingleton(Monitor(new AppearanceOptions { Mode = "Obsidian" }));
            services.AddSingleton(Monitor(new LanguageOptions { Language = AppLanguage.Turkish }));
            services.AddSingleton(Monitor(new TeamsGraphOptions()));
            services.AddSingleton(Monitor(new TeamsNotificationOptions()));
            services.AddSingleton(Monitor(new NotificationProviderOptions()));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IAppearanceService, AppearanceService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton(Substitute.For<IUpdateService>());
            services.AddSingleton(Substitute.For<INotificationService>());
            services.AddSingleton<IBrewCompletionNotificationService, BrewCompletionNotificationService>();
            services.AddSingleton<IConfigurationRoot>(new ConfigurationBuilder().Build());
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<AmbientOverlayViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<ProfileListViewModel>();
            services.AddTransient<ActiveBrewsViewModel>();
            services.AddTransient<SettingsViewModel>();
            _provider = services.BuildServiceProvider();
            Appearance = _provider.GetRequiredService<IAppearanceService>();
            Appearance.PropertyChanged += ThemeChanged;
            ThemeChanged(null, new PropertyChangedEventArgs(nameof(IAppearanceService.CurrentTheme)));
            Timer = _provider.GetRequiredService<IBrewTimerService>();
            Main = _provider.GetRequiredService<MainWindowViewModel>();
            View = new MainView { DataContext = Main };
            Window = new Window { Width = width, Height = height, SystemDecorations = SystemDecorations.None, Content = View };
            Window.Show();
            Layout(Window);
        }
        private void ThemeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IAppearanceService.CurrentTheme))
                Application.Current!.RequestedThemeVariant = Appearance.IsPorcelain ? ThemeVariant.Light : ThemeVariant.Dark;
        }
        public void Dispose()
        {
            Window.Close();
            Appearance.PropertyChanged -= ThemeChanged;
            _provider.Dispose();
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
}
