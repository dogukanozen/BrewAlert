using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Notifications;
using BrewAlert.Infrastructure.Persistence;
using BrewAlert.UI.Constants;
using BrewAlert.UI.Services;
using UIPreferencesService = BrewAlert.UI.Services.IPreferencesService;
using BrewAlert.UI.ViewModels;
using BrewAlert.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Avalonia.Styling;
using Avalonia.Threading;
using System.ComponentModel;

namespace BrewAlert.UI;

public partial class App : Application
{
    private IServiceProvider? _services;
    private IAppearanceService? _appearance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DisableAvaloniaDataAnnotationValidation();
        _services = ConfigureServices();
        HandleUnhandledExceptions(_services.GetRequiredService<ILogger<App>>());
        _appearance = _services.GetRequiredService<IAppearanceService>();
        _appearance.PropertyChanged += OnAppearanceChanged;
        ApplyAppearance();

        var mainVm = _services.GetRequiredService<MainWindowViewModel>();

        // Eagerly resolve so the singletons subscribe to BrewCompleted at startup, not when
        // the first view that happens to depend on them is created.
        _ = _services.GetRequiredService<IBrewHistoryService>();
        _ = _services.GetRequiredService<IBrewCompletionNotificationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) =>
            {
                if (_appearance is not null) _appearance.PropertyChanged -= OnAppearanceChanged;
                if (_services is IDisposable d) d.Dispose();
            };

            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Running brews live only in memory: a UI exception that kills the process loses every
    // timer and its Teams notification, so log it and keep the kiosk running instead.
    private static void HandleUnhandledExceptions(ILogger logger)
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            logger.LogCritical(e.Exception, "Unhandled UI exception; the app keeps running.");
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception; the process is terminating.");
    }

    private static IServiceProvider ConfigureServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddJsonFile(BrewAlertPaths.Preferences, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("BREWALERT__")
            .Build();

        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // Configuration
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConfigurationRoot>(configuration);
        services.Configure<TeamsNotificationOptions>(
            configuration.GetSection(TeamsNotificationOptions.SectionPath));
        services.Configure<TeamsGraphOptions>(
            configuration.GetSection(TeamsGraphOptions.SectionPath));
        services.Configure<NotificationProviderOptions>(
            configuration.GetSection(NotificationProviderOptions.SectionPath));
        services.Configure<LanguageOptions>(
            configuration.GetSection(LanguageOptions.SectionPath));
        services.Configure<AppearanceOptions>(configuration.GetSection(AppearanceOptions.SectionPath));

        // Core services
        services.AddSingleton<IBrewTimerService, BrewTimerService>();
        services.AddSingleton<BrewProfileService>();

        // Infrastructure
        services.AddSingleton<IProfileRepository>(sp =>
            new JsonProfileRepository(sp.GetRequiredService<ILogger<JsonProfileRepository>>()));
        services.AddSingleton<IBrewHistoryRepository>(sp =>
            new SqliteBrewHistoryRepository(BrewAlertPaths.History,
                sp.GetRequiredService<ILogger<SqliteBrewHistoryRepository>>()));
        services.AddSingleton<IBrewHistoryService, BrewHistoryService>();

        // Notification services — active back-end selected via BrewAlert:Notifications:Provider
        services.AddHttpClient();
        services.AddHttpClient(nameof(TeamsWebhookNotifier))
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<TeamsNotificationOptions>>().CurrentValue;
                if (opts.TimeoutSeconds > 0) client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });

        services.AddHttpClient(nameof(TeamsGraphNotifier))
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<TeamsGraphOptions>>().CurrentValue;
                if (opts.TimeoutSeconds > 0) client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });

        services.AddSingleton<TeamsWebhookNotifier>();
        services.AddSingleton<TeamsGraphNotifier>();
        services.AddSingleton<ConsoleNotifier>();
        services.AddSingleton<INotificationService, RoutingNotificationService>();

        // Preferences & localization
        services.AddSingleton<UIPreferencesService, PreferencesService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppearanceService, AppearanceService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // Notification coordinator — singleton so it outlives any individual timer view
        services.AddSingleton<IBrewCompletionNotificationService, BrewCompletionNotificationService>();

        // Update Service
        services.AddSingleton<IUpdateService, UpdateService>();

        // Network status for the Settings screen (SSH access to the Pi)
        services.AddSingleton<INetworkInfoService, NetworkInfoService>();

        // ViewModels
        services.AddSingleton<AmbientOverlayViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ActiveBrewsViewModel>();
        services.AddTransient<ProfileListViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppearanceService.CurrentTheme)) ApplyAppearance();
    }

    private void ApplyAppearance() => RequestedThemeVariant = _appearance?.IsPorcelain == true
        ? ThemeVariant.Light : ThemeVariant.Dark;

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var plugins = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in plugins)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
