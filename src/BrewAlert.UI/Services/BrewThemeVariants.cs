using Avalonia.Styling;

namespace BrewAlert.UI.Services;

/// <summary>Named palettes inherit the matching Fluent and ambient light/dark resources.</summary>
public static class BrewThemeVariants
{
    public static readonly ThemeVariant Sumi = new(nameof(BrewTheme.Sumi), ThemeVariant.Light);
    public static readonly ThemeVariant Ibre = new(nameof(BrewTheme.Ibre), ThemeVariant.Dark);
    public static readonly ThemeVariant Aizome = new(nameof(BrewTheme.Aizome), ThemeVariant.Dark);

    public static ThemeVariant For(BrewTheme theme) => theme switch
    {
        BrewTheme.Porcelain => ThemeVariant.Light,
        BrewTheme.Sumi => Sumi,
        BrewTheme.Ibre => Ibre,
        BrewTheme.Aizome => Aizome,
        _ => ThemeVariant.Dark,
    };
}
