using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using BrewAlert.UI.Converters;

namespace BrewAlert.UI.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = Avalonia.Media.Color.Parse("#a9aba6") };
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = Avalonia.Media.Color.Parse("#51574f") };
        Styles.Add(fluent);
        Styles.Add(new StyleInclude(new Uri("avares://BrewAlert.UI/")) { Source = new Uri("avares://BrewAlert.UI/Themes/BrewAlertTheme.axaml") });
        Resources["BrewIcon"] = new BrewIconConverter();
        DataTemplates.Add(new BrewAlert.UI.ViewLocator());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .UseSkia()
        .WithInterFont();
}
