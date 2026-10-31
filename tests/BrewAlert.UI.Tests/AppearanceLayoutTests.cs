using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
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
    public void ButtonText_InheritsButtonForegroundAcrossThemeChanges()
    {
        var label = new TextBlock { Text = "Default shell text" };
        var panel = new StackPanel { Spacing = 8, Children = { label } };
        var shell = new UserControl { Classes = { "brew-shell" }, Content = panel };
        foreach (var classes in new[] { new[] { "brew-primary" }, new[] { "theme-option", "selected" }, new[] { "brew-nav-btn" } })
        foreach (var explicitText in new[] { false, true })
        {
            var button = new Button { Content = explicitText ? new TextBlock { Text = "Continue" } : "Continue" };
            foreach (var name in classes) button.Classes.Add(name);
            panel.Children.Add(button);
        }
        var window = new Window { Width = 800, Height = 480, Content = shell };
        var previousTheme = Application.Current!.RequestedThemeVariant;
        try
        {
            window.Show();
            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light, ThemeVariant.Dark })
            {
                Application.Current.RequestedThemeVariant = theme;
                Layout(window);
                Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(shell.Foreground).Color,
                    Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
                foreach (var button in panel.Children.OfType<Button>())
                {
                    var text = Assert.Single(button.GetVisualDescendants().OfType<TextBlock>());
                    Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color,
                        Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color);
                }
                Capture(window, "button-foreground-" + theme);
            }
        }
        finally
        {
            window.Close();
            Application.Current.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaFact]
    public async Task AllAppearances_RenderRealViewsAtPiAndNarrowSizes()
    {
        foreach (var width in new[] { 800, 320 })
        foreach (var theme in Enum.GetValues<BrewTheme>())
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
                Assert.True(choice.Bounds.Height >= 88);
                foreach (var text in choice.GetVisualDescendants().OfType<TextBlock>().Where(x => x.IsEffectivelyVisible))
                {
                    var origin = text.TranslatePoint(new Point(0, 0), choice)!.Value;
                    Assert.InRange(origin.X, -0.5, choice.Bounds.Width);
                    Assert.True(origin.X + text.Bounds.Width <= choice.Bounds.Width + 0.5, theme + " profile text overflows.");
                }
            });
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.View.Background);
            Assert.Equal(Color.Parse(theme switch { BrewTheme.Porcelain => "#eeede8", BrewTheme.Sumi => "#eceee5", BrewTheme.Ibre => "#171d1c", BrewTheme.Aizome => "#152a38", _ => "#0d0e0f" }), background.Color);
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
            var texts = fixture.View.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Assert.Equal("Office-WiFi", texts.Single(x => x.Name == "NetworkName").Text);
            var address = texts.Single(x => x.Name == "NetworkAddress");
            Assert.Equal("IPv4: 192.168.1.23", address.Text);
            Assert.True(address.IsEffectivelyVisible);
            Assert.InRange(address.Bounds.Width, 1, width);
            Capture(fixture.Window, theme + "-settings-" + width);
            address.BringIntoView();
            Layout(fixture.Window);
            Capture(fixture.Window, theme + "-settings-network-" + width);
            await settings.Appearance.SetModeCommand.ExecuteAsync(AppearanceOptions.Weekly);
            Layout(fixture.Window);
            Assert.True(settings.Appearance.IsWeekly);
            var appearanceView = fixture.View.GetVisualDescendants().OfType<AppearanceSettingsView>().Single();
            appearanceView.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == settings.Appearance.Title).BringIntoView();
            Layout(fixture.Window);
            var dayPickers = appearanceView.GetVisualDescendants().OfType<ComboBox>().ToArray();
            Assert.Equal(7, dayPickers.Length);
            Assert.All(dayPickers, picker => Assert.Equal(5, picker.ItemCount));
            Capture(fixture.Window, theme + "-schedule-" + width);
        }
    }

    [AvaloniaTheory]
    [InlineData(800)]
    [InlineData(320)]
    public async Task WeeklyPicker_SelectionSavesSelectedDay(int width)
    {
        using var fixture = new Fixture(width, 480);
        fixture.Main.NavigateToSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(fixture.Main.CurrentView);
        await settings.Appearance.SetModeCommand.ExecuteAsync(AppearanceOptions.Weekly);
        Layout(fixture.Window);
        var picker = fixture.View.GetVisualDescendants().OfType<ComboBox>()
            .Single(x => x.DataContext is DayThemeViewModel { Day: DayOfWeek.Wednesday });
        picker.BringIntoView();
        Layout(fixture.Window);
        Assert.True(picker.IsEffectivelyVisible);
        var selection = picker.Items.OfType<ThemeOption>().Single(x => x.Theme == BrewTheme.Ibre);
        var expected = fixture.Appearance.Options;
        expected.SetDay(DayOfWeek.Wednesday, BrewTheme.Ibre);
        fixture.Preferences.ClearReceivedCalls();

        // Change the rendered control, so a missing or one-way XAML binding fails this test.
        picker.SetCurrentValue(ComboBox.SelectedItemProperty, selection);
        Layout(fixture.Window);

        await fixture.Preferences.Received(1).SaveAppearanceAsync(
            Arg.Is<AppearanceOptions>(value => value.EquivalentTo(expected)), Arg.Any<CancellationToken>());
        Assert.Equal(BrewTheme.Ibre, fixture.Appearance.Options.ForDay(DayOfWeek.Wednesday));
        Assert.Equal(BrewTheme.Ibre, Assert.IsType<ThemeOption>(picker.SelectedItem).Theme);
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
        foreach (var theme in Enum.GetValues<BrewTheme>())
        {
            await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
            Layout(fixture.Window);
            Assert.Same(timerView, fixture.Main.CurrentView);
            Assert.Equal(session.Id, Assert.Single(fixture.Timer.GetActiveSessions()).Id);
            if (theme != BrewTheme.Obsidian) Assert.Contains(theme.ToString().ToLowerInvariant(), fixture.View.Classes);
            Assert.Equal(BrewThemeVariants.For(theme), Application.Current!.RequestedThemeVariant);
        }
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

    [AvaloniaTheory]
    [InlineData(800)]
    [InlineData(320)]
    public async Task EveryAmbientMode_RendersInAllAppearances(int width)
    {
        foreach (var theme in Enum.GetValues<BrewTheme>())
        {
            using var fixture = new Fixture(width, 480);
            await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
            await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
            foreach (var mode in Enum.GetValues<AmbientMode>())
            {
                fixture.Main.Ambient.Mode = mode;
                Layout(fixture.Window);
                if (mode is AmbientMode.Card or AmbientMode.Orbit
                    && Environment.GetEnvironmentVariable("BREWALERT_RENDER_DIR") is not null)
                    await Task.Delay(650);
                Layout(fixture.Window);
                Assert.True(fixture.Main.Ambient.IsVisible);
                var tag = mode is AmbientMode.SilkWeave or AmbientMode.OrbitalDots or AmbientMode.MoonWaves
                    ? "ThemeBackdrop" : mode.ToString();
                var layers = fixture.View.GetVisualDescendants().OfType<Control>()
                    .Where(x => x.Tag is string value && value == tag).ToArray();
                Assert.NotEmpty(layers);
                Assert.All(layers, layer => Assert.True(layer.IsEffectivelyVisible, $"{theme}/{mode} layer is hidden."));
                var overlay = fixture.View.GetVisualDescendants().OfType<AmbientOverlayView>().Single();
                var root = Assert.IsType<Panel>(overlay.Content);
                Assert.Contains("ambient-live", root.Classes);
                var backdrop = overlay.FindControl<ThemeBackdrop>("ThemeBackdrop")!;
                Assert.Equal(fixture.Main.Ambient.IsThemeBackdropMode, backdrop.IsRunning);
                Assert.Equal(mode, backdrop.Mode);
                Assert.Equal(mode == AmbientMode.Silk, overlay.FindControl<SilkBackdrop>("SilkBackdrop")!.IsRunning);
                var clock = fixture.View.GetVisualDescendants().OfType<AnimatedClock>()
                    .Single(text => text.Text == fixture.Main.Ambient.ClockTime);
                var position = clock.TranslatePoint(new Point(0, 0), fixture.Window)!.Value;
                Assert.InRange(position.X, 0, fixture.Window.Width - clock.Bounds.Width);
                Assert.InRange(position.Y, 0, fixture.Window.Height - clock.Bounds.Height);
                Capture(fixture.Window, theme + "-ambient-" + mode + "-" + width);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(BrewTheme.Sumi)]
    [InlineData(BrewTheme.Ibre)]
    [InlineData(BrewTheme.Aizome)]
    public async Task SignatureAmbient_KeepsFreshnessAndWakeHintSeparateOnNarrowScreens(BrewTheme theme)
    {
        using var fixture = new Fixture(320, 480);
        await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
        await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
        fixture.Main.Ambient.Mode = AmbientMode.MoonWaves;
        Layout(fixture.Window);
        var summary = fixture.View.GetVisualDescendants().OfType<StackPanel>()
            .Single(x => x.Classes.Contains("signature-summary"));
        var hint = fixture.View.GetVisualDescendants().OfType<TextBlock>()
            .Single(x => x.Text == fixture.Main.Ambient.TapHint);
        var origin = summary.TranslatePoint(new Point(), fixture.Window)!.Value;
        var hintOrigin = hint.TranslatePoint(new Point(), fixture.Window)!.Value;
        Assert.True(summary.IsEffectivelyVisible);
        Assert.InRange(origin.X, 0, 320 - summary.Bounds.Width);
        Assert.True(origin.Y + summary.Bounds.Height < hintOrigin.Y);
        var clock = fixture.View.GetVisualDescendants().OfType<AnimatedClock>().Single();
        var clockOrigin = clock.TranslatePoint(new Point(), fixture.Window)!.Value;
        Assert.InRange(clockOrigin.X, 0, 320 - clock.Bounds.Width);
        Assert.True(clockOrigin.Y + clock.Bounds.Height < origin.Y);
        if (Environment.GetEnvironmentVariable("BREWALERT_RENDER_DIR") is not null) await Task.Delay(900);
        Layout(fixture.Window);
        Capture(fixture.Window, theme + "-ambient-narrow");
    }

    [AvaloniaFact]
    public async Task Orbit_LeavesClockAndDateClearAtPiAndNarrowSizes()
    {
        foreach (var width in new[] { 800, 320 })
        {
            using var fixture = new Fixture(width, 480);
            await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
            fixture.Main.Ambient.Mode = AmbientMode.Orbit;
            Layout(fixture.Window);
            var stage = fixture.View.GetVisualDescendants().OfType<Grid>().Single(x => x.Name == "OrbitStage");
            var center = stage.TranslatePoint(new Point(stage.Width / 2, stage.Height / 2), fixture.Window)!.Value;
            var iconSize = stage.Children.OfType<Viewbox>().Max(icon => icon.Width);
            var innerRadius = stage.Width / 2 - iconSize / 2 - iconSize / Math.Sqrt(2);
            foreach (var text in fixture.View.GetVisualDescendants().OfType<Control>()
                .Where(x => x.Name is "AmbientClock" or "AmbientDate"))
            {
                var origin = text.TranslatePoint(new Point(0, 0), fixture.Window)!.Value;
                foreach (var corner in new[] { origin, origin + new Vector(text.Bounds.Width, 0),
                    origin + new Vector(0, text.Bounds.Height), origin + new Vector(text.Bounds.Width, text.Bounds.Height) })
                    Assert.True(Math.Sqrt(Math.Pow(corner.X - center.X, 2) + Math.Pow(corner.Y - center.Y, 2))
                        < innerRadius, text.Name + " enters the orbit path.");
            }
            Capture(fixture.Window, "Obsidian-orbit-clearance-" + width);
        }
    }

    [AvaloniaTheory]
    [InlineData(BrewTheme.Obsidian)]
    [InlineData(BrewTheme.Porcelain)]
    [InlineData(BrewTheme.Sumi)]
    [InlineData(BrewTheme.Ibre)]
    [InlineData(BrewTheme.Aizome)]
    public async Task AmbientAnimations_RunOnlyWhileTheirModeIsOnScreen(BrewTheme theme)
    {
        using var fixture = new Fixture(800, 480);
        await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
        var aurora = fixture.View.GetLogicalDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
            .Single(x => x.Classes.Contains("aurora-a"));
        double Angle() => ((RotateTransform)aurora.RenderTransform!).Angle;

        Assert.False(IsAnimating(fixture.Window, Angle), "Aurora animates while hidden.");
        await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
        fixture.Main.Ambient.Mode = AmbientMode.Aurora;
        Assert.True(IsAnimating(fixture.Window, Angle), $"Aurora does not animate in {theme}.");
        fixture.Main.Ambient.Mode = AmbientMode.MoonWaves;
        Assert.False(IsAnimating(fixture.Window, Angle), "Aurora keeps animating in another mode.");
        fixture.Main.Ambient.Mode = AmbientMode.Aurora;
        Assert.True(IsAnimating(fixture.Window, Angle), "Aurora does not resume.");
        foreach (var nextTheme in Enum.GetValues<BrewTheme>())
        {
            await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = nextTheme.ToString() });
            Assert.True(IsAnimating(fixture.Window, Angle), $"Theme switch to {nextTheme} stopped Aurora.");
        }
        fixture.Main.DismissAmbientCommand.Execute(null);
        Assert.False(IsAnimating(fixture.Window, Angle), "Aurora keeps animating after dismissal.");
    }

    [AvaloniaTheory]
    [InlineData(BrewTheme.Obsidian)]
    [InlineData(BrewTheme.Porcelain)]
    [InlineData(BrewTheme.Sumi)]
    [InlineData(BrewTheme.Ibre)]
    [InlineData(BrewTheme.Aizome)]
    public async Task EveryLegacyMotion_MovesOnlyInItsVisibleMode(BrewTheme theme)
    {
        using var fixture = new Fixture(800, 480);
        await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
        await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
        foreach (var (mode, name) in new[] {
            (AmbientMode.Card, "ambient-ring-cw"), (AmbientMode.Dvd, "DvdLogo"),
            (AmbientMode.Orbit, "OrbitStage"), (AmbientMode.Aurora, "aurora-a"),
            (AmbientMode.BrewRain, "brew-rain-a"), (AmbientMode.Kaleidoscope, "kaleido-a"),
            (AmbientMode.Meteor, "meteor-a"), (AmbientMode.Liquid, "liquid-wave-a"),
            (AmbientMode.Hyperspace, "warp-streak-a"), (AmbientMode.Radar, "radar-ring-a"),
            (AmbientMode.Fireflies, "firefly-a"), (AmbientMode.Prism, "prism-a") })
        {
            var control = fixture.View.GetLogicalDescendants().OfType<Control>()
                .First(x => x.Name == name || x.Classes.Contains(name));
            double Motion()
            {
                var matrix = control.RenderTransform!.Value;
                return matrix.M11 + matrix.M12 + matrix.M21 + matrix.M22 + matrix.M31 + matrix.M32;
            }
            fixture.Main.Ambient.Mode = mode;
            Assert.True(IsAnimating(fixture.Window, Motion), $"{theme}/{mode} does not move.");
            fixture.Main.Ambient.Mode = AmbientMode.MoonWaves;
            Assert.False(IsAnimating(fixture.Window, Motion), $"{theme}/{mode} keeps moving while inactive.");
        }
    }

    [AvaloniaFact]
    public async Task AmbientEffectColours_FollowThemeAndKeepTheirAlpha()
    {
        using var fixture = new Fixture(800, 480);
        await fixture.Main.ShowAmbientCommand.ExecuteAsync(null);
        fixture.Main.Ambient.Mode = AmbientMode.Fireflies;
        var particle = fixture.View.GetLogicalDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
            .First(x => x.Classes.Contains("firefly-a"));
        foreach (var (theme, expected) in new[] {
            (BrewTheme.Obsidian, "#ffd3d3d3"), (BrewTheme.Porcelain, "#ff2c2c2c"),
            (BrewTheme.Sumi, "#ff395239"), (BrewTheme.Ibre, "#ffcfd8cf"), (BrewTheme.Aizome, "#ffccd6d0") })
        {
            await fixture.Appearance.SaveAsync(new AppearanceOptions { Mode = theme.ToString() });
            Layout(fixture.Window);
            Assert.Equal(Color.Parse(expected), Assert.IsAssignableFrom<ISolidColorBrush>(particle.Fill).Color);
        }
    }

    private static bool IsAnimating(Window window, Func<double> value)
    {
        Layout(window);
        var before = value();
        Thread.Sleep(300);
        Layout(window);
        return value() != before;
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
        public IPreferencesService Preferences { get; } = Substitute.For<IPreferencesService>();

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
            services.AddSingleton(Preferences);
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
            var network = Substitute.For<INetworkInfoService>();
            network.GetStatusAsync(Arg.Any<CancellationToken>())
                .Returns(new NetworkStatus("Office-WiFi", true, ["192.168.1.23"]));
            services.AddSingleton(network);
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
                Application.Current!.RequestedThemeVariant = BrewThemeVariants.For(Appearance.CurrentTheme);
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
