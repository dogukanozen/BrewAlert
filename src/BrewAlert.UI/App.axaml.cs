using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using BrewAlert.Core;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Notifications;
using BrewAlert.Infrastructure.Persistence;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using BrewAlert.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;

namespace BrewAlert.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var services = ConfigureServices();

            // MainWindowViewModel is Singleton — resolve once and attach to the window
            var mainVm = services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var preferencesPath = BrewAlertConstants.PreferencesPath;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddJsonFile(preferencesPath, optional: true, reloadOnChange: true)
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

        // Core services
        services.AddSingleton<IBrewTimerService, BrewTimerService>();
        services.AddSingleton<BrewProfileService>();

        // Infrastructure
        services.AddSingleton<IProfileRepository>(sp =>
            new JsonProfileRepository(sp.GetRequiredService<ILogger<JsonProfileRepository>>()));

        // Notification services — active back-end selected via BrewAlert:Notifications:Provider
        // ("Graph" | "Webhook" | "Console"). RoutingNotificationService reads IOptionsMonitor
        // on every call so switching the provider in preferences.json is instant.
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

        // Navigation — Singleton so all ViewModels share the same navigation state
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        // MainWindowViewModel is Singleton — it lives as long as the window
        services.AddSingleton<MainWindowViewModel>();
        // Child ViewModels are Transient — fresh instance per navigation
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
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
