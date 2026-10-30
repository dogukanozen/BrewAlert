namespace BrewAlert.UI.Services;

using System.Globalization;
using Avalonia.Threading;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// Singleton localization service with TR/EN string tables.
/// Language changes are propagated via the LanguageChanged event so ViewModels refresh live.
/// </summary>
public sealed class LocalizationService : ILocalizationService, IDisposable
{
    // Cached so we don't allocate a CultureInfo on every formatting call. The Turkish
    // weekday/month forms come from the framework's CLDR data — no manual mapping needed.
    private static readonly CultureInfo TurkishCulture = new("tr-TR");
    private static readonly CultureInfo EnglishCulture = new("en-US");

    private string _currentLanguage;
    private readonly IDisposable? _changeListener;

    public event Action<string>? LanguageChanged;
    public string CurrentLanguage => _currentLanguage;
    public CultureInfo CurrentCulture =>
        _currentLanguage == AppLanguage.Turkish ? TurkishCulture : EnglishCulture;

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
        ["HomeTitle"] = "Let's take a break.",
        ["HomeEyebrow"] = "A SMALL RITUAL",
        ["HomeSubtitle"] = "You brew. We will let you know when it is ready.",
        ["HomeNotificationHint"] = "A little time. A good brew.",
        ["IdleScreen"] = "Idle screen",
        ["ActiveNavCount"] = "Active · {0}",
        ["AppearanceTitle"] = "Appearance",
        ["AppearanceHint"] = "Manual choices apply immediately. The weekly schedule follows this device's local day and waits until you return home with no active brews.",
        ["WeeklyTheme"] = "Weekly schedule",
        ["ThemeObsidian"] = "Obsidian",
        ["ThemePorcelain"] = "Porcelain",
        ["ThemeAizome"] = "Aizome",
        ["ThemeIbre"] = "Ibre",
        ["ThemeSumi"] = "Sumi",
        ["CurrentAppearance"] = "Now: {0}",
        ["BrewStart"] = "Brew",
        ["ShowMoreHistory"] = "Show all",
        ["ShowLessHistory"] = "Show less",
        ["Brewing"] = "Brewing...",
        ["Paused"] = "Paused",
        ["Cancelled"] = "Cancelled",
        ["Ready"] = "Ready!",
        ["SendingNotification"] = "Sending notification...",
        ["NotificationSent"] = "Notification sent!",
        ["PauseButton"] = "Pause",
        ["ResumeButton"] = "Resume",
        ["CancelButton"] = "Cancel",
        ["BackButton"] = "← Back to Brews",
        ["BrewsNavButton"] = "Brews",
        ["SelectYourBrew"] = "Select Your Brew",
        ["Loading"] = "Loading...",
        ["SettingsTitle"] = "Settings",
        ["DurationSettings"] = "Duration Settings",
        ["ResetToDefaults"] = "Reset to Defaults",
        ["NotificationSettings"] = "Notification Settings",
        ["NotificationChannel"] = "Notification channel",
        ["ActiveChannel"] = "Active channel:",
        ["WebhookConfigured"] = "Active — Teams Webhook",
        ["GraphConfigured"] = "Active — Teams Graph API",
        ["NotConfigured"] = "Not configured",
        ["TestTitle"] = "Test",
        ["TestConnection"] = "Test Connection",
        ["SendTestNotification"] = "Send Test Notification",
        ["Connecting"] = "Connecting...",
        ["Sending"] = "Sending...",
        ["ConnectionSuccess"] = "Connection successful! Test card sent.",
        ["ConnectionFailed"] = "Connection failed.",
        ["TestNotificationSent"] = "Test notification sent!",
        ["SaveFailed"] = "Could not save preference: {0}",
        ["ErrorPrefix"] = "Error: {0}",
        ["CouldNotSend"] = "Could not send: {0}",
        ["NotConfiguredError"] = "{0} not configured. Check appsettings.Development.json.",
        ["Language"] = "Language",
        ["AddProfile"] = "+ Add Profile",
        ["DeleteProfile"] = "Delete",
        ["ProfileNameLabel"] = "Name",
        ["TypeLabel"] = "Type",
        ["DurationLabel"] = "Duration (min)",
        ["AddButton"] = "Add",
        ["CancelButton_Short"] = "Cancel",
        ["WebhookHint"] = "Enter the Power Automate webhook URL below and tap Save.",
        ["WebhookUrlLabel"] = "Webhook URL",
        ["SaveWebhookUrl"] = "Save",
        ["WebhookUrlSaved"] = "Webhook URL saved.",
        ["GraphHint"] = "Set BrewAlert:Notifications:TeamsGraph:* in appsettings.Development.json.",
        ["UpdateTitle"] = "Updates",
        ["CheckUpdates"] = "Check for Updates",
        ["InstallUpdate"] = "Install Now",
        ["UpdateAvailable"] = "Update downloaded and ready to install!",
        ["UpToDate"] = "BrewAlert is up to date.",
        ["CheckingUpdates"] = "Checking for updates...",
        ["UpdateError"] = "Failed to check for updates.",
        ["InstallingUpdate"] = "Installing update...",
        ["CurrentVersion"] = "Current version: {0}",
        ["UpdateWaitsForBrew"] = "Install after the running brew finishes.",
        ["NetworkTitle"] = "Network",
        ["NetworkWifi"] = "Wi-Fi",
        ["NetworkWired"] = "Wired",
        ["NetworkNotConnected"] = "Not connected",
        ["ProfilesLoadError"] = "Could not load profiles: {0}",
        ["MinShort"] = "min",
        ["TestBrewName"] = "Test Brew",
        ["UpdateDismiss"] = "Dismiss",
        ["RecentBrews"] = "Recent Brews",
        ["JustNow"] = "just now",
        ["MinutesAgo"] = "{0}m ago",
        ["HoursAgo"] = "{0}h ago",
        ["DaysAgo"] = "{0}d ago",
        ["ActiveBrewsTitle"] = "Active Brews",
        ["NewBrewButton"] = "+ New Brew",
        ["NoActiveBrews"] = "No active brews — start one from Brews.",
        ["ActiveBrewsCount"] = "{0} active brews",
        ["DismissButton"] = "Dismiss",
        ["AmbientTapToWake"] = "Tap anywhere to wake",
        ["AmbientWelcomeTitle"] = "BrewAlert",
        ["AmbientWelcomeSubtitle"] = "Happy {0} — time for a cozy brew?",
        ["AmbientNoBrewsTitle"] = "No brews yet today",
        ["AmbientNoBrewsSubtitle"] = "The kettle is waiting — pick a profile when you're ready.",
        ["AmbientTodayCount"] = "{0} brews today",
        ["AmbientTodayCountHint"] = "Here's how your cups are holding up.",
        ["AmbientStillGood"] = "{0} still tastes great",
        ["AmbientStillGoodHint"] = "Brewed {0} — enjoy it while it's hot.",
        ["AmbientTeaStale"] = "{0} — tea brewed {1}",
        ["AmbientTeaWarning"] = "{0} is getting cold ({1})",
        ["AmbientCoffeeStale"] = "{0} — coffee from {1}, now bitter and stale",
        ["AmbientCoffeeWarning"] = "{0} is cooling down ({1})",
        ["AmbientCustomStale"] = "{0} — brewed {1}",
        ["AmbientCustomWarning"] = "{0} has been sitting ({1})",
        ["AmbientStaleHint"] = "Bitter, flat, or stale — time to brew a fresh cup.",
        ["AmbientWarningHint"] = "Still drinkable, but the magic is fading.",
    };

    private static readonly Dictionary<string, string> Turkish = new()
    {
        ["HomeTitle"] = "Bir mola verelim.",
        ["HomeEyebrow"] = "KÜÇÜK BİR RİTÜEL",
        ["HomeSubtitle"] = "Sen demle. Hazır olunca biz haber veririz.",
        ["HomeNotificationHint"] = "Biraz zaman. İyi bir demleme.",
        ["IdleScreen"] = "Dinlenme ekranı",
        ["ActiveNavCount"] = "Aktif · {0}",
        ["AppearanceTitle"] = "Görünüm",
        ["AppearanceHint"] = "Elle seçim hemen uygulanır. Haftalık program cihazın yerel gününü izler; otomatik geçiş için demlemenin bitmesini ve ana ekrana dönüşü bekler.",
        ["WeeklyTheme"] = "Haftalık program",
        ["ThemeObsidian"] = "Obsidyen",
        ["ThemePorcelain"] = "Porselen",
        ["ThemeAizome"] = "Aizome",
        ["ThemeIbre"] = "İbre",
        ["ThemeSumi"] = "Sumi",
        ["CurrentAppearance"] = "Şu an: {0}",
        ["BrewStart"] = "Demle",
        ["ShowMoreHistory"] = "Tümünü göster",
        ["ShowLessHistory"] = "Daha az göster",
        ["Brewing"] = "Demliyor...",
        ["Paused"] = "Duraklatıldı",
        ["Cancelled"] = "İptal edildi",
        ["Ready"] = "Hazır!",
        ["SendingNotification"] = "Bildirim gönderiliyor...",
        ["NotificationSent"] = "Bildirim gönderildi!",
        ["PauseButton"] = "Duraklat",
        ["ResumeButton"] = "Devam Et",
        ["CancelButton"] = "İptal",
        ["BackButton"] = "← Demlere Dön",
        ["BrewsNavButton"] = "Demler",
        ["SelectYourBrew"] = "Deminizi Seçin",
        ["Loading"] = "Yükleniyor...",
        ["SettingsTitle"] = "Ayarlar",
        ["DurationSettings"] = "Süre Ayarları",
        ["ResetToDefaults"] = "Varsayılanlara Sıfırla",
        ["NotificationSettings"] = "Bildirim Ayarları",
        ["NotificationChannel"] = "Bildirim Kanalı",
        ["ActiveChannel"] = "Aktif kanal:",
        ["WebhookConfigured"] = "Aktif — Teams Webhook",
        ["GraphConfigured"] = "Aktif — Teams Graph API",
        ["NotConfigured"] = "Yapılandırılmamış",
        ["TestTitle"] = "Test",
        ["TestConnection"] = "Bağlantıyı Test Et",
        ["SendTestNotification"] = "Test Bildirimi Gönder",
        ["Connecting"] = "Bağlanıyor...",
        ["Sending"] = "Gönderiliyor...",
        ["ConnectionSuccess"] = "Bağlantı başarılı! Test kartı gönderildi.",
        ["ConnectionFailed"] = "Bağlantı başarısız.",
        ["TestNotificationSent"] = "Test bildirimi gönderildi!",
        ["SaveFailed"] = "Tercih kaydedilemedi: {0}",
        ["ErrorPrefix"] = "Hata: {0}",
        ["CouldNotSend"] = "Gönderilemedi: {0}",
        ["NotConfiguredError"] = "{0} yapılandırılmamış. appsettings.Development.json dosyasını kontrol et.",
        ["Language"] = "Dil",
        ["AddProfile"] = "+ Profil Ekle",
        ["DeleteProfile"] = "Sil",
        ["ProfileNameLabel"] = "Ad",
        ["TypeLabel"] = "Tür",
        ["DurationLabel"] = "Süre (dk)",
        ["AddButton"] = "Ekle",
        ["CancelButton_Short"] = "İptal",
        ["WebhookHint"] = "Aşağıya Power Automate webhook URL'sini girin ve Kaydet'e basın.",
        ["WebhookUrlLabel"] = "Webhook URL",
        ["SaveWebhookUrl"] = "Kaydet",
        ["WebhookUrlSaved"] = "Webhook URL kaydedildi.",
        ["GraphHint"] = "BrewAlert:Notifications:TeamsGraph:* alanlarını appsettings.Development.json'da ayarlayın.",
        ["UpdateTitle"] = "Güncellemeler",
        ["CheckUpdates"] = "Güncellemeleri Kontrol Et",
        ["InstallUpdate"] = "Şimdi Kur",
        ["UpdateAvailable"] = "Güncelleme indirildi, kurulmaya hazır!",
        ["UpToDate"] = "BrewAlert güncel.",
        ["CheckingUpdates"] = "Güncellemeler kontrol ediliyor...",
        ["UpdateError"] = "Güncelleme kontrolü başarısız oldu.",
        ["InstallingUpdate"] = "Güncelleme kuruluyor...",
        ["CurrentVersion"] = "Mevcut sürüm: {0}",
        ["UpdateWaitsForBrew"] = "Demleme bitince kurun.",
        ["NetworkTitle"] = "Ağ",
        ["NetworkWifi"] = "Wi-Fi",
        ["NetworkWired"] = "Kablolu",
        ["NetworkNotConnected"] = "Bağlı değil",
        ["ProfilesLoadError"] = "Profiller yüklenemedi: {0}",
        ["MinShort"] = "dk",
        ["TestBrewName"] = "Test Demi",
        ["UpdateDismiss"] = "Kapat",
        ["RecentBrews"] = "Son Demlemeler",
        ["JustNow"] = "az önce",
        ["MinutesAgo"] = "{0} dk önce",
        ["HoursAgo"] = "{0} sa önce",
        ["DaysAgo"] = "{0} g önce",
        ["ActiveBrewsTitle"] = "Aktif Demlemeler",
        ["NewBrewButton"] = "+ Yeni Demleme",
        ["NoActiveBrews"] = "Aktif demleme yok — Demler ekranından başlatın.",
        ["ActiveBrewsCount"] = "{0} aktif demleme",
        ["DismissButton"] = "Kapat",
        ["AmbientTapToWake"] = "Uyandırmak için dokunun",
        ["AmbientWelcomeTitle"] = "BrewAlert",
        ["AmbientWelcomeSubtitle"] = "Güzel bir {0} günü — sıcak bir demleme?",
        ["AmbientNoBrewsTitle"] = "Bugün henüz demleme yok",
        ["AmbientNoBrewsSubtitle"] = "Çaydanlık hazır — istediğiniz zaman profil seçin.",
        ["AmbientTodayCount"] = "Bugün {0} demleme",
        ["AmbientTodayCountHint"] = "Fincanlarınızın durumuna bir bakalım.",
        ["AmbientStillGood"] = "{0} hâlâ güzel",
        ["AmbientStillGoodHint"] = "{0} demlendi — sıcakken tadını çıkarın.",
        ["AmbientTeaStale"] = "{0} — çay {1} demlendi, artık acı ve bayat",
        ["AmbientTeaWarning"] = "{0} soğuyor ({1})",
        ["AmbientCoffeeStale"] = "{0} — kahve {1} demlendi, artık acı ve bayat",
        ["AmbientCoffeeWarning"] = "{0} soğumaya başladı ({1})",
        ["AmbientCustomStale"] = "{0} — {1} demlendi",
        ["AmbientCustomWarning"] = "{0} bir süredir bekliyor ({1})",
        ["AmbientStaleHint"] = "Acı, bayat veya kötü — yenisini demleyin.",
        ["AmbientWarningHint"] = "Hâlâ içilebilir ama tadı azalıyor.",
    };
}
