namespace BrewAlert.UI.Services;

public enum BrewTheme { Obsidian, Porcelain, Sumi, Ibre, Aizome }

/// <summary>Stored names stay independent of the current UI language.</summary>
public sealed class AppearanceOptions
{
    public const string SectionPath = "BrewAlert:Appearance";
    public const string Weekly = "Weekly";
    public string Mode { get; set; } = Weekly;
    public string Monday { get; set; } = nameof(BrewTheme.Obsidian);
    public string Tuesday { get; set; } = nameof(BrewTheme.Porcelain);
    public string Wednesday { get; set; } = nameof(BrewTheme.Sumi);
    public string Thursday { get; set; } = nameof(BrewTheme.Ibre);
    public string Friday { get; set; } = nameof(BrewTheme.Aizome);
    public string Saturday { get; set; } = nameof(BrewTheme.Porcelain);
    public string Sunday { get; set; } = nameof(BrewTheme.Obsidian);

    public BrewTheme Resolve(DayOfWeek day) =>
        string.Equals(Mode, Weekly, StringComparison.OrdinalIgnoreCase) ? ForDay(day) : Parse(Mode);

    public BrewTheme ForDay(DayOfWeek day) => Parse(day switch
    {
        DayOfWeek.Monday => Monday, DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday, DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday, DayOfWeek.Saturday => Saturday, _ => Sunday,
    });

    public void SetDay(DayOfWeek day, BrewTheme theme)
    {
        var name = theme.ToString();
        switch (day)
        {
            case DayOfWeek.Monday: Monday = name; break;
            case DayOfWeek.Tuesday: Tuesday = name; break;
            case DayOfWeek.Wednesday: Wednesday = name; break;
            case DayOfWeek.Thursday: Thursday = name; break;
            case DayOfWeek.Friday: Friday = name; break;
            case DayOfWeek.Saturday: Saturday = name; break;
            case DayOfWeek.Sunday: Sunday = name; break;
        }
    }

    public AppearanceOptions Copy() => (AppearanceOptions)MemberwiseClone();
    public AppearanceOptions Normalized()
    {
        var result = Copy();
        result.Mode = string.Equals(Mode, Weekly, StringComparison.OrdinalIgnoreCase)
            ? Weekly : Parse(Mode).ToString();
        foreach (var day in Enum.GetValues<DayOfWeek>()) result.SetDay(day, ForDay(day));
        return result;
    }
    public bool EquivalentTo(AppearanceOptions other) => Mode == other.Mode
        && Enum.GetValues<DayOfWeek>().All(day => ForDay(day) == other.ForDay(day));
    private static BrewTheme Parse(string? value) => Enum.GetValues<BrewTheme>()
        .FirstOrDefault(theme => string.Equals(value, theme.ToString(), StringComparison.OrdinalIgnoreCase));
}
