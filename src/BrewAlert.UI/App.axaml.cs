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
using System;
using System.Linq;

namespace BrewAlert.UI;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            _services = ConfigureServices();

            desktop.ShutdownRequested += (_, _) =>
            {
                if (_services is IDisposable d) d.Dispose();
            };

            var mainVm = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
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
        services.Configure<TeamsNotificationOptions>(
            configuration.GetSection(TeamsNotificationOptions.SectionPath));
        services.Configure<TeamsGraphOptions>(
            configuration.GetSection(TeamsGraphOptions.SectionPath));
        services.Configure<NotificationProviderOptions>(
            configuration.GetSection(NotificationProviderOptions.SectionPath));
        services.Configure<LanguageOptions>(
            configuration.GetSection(LanguageOptions.SectionPath));

        // Core services
        services.AddSingleton<IBrewTimerService, BrewTimerService>();
        services.AddSingleton<BrewProfileService>();

        // Infrastructure
        services.AddSingleton<IProfileRepository>(sp =>
            new JsonProfileRepository(sp.GetRequiredService<ILogger<JsonProfileRepository>>()));

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
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<BrewTimerViewModel>();
        services.AddTransient<ProfileListViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var plugins = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in plugins)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
