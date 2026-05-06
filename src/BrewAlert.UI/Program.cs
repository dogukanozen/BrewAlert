using Avalonia;
using Avalonia.X11;
using System;

namespace BrewAlert.UI;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                // EGL (OpenGL ES) → GLX → software fallback sırası
                // Raspberry Pi VideoCore GPU'sunu kullanır
                RenderingMode = [X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software]
            })
            .WithInterFont();

#if DEBUG
        builder = builder.LogToTrace();
#endif
        return builder;
    }
}
