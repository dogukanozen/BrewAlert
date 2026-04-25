using BrewAlert.Core;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BrewAlert.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly INotificationService _notificationService;
    private readonly BrewProfileService _profileService;
    private readonly IPreferencesService _preferencesService;
    private readonly IDisposable? _configSubscription;

    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _selectedProvider = string.Empty;

    // Graph config display
    public string TenantId { get; }
    public string ClientId { get; }
    public string ChatId { get; }
    public bool IsGraphConfigured { get; }

    // Webhook config display
    public string WebhookUrl { get; }
    public bool IsWebhookConfigured { get; }

    public bool IsGraphSelected => SelectedProvider == NotificationProvider.Graph;
    public bool IsWebhookSelected => SelectedProvider == NotificationProvider.Webhook;

    /// <summary>True when the currently selected provider is fully configured.</summary>
    public bool IsConfigured => SelectedProvider switch
    {
        NotificationProvider.Graph => IsGraphConfigured,
        NotificationProvider.Webhook => IsWebhookConfigured,
        NotificationProvider.Console => true,
        _ => false,
    };

    public ObservableCollection<EditableProfileViewModel> Profiles { get; } = new();

    public SettingsViewModel(
        INotificationService notificationService,
        IOptionsMonitor<TeamsGraphOptions> graphOptions,
        IOptionsMonitor<TeamsNotificationOptions> webhookOptions,
        IOptionsMonitor<NotificationProviderOptions> providerOptions,
        BrewProfileService profileService,
        IPreferencesService preferencesService)
    {
        _notificationService = notificationService;
        _profileService = profileService;
        _preferencesService = preferencesService;

        var g = graphOptions.CurrentValue;
        TenantId = Mask(g.TenantId);
        ClientId = Mask(g.ClientId);
        ChatId = g.ChatId;
        IsGraphConfigured = !string.IsNullOrWhiteSpace(g.TenantId)
            && !string.IsNullOrWhiteSpace(g.ClientId)
            && !string.IsNullOrWhiteSpace(g.ClientSecret)
            && !string.IsNullOrWhiteSpace(g.ChatId);

        var w = webhookOptions.CurrentValue;
        WebhookUrl = Mask(w.WebhookUrl);
        IsWebhookConfigured = !string.IsNullOrWhiteSpace(w.WebhookUrl);

        // Sync with external config changes (e.g. manual file edit)
        _configSubscription = providerOptions.OnChange(opt => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedProvider = opt.Provider);
        });

        // Initialize state
        SelectedProvider = providerOptions.CurrentValue.Provider;

        _ = LoadProfilesAsync();
    }

    public void Dispose() => _configSubscription?.Dispose();

    partial void OnSelectedProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsGraphSelected));
        OnPropertyChanged(nameof(IsWebhookSelected));
        OnPropertyChanged(nameof(IsConfigured));
    }

    [RelayCommand]
    private async Task SetProvider(string provider)
    {
        if (SelectedProvider == provider) return;

        IsBusy = true;
        try
        {
            await _preferencesService.SaveNotificationProviderAsync(provider);
            SelectedProvider = provider;
            TestResult = string.Empty;
        }
        catch (Exception ex)
        {
            TestResult = $"❌ Tercih kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync();
            foreach (var p in profiles)
            {
                Profiles.Add(new EditableProfileViewModel(p, _profileService));
            }
        }
        catch (Exception ex)
        {
            TestResult = $"❌ Profiller yüklenemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetProfiles()
    {
        IsBusy = true;
        try
        {
            var currentProfiles = await _profileService.GetAllProfilesAsync();
            foreach (var p in currentProfiles)
            {
                await _profileService.DeleteProfileAsync(p.Id);
            }

            Profiles.Clear();
            await LoadProfilesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (!IsConfigured)
        {
            var channel = SelectedProvider switch
            {
                NotificationProvider.Graph => "Teams Graph",
                NotificationProvider.Webhook => "Teams Webhook",
                _ => SelectedProvider
            };
            TestResult = $"❌ {channel} yapılandırılmamış. Uygulama ayarlarını kontrol edin.";
            return;
        }

        IsBusy = true;
        TestResult = "Bağlanıyor...";
        try
        {
            var success = await _notificationService.TestConnectionAsync();
            TestResult = success ? "✅ Bağlantı başarılı! Test kartı gönderildi." : "❌ Bağlantı başarısız.";
        }
        catch (Exception ex)
        {
            TestResult = $"❌ Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendTestNotification()
    {
        if (!IsConfigured)
        {
            var channel = SelectedProvider switch
            {
                NotificationProvider.Graph => "Teams Graph",
                NotificationProvider.Webhook => "Teams Webhook",
                _ => SelectedProvider
            };
            TestResult = $"❌ {channel} yapılandırılmamış. Uygulama ayarlarını kontrol edin.";
            return;
        }

        IsBusy = true;
        TestResult = "Gönderiliyor...";
        try
        {
            var fakeSession = new BrewSession
            {
                Profile = new BrewProfile
                {
                    Name = "Test Brew",
                    Type = BrewType.Coffee,
                    BrewDuration = TimeSpan.FromMinutes(4),
                    Icon = "☕"
                },
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-4),
                EndsAtUtc = DateTime.UtcNow,
                Remaining = TimeSpan.Zero,
                State = BrewSessionState.Completed
            };

            var result = await _notificationService.SendBrewCompletedAsync(fakeSession);
            TestResult = result.IsSuccess
                ? "✅ Test bildirimi gönderildi!"
                : $"❌ Gönderilemedi: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            TestResult = $"❌ Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Mask(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(boş)" :
        value.Length <= 8 ? new string('●', value.Length) :
        value[..8] + "••••••••";
}

public partial class EditableProfileViewModel : ViewModelBase
{
    private readonly BrewProfile _profile;
    private readonly BrewProfileService _service;

    public string Name => _profile.Name;
    public string Icon => _profile.Icon;

    [ObservableProperty] private TimeSpan _duration;

    public EditableProfileViewModel(BrewProfile profile, BrewProfileService service)
    {
        _profile = profile;
        _service = service;
        Duration = profile.BrewDuration;
    }

    [RelayCommand]
    private async Task IncreaseTime()
    {
        Duration = Duration.Add(TimeSpan.FromMinutes(1));
        _profile.BrewDuration = Duration;
        await _service.SaveProfileAsync(_profile);
    }

    [RelayCommand]
    private async Task DecreaseTime()
    {
        if (Duration.TotalMinutes > 1)
        {
            Duration = Duration.Subtract(TimeSpan.FromMinutes(1));
            _profile.BrewDuration = Duration;
            await _service.SaveProfileAsync(_profile);
        }
    }
}
