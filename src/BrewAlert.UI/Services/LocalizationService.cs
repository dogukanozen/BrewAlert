namespace BrewAlert.UI.Services;

using Avalonia.Threading;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// Singleton localization service with TR/EN string tables.
/// Language changes are propagated via the LanguageChanged event so ViewModels refresh live.
/// </summary>
public sealed class LocalizationService : ILocalizationService, IDisposable
{
    private string _currentLanguage;
    private readonly IDisposable? _changeListener;

    public event Action<string>? LanguageChanged;
    public string CurrentLanguage => _currentLanguage;

    public LocalizationService(IOptionsMonitor<LanguageOptions> options)
    {
        _currentLanguage = options.CurrentValue.Language;
        // IOptionsMonitor.OnChange fires on a background thread; marshal to the UI
        // thread so LanguageChanged handlers can update observable properties safely.
        _changeListener = options.OnChange(opt =>
            Dispatcher.UIThread.Post(() => SetLanguage(opt.Language)));
    }

    public void SetLanguage(string language)
    {
        if (_currentLanguage == language) return;
        _currentLanguage = language;
        LanguageChanged?.Invoke(language);
    }

    public string Get(string key)
    {
        var table = _currentLanguage == AppLanguage.Turkish ? Turkish : English;
        return table.TryGetValue(key, out var value) ? value : key;
    }

    public void Dispose() => _changeListener?.Dispose();

    private static readonly Dictionary<string, string> English = new()
    {
        ["Brewing"]                 = "Brewing...",
        ["Paused"]                  = "Paused",
        ["Cancelled"]               = "Cancelled",
        ["Ready"]                   = "Ready! ☕",
        ["SendingNotification"]     = "Sending notification...",
        ["NotificationSent"]        = "✅ Notification sent!",
        ["PauseButton"]             = "⏸ Pause",
        ["ResumeButton"]            = "▶ Resume",
        ["CancelButton"]            = "✕ Cancel",
        ["BackButton"]              = "← Back to Brews",
        ["BrewsNavButton"]          = "☕ Brews",
        ["SelectYourBrew"]          = "Select Your Brew",
        ["Loading"]                 = "Loading...",
        ["SettingsTitle"]           = "Settings",
        ["DurationSettings"]        = "Duration Settings",
        ["ResetToDefaults"]         = "Reset to Defaults",
        ["NotificationSettings"]    = "Notification Settings",
        ["NotificationChannel"]     = "Notification channel",
        ["ActiveChannel"]           = "Active channel:",
        ["WebhookConfigured"]       = "✅ Active — Teams Webhook",
        ["GraphConfigured"]         = "✅ Active — Teams Graph API",
        ["NotConfigured"]           = "⚠️ Not configured",
        ["TestTitle"]               = "Test",
        ["TestConnection"]          = "Test Connection",
        ["SendTestNotification"]    = "Send Test Notification",
        ["Connecting"]              = "Connecting...",
        ["Sending"]                 = "Sending...",
        ["ConnectionSuccess"]       = "✅ Connection successful! Test card sent.",
        ["ConnectionFailed"]        = "❌ Connection failed.",
        ["TestNotificationSent"]    = "✅ Test notification sent!",
        ["SaveFailed"]              = "❌ Could not save preference: {0}",
        ["ErrorPrefix"]             = "❌ Error: {0}",
        ["CouldNotSend"]            = "❌ Could not send: {0}",
        ["NotConfiguredError"]      = "❌ {0} not configured. Check appsettings.Development.json.",
        ["Language"]                = "Language",
        ["AddProfile"]              = "+ Add Profile",
        ["DeleteProfile"]           = "Delete",
        ["ProfileNameLabel"]        = "Name",
        ["TypeLabel"]               = "Type",
        ["DurationLabel"]           = "Duration (min)",
        ["AddButton"]               = "Add",
        ["CancelButton_Short"]      = "Cancel",
        ["WebhookHint"]             = "Enter the Power Automate webhook URL below and tap Save.",
        ["WebhookUrlLabel"]         = "Webhook URL",
        ["SaveWebhookUrl"]          = "Save",
        ["WebhookUrlSaved"]         = "✅ Webhook URL saved.",
        ["GraphHint"]               = "Set BrewAlert:Notifications:TeamsGraph:* in appsettings.Development.json.",
        ["UpdateTitle"]             = "Updates",
        ["CheckUpdates"]            = "Check for Updates",
        ["InstallUpdate"]           = "Install Now",
        ["UpdateAvailable"]         = "Update downloaded and ready to install!",
        ["UpToDate"]                = "BrewAlert is up to date.",
        ["CheckingUpdates"]         = "Checking for updates...",
        ["UpdateError"]             = "Failed to check for updates.",
        ["InstallingUpdate"]        = "Installing update...",
        ["CurrentVersion"]          = "Current version: {0}",
        ["MinShort"]                = "min",
        ["TestBrewName"]            = "Test Brew",
        ["UpdateDismiss"]           = "Dismiss",
        ["RecentBrews"]             = "Recent Brews",
        ["JustNow"]                 = "just now",
        ["MinutesAgo"]              = "{0}m ago",
        ["HoursAgo"]                = "{0}h ago",
        ["DaysAgo"]                 = "{0}d ago",
    };

