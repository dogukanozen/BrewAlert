using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.Services;
using IPreferencesService = BrewAlert.UI.Services.IPreferencesService;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BrewAlert.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly INotificationService _notificationService;
    private readonly BrewProfileService _profileService;
    private readonly IPreferencesService _preferencesService;
    private readonly ILocalizationService _loc;
    private readonly IOptionsMonitor<TeamsGraphOptions> _graphOptions;
    private readonly IOptionsMonitor<TeamsNotificationOptions> _webhookOptions;

    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _selectedProvider = string.Empty;
    [ObservableProperty] private string _currentLanguage = string.Empty;

    // Localized UI labels
    [ObservableProperty] private string _durationSettingsTitle = string.Empty;
    [ObservableProperty] private string _resetButtonText = string.Empty;
    [ObservableProperty] private string _notificationSettingsTitle = string.Empty;
    [ObservableProperty] private string _notificationChannelLabel = string.Empty;
    [ObservableProperty] private string _activeChannelLabel = string.Empty;
    [ObservableProperty] private string _testTitle = string.Empty;
    [ObservableProperty] private string _testConnectionText = string.Empty;
    [ObservableProperty] private string _sendTestNotificationText = string.Empty;
    [ObservableProperty] private string _languageLabel = string.Empty;
    [ObservableProperty] private string _addProfileText = string.Empty;
    [ObservableProperty] private string _webhookStatusText = string.Empty;
    [ObservableProperty] private string _graphStatusText = string.Empty;
    [ObservableProperty] private string _webhookHintText = string.Empty;
    [ObservableProperty] private string _graphHintText = string.Empty;

    // Graph config display (masked)
    public string TenantId { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string ChatId { get; private set; } = string.Empty;
    public bool IsGraphConfigured { get; private set; }

    // Webhook config display (masked)
    public string WebhookUrl { get; private set; } = string.Empty;
    public bool IsWebhookConfigured { get; private set; }

    public bool IsGraphSelected => SelectedProvider == NotificationProvider.Graph;
    public bool IsWebhookSelected => SelectedProvider == NotificationProvider.Webhook;

    public bool IsConfigured => SelectedProvider switch
    {
        NotificationProvider.Graph => IsGraphConfigured,
        NotificationProvider.Webhook => IsWebhookConfigured,
        NotificationProvider.Console => true,
        _ => false,
    };

    // New profile form
    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private int _newProfileDuration = 5;
    [ObservableProperty] private BrewType _newProfileType = BrewType.Tea;

    public ObservableCollection<EditableProfileViewModel> Profiles { get; } = new();

    public SettingsViewModel(
        INotificationService notificationService,
        IOptionsMonitor<TeamsGraphOptions> graphOptions,
        IOptionsMonitor<TeamsNotificationOptions> webhookOptions,
        IOptionsMonitor<NotificationProviderOptions> providerOptions,
        BrewProfileService profileService,
        IPreferencesService preferencesService,
        ILocalizationService loc)
    {
        _notificationService = notificationService;
        _graphOptions = graphOptions;
        _webhookOptions = webhookOptions;
        _profileService = profileService;
        _preferencesService = preferencesService;
        _loc = loc;

        _selectedProvider = providerOptions.CurrentValue.Provider;
        _currentLanguage = loc.CurrentLanguage;

        RefreshConfigDisplay();
        _loc.LanguageChanged += OnLanguageChanged;
        RefreshLocalizedStrings();

        _ = LoadProfilesAsync();
    }

    private void RefreshConfigDisplay()
    {
        var g = _graphOptions.CurrentValue;
        TenantId = Mask(g.TenantId);
        ClientId = Mask(g.ClientId);
        ChatId = Mask(g.ChatId);
        IsGraphConfigured = !string.IsNullOrWhiteSpace(g.TenantId)
            && !string.IsNullOrWhiteSpace(g.ClientId)
            && !string.IsNullOrWhiteSpace(g.ClientSecret)
            && !string.IsNullOrWhiteSpace(g.ChatId);

        var w = _webhookOptions.CurrentValue;
        WebhookUrl = Mask(w.WebhookUrl);
        IsWebhookConfigured = !string.IsNullOrWhiteSpace(w.WebhookUrl);
    }

    private void RefreshLocalizedStrings()
    {
        DurationSettingsTitle = _loc.Get("DurationSettings");
        ResetButtonText = _loc.Get("ResetToDefaults");
        NotificationSettingsTitle = _loc.Get("NotificationSettings");
        NotificationChannelLabel = _loc.Get("NotificationChannel");
        ActiveChannelLabel = _loc.Get("ActiveChannel");
        TestTitle = _loc.Get("TestTitle");
        TestConnectionText = _loc.Get("TestConnection");
        SendTestNotificationText = _loc.Get("SendTestNotification");
        LanguageLabel = _loc.Get("Language");
        AddProfileText = _loc.Get("AddProfile");
        WebhookStatusText = IsWebhookConfigured ? _loc.Get("WebhookConfigured") : _loc.Get("NotConfigured");
        GraphStatusText = IsGraphConfigured ? _loc.Get("GraphConfigured") : _loc.Get("NotConfigured");
        WebhookHintText = _loc.Get("WebhookHint");
        GraphHintText = _loc.Get("GraphHint");

        foreach (var p in Profiles)
            p.RefreshLocalization(_loc);
    }

    private void OnLanguageChanged(string lang)
    {
        CurrentLanguage = lang;
        RefreshLocalizedStrings();
    }

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
            TestResult = string.Format(_loc.Get("SaveFailed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetLanguage(string language)
    {
        if (CurrentLanguage == language) return;
        IsBusy = true;
        try
        {
            await _preferencesService.SaveLanguageAsync(language);
            _loc.SetLanguage(language);
        }
        catch (Exception ex)
        {
            TestResult = string.Format(_loc.Get("SaveFailed"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync();
        Profiles.Clear();
        foreach (var p in profiles)
            Profiles.Add(new EditableProfileViewModel(p, _profileService, _loc));
    }

    [RelayCommand]
    private async Task ResetProfiles()
    {
        IsBusy = true;
        try
        {
            var current = await _profileService.GetAllProfilesAsync();
            foreach (var p in current)
                await _profileService.DeleteProfileAsync(p.Id);
            Profiles.Clear();
            await LoadProfilesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void IncreaseDuration()
    {
        if (NewProfileDuration < 120) NewProfileDuration++;
    }

    [RelayCommand]
    private void DecreaseDuration()
    {
        if (NewProfileDuration > 1) NewProfileDuration--;
    }

    [RelayCommand]
    private async Task AddProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName) || NewProfileDuration < 1) return;

        IsBusy = true;
        try
        {
            var profile = new BrewProfile
            {
                Name = NewProfileName.Trim(),
                Type = NewProfileType,
                BrewDuration = TimeSpan.FromMinutes(NewProfileDuration),
                Icon = NewProfileType switch
                {
                    BrewType.Tea => "♨",
                    BrewType.Coffee => "☕",
                    _ => "🫖",
                },
            };

            await _profileService.SaveProfileAsync(profile);
            Profiles.Add(new EditableProfileViewModel(profile, _profileService, _loc));
            NewProfileName = string.Empty;
            NewProfileDuration = 5;
        }
        catch (Exception ex)
        {
            TestResult = string.Format(_loc.Get("ErrorPrefix"), ex.Message);
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
            var channel = SelectedProvider == NotificationProvider.Graph ? "Teams Graph" : "Teams Webhook";
            TestResult = string.Format(_loc.Get("NotConfiguredError"), channel);
            return;
        }

        IsBusy = true;
        TestResult = _loc.Get("Connecting");
        try
        {
            var success = await _notificationService.TestConnectionAsync();
            TestResult = success ? _loc.Get("ConnectionSuccess") : _loc.Get("ConnectionFailed");
        }
        catch (Exception ex)
        {
            TestResult = string.Format(_loc.Get("ErrorPrefix"), ex.Message);
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
            var channel = SelectedProvider == NotificationProvider.Graph ? "Teams Graph" : "Teams Webhook";
            TestResult = string.Format(_loc.Get("NotConfiguredError"), channel);
            return;
        }

        IsBusy = true;
        TestResult = _loc.Get("Sending");
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
                ? _loc.Get("TestNotificationSent")
                : string.Format(_loc.Get("CouldNotSend"), result.ErrorMessage);
        }
        catch (Exception ex)
        {
            TestResult = string.Format(_loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Mask(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(empty)" :
        value.Length <= 8 ? new string('●', value.Length) :
        value[..8] + "••••••••";

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}

public partial class EditableProfileViewModel : ViewModelBase
{
    private readonly BrewProfile _profile;
    private readonly BrewProfileService _service;
    private ILocalizationService _loc;

    public string Name => _profile.Name;
    public string Icon => _profile.Icon;

    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private string _deleteButtonText = string.Empty;
    [ObservableProperty] private bool _isDeleted;

    public EditableProfileViewModel(BrewProfile profile, BrewProfileService service, ILocalizationService loc)
    {
        _profile = profile;
        _service = service;
        _loc = loc;
        Duration = profile.BrewDuration;
        DeleteButtonText = loc.Get("DeleteProfile");
    }

    public void RefreshLocalization(ILocalizationService loc)
    {
        _loc = loc;
        DeleteButtonText = loc.Get("DeleteProfile");
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

    [ObservableProperty] private string _deleteErrorText = string.Empty;

    [RelayCommand]
    private async Task Delete()
    {
        try
        {
            await _service.DeleteProfileAsync(_profile.Id);
            IsDeleted = true;
        }
        catch (Exception ex)
        {
            DeleteErrorText = ex.Message;
        }
    }
}
