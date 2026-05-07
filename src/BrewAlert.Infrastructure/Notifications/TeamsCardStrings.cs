namespace BrewAlert.Infrastructure.Notifications;

using System.Collections.Generic;

/// <summary>
/// TR/EN string table for the Teams Adaptive Cards posted by
/// <see cref="TeamsMessageBuilder"/> and <see cref="TeamsGraphMessageBuilder"/>.
///
/// Lives in Infrastructure because the UI's <c>ILocalizationService</c> sits one
/// layer up and the dependency direction is UI → Infrastructure → Core.
/// Unknown languages fall back to English.
/// </summary>
internal static class TeamsCardStrings
{
    public const string LanguageEnglish = "English";
    public const string LanguageTurkish = "Turkish";

    public static string Get(string language, string key)
    {
        var table = language == LanguageTurkish ? Turkish : English;
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly Dictionary<string, string> English = new()
    {
        ["HeaderReady"]              = "{0} BrewAlert — Your {1} is Ready!",
        ["FactProfile"]              = "Profile",
        ["FactBrewTime"]             = "Brew Time",
        ["FactCompletedAt"]          = "Completed At",
        ["FooterDontLetItGetCold"]   = "Don't let it get cold! ☕",
        ["TestSuccess"]              = "✅ BrewAlert — Connection Test Successful!",
        ["MinShort"]                 = "min",
        ["SecShort"]                 = "sec",
        ["UtcSuffix"]                = "(UTC)",
        ["BrewType_Tea"]             = "Tea",
        ["BrewType_Coffee"]          = "Coffee",
        ["BrewType_Custom"]          = "Custom",
    };

    private static readonly Dictionary<string, string> Turkish = new()
    {
        ["HeaderReady"]              = "{0} BrewAlert — {1} hazır!",
        ["FactProfile"]              = "Profil",
        ["FactBrewTime"]             = "Demleme Süresi",
        ["FactCompletedAt"]          = "Tamamlanma",
        ["FooterDontLetItGetCold"]   = "Soğutmayın! ☕",
        ["TestSuccess"]              = "✅ BrewAlert — Bağlantı Testi Başarılı!",
        ["MinShort"]                 = "dk",
        ["SecShort"]                 = "sn",
        ["UtcSuffix"]                = "(UTC)",
        ["BrewType_Tea"]             = "Çay",
        ["BrewType_Coffee"]          = "Kahve",
        ["BrewType_Custom"]          = "Özel",
    };
}
