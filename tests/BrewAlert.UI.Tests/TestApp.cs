using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace BrewAlert.UI.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
