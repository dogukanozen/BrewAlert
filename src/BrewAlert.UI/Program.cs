using Avalonia;
using Avalonia.X11;
using Avalonia.LinuxFramebuffer;
using System;
using System.Linq;

namespace BrewAlert.UI;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = BuildAvaloniaApp();

        if (args.Contains("--drm"))
        {
            // Raspberry Pi KMS/DRM (Direct Rendering Manager)
            // Kiosk modu için en iyi performans ve tam ekran desteği
            SilenceConsole();
            builder.StartLinuxDrm(args, card: null, scaling: 1.0);
        }
        else
        {
            // Masaüstü (X11/Wayland/Windows)
            builder.StartWithClassicDesktopLifetime(args);
        }
    }

    private static void SilenceConsole()
    {
        // Konsol imlecini gizle ve çıktıların UI üzerine binmesini engellemek için hazırlık
        try { Console.CursorVisible = false; } catch { }
    }

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
