using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace BrewAlert.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;

    private readonly BrewProfileService _profileService;

    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string TenantId { get; }
    public string ClientId { get; }
    public string ChatId { get; }
    public bool IsConfigured { get; }

    public ObservableCollection<EditableProfileViewModel> Profiles { get; } = new();

    public SettingsViewModel(
        INotificationService notificationService,
        IOptions<TeamsGraphOptions> graphOptions,
        IOptions<TeamsNotificationOptions> webhookOptions,
        BrewProfileService profileService)
    {
        _notificationService = notificationService;
        _profileService = profileService;
        var g = graphOptions.Value;
        var w = webhookOptions.Value;

        TenantId = Mask(g.TenantId);
        ClientId = Mask(g.ClientId);
        ChatId = g.ChatId;

        var graphConfigured = g.Enabled
            && !string.IsNullOrWhiteSpace(g.TenantId)
            && !string.IsNullOrWhiteSpace(g.ClientId)
            && !string.IsNullOrWhiteSpace(g.ClientSecret)
            && !string.IsNullOrWhiteSpace(g.ChatId);

        var webhookConfigured = w.Enabled && !string.IsNullOrWhiteSpace(w.WebhookUrl);

        IsConfigured = graphConfigured || webhookConfigured;

        _ = LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync();
        foreach (var p in profiles)
        {
            Profiles.Add(new EditableProfileViewModel(p, _profileService));
        }
    }

    [RelayCommand]
    private async Task ResetProfiles()
    {
        IsBusy = true;
        try
        {
            var currentProfiles = await _profileService.GetAllProfilesAsync();
            // Call repository directly or use service if we had a clear method.
            // But deleting one by one is safe here.
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
            TestResult = "❌ Bildirim servisi yapılandırılmamış. appsettings.json dosyasını kontrol et.";
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
            TestResult = "❌ Bildirim servisi yapılandırılmamış. appsettings.json dosyasını kontrol et.";
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
