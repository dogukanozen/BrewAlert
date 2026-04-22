using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace BrewAlert.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;

    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string TenantId { get; }
    public string ClientId { get; }
    public string ChatId { get; }
    public bool IsConfigured { get; }

    public SettingsViewModel(
        INotificationService notificationService,
        IOptions<TeamsGraphOptions> options)
    {
        _notificationService = notificationService;
        var o = options.Value;

        TenantId = Mask(o.TenantId);
        ClientId = Mask(o.ClientId);
        ChatId = o.ChatId;
        IsConfigured = o.Enabled
            && !string.IsNullOrWhiteSpace(o.TenantId)
            && !string.IsNullOrWhiteSpace(o.ClientId)
            && !string.IsNullOrWhiteSpace(o.ClientSecret)
            && !string.IsNullOrWhiteSpace(o.ChatId);
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (!IsConfigured)
        {
            TestResult = "❌ Teams Graph yapılandırılmamış. appsettings.Development.json dosyasını kontrol et.";
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
            TestResult = "❌ Teams Graph yapılandırılmamış. appsettings.Development.json dosyasını kontrol et.";
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
