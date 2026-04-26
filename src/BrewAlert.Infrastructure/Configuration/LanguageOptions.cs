namespace BrewAlert.Infrastructure.Configuration;

/// <summary>
/// Language preference. Bound from the "BrewAlert" configuration section.
/// Value: "English" (default) or "Turkish".
/// </summary>
public sealed class LanguageOptions
{
    public const string SectionPath = "BrewAlert";
    public string Language { get; set; } = "English";
}