    private static readonly Dictionary<string, string> Turkish = new()
    {
        ["Brewing"]                 = "Demliyor...",
        ["Paused"]                  = "Duraklatıldı",
        ["Cancelled"]               = "İptal edildi",
        ["Ready"]                   = "Hazır! ☕",
        ["SendingNotification"]     = "Bildirim gönderiliyor...",
        ["NotificationSent"]        = "✅ Bildirim gönderildi!",
        ["PauseButton"]             = "⏸ Duraklat",
        ["ResumeButton"]            = "▶ Devam Et",
        ["CancelButton"]            = "✕ İptal",
        ["BackButton"]              = "← Demlere Dön",
        ["BrewsNavButton"]          = "☕ Demler",
        ["SelectYourBrew"]          = "Deminizi Seçin",
        ["Loading"]                 = "Yükleniyor...",
        ["SettingsTitle"]           = "Ayarlar",
        ["DurationSettings"]        = "Süre Ayarları",
        ["ResetToDefaults"]         = "Varsayılanlara Sıfırla",
        ["NotificationSettings"]    = "Bildirim Ayarları",
        ["NotificationChannel"]     = "Bildirim Kanalı",
        ["ActiveChannel"]           = "Aktif kanal:",
        ["WebhookConfigured"]       = "✅ Aktif — Teams Webhook",
        ["GraphConfigured"]         = "✅ Aktif — Teams Graph API",
        ["NotConfigured"]           = "⚠️ Yapılandırılmamış",
        ["TestTitle"]               = "Test",
        ["TestConnection"]          = "Bağlantıyı Test Et",
        ["SendTestNotification"]    = "Test Bildirimi Gönder",
        ["Connecting"]              = "Bağlanıyor...",
        ["Sending"]                 = "Gönderiliyor...",
        ["ConnectionSuccess"]       = "✅ Bağlantı başarılı! Test kartı gönderildi.",
        ["ConnectionFailed"]        = "❌ Bağlantı başarısız.",
        ["TestNotificationSent"]    = "✅ Test bildirimi gönderildi!",
        ["SaveFailed"]              = "❌ Tercih kaydedilemedi: {0}",
        ["ErrorPrefix"]             = "❌ Hata: {0}",
        ["CouldNotSend"]            = "❌ Gönderilemedi: {0}",
        ["NotConfiguredError"]      = "❌ {0} yapılandırılmamış. appsettings.Development.json dosyasını kontrol et.",
        ["Language"]                = "Dil",
        ["AddProfile"]              = "+ Profil Ekle",
        ["DeleteProfile"]           = "Sil",
        ["ProfileNameLabel"]        = "Ad",
        ["TypeLabel"]               = "Tür",
        ["DurationLabel"]           = "Süre (dk)",
        ["AddButton"]               = "Ekle",
        ["CancelButton_Short"]      = "İptal",
        ["WebhookHint"]             = "Aşağıya Power Automate webhook URL'sini girin ve Kaydet'e basın.",
        ["WebhookUrlLabel"]         = "Webhook URL",
        ["SaveWebhookUrl"]          = "Kaydet",
        ["WebhookUrlSaved"]         = "✅ Webhook URL kaydedildi.",
        ["GraphHint"]               = "BrewAlert:Notifications:TeamsGraph:* alanlarını appsettings.Development.json'da ayarlayın.",
        ["UpdateTitle"]             = "Güncellemeler",
        ["CheckUpdates"]            = "Güncellemeleri Kontrol Et",
        ["InstallUpdate"]           = "Şimdi Kur",
        ["UpdateAvailable"]         = "Güncelleme indirildi, kurulmaya hazır!",
        ["UpToDate"]                = "BrewAlert güncel.",
        ["CheckingUpdates"]         = "Güncellemeler kontrol ediliyor...",
        ["UpdateError"]             = "Güncelleme kontrolü başarısız oldu.",
        ["InstallingUpdate"]        = "Güncelleme kuruluyor...",
        ["CurrentVersion"]          = "Mevcut sürüm: {0}",
        ["MinShort"]                = "dk",
        ["TestBrewName"]            = "Test Demi",
        ["UpdateDismiss"]           = "Kapat",
        ["RecentBrews"]             = "Son Demlemeler",
        ["JustNow"]                 = "az önce",
        ["MinutesAgo"]              = "{0} dk önce",
        ["HoursAgo"]                = "{0} sa önce",
        ["DaysAgo"]                 = "{0} g önce",
    };
}
