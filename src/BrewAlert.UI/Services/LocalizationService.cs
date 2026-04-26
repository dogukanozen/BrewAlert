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
        ["WebhookHint"]             = "Set BrewAlert:Notifications:Teams:WebhookUrl in appsettings.Development.json.",
        ["GraphHint"]               = "Set BrewAlert:Notifications:TeamsGraph:* in appsettings.Development.json.",
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
        ["WebhookHint"]             = "BrewAlert:Notifications:Teams:WebhookUrl alanını appsettings.Development.json'da ayarlayın.",
        ["GraphHint"]               = "BrewAlert:Notifications:TeamsGraph:* alanlarını appsettings.Development.json'da ayarlayın.",
    };
}
