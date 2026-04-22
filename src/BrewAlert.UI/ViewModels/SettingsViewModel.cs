using BrewAlert.Core.Interfaces;
using BrewAlert.Infrastructure.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// ViewModel for the settings screen — webhook URL configuration and connection test.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly INotificationService _notificationService;
    private readonly TeamsNotificationOptions _options;

    [ObservableProperty] private string _webhookUrl = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private bool _isTesting;

    public SettingsViewModel(
        INotificationService notificationService,
        IOptions<TeamsNotificationOptions> options)
    {
        _notificationService = notificationService;
        _options = options.Value;

        WebhookUrl = _options.WebhookUrl;
        IsEnabled = _options.Enabled;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        IsTesting = true;
        TestResult = "Testing...";
        try
        {
            var success = await _notificationService.TestConnectionAsync();
            TestResult = success ? "✅ Connection successful!" : "❌ Connection failed.";
        }
        catch (Exception ex)
        {
            TestResult = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
