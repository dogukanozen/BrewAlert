using BrewAlert.Core.Models;

namespace BrewAlert.Core.Services;

/// <summary>
/// Pure rules for how long tea, coffee, and custom brews stay pleasant.
/// Used by the ambient idle screen — no UI dependencies.
/// </summary>
public static class BrewFreshnessEvaluator
{
    public static BrewFreshnessLevel Evaluate(BrewType type, TimeSpan age)
    {
        if (age >= GetStaleThreshold(type)) return BrewFreshnessLevel.Stale;
        if (age >= GetWarningThreshold(type)) return BrewFreshnessLevel.Cooling;
        return BrewFreshnessLevel.Fresh;
    }

    public static TimeSpan GetWarningThreshold(BrewType type) => type switch
    {
        BrewType.Tea => TimeSpan.FromMinutes(45),
        BrewType.Coffee => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromHours(1)
    };

    public static TimeSpan GetStaleThreshold(BrewType type) => type switch
    {
        BrewType.Tea => TimeSpan.FromHours(2),
        BrewType.Coffee => TimeSpan.FromMinutes(45),
        _ => TimeSpan.FromHours(3)
    };
}
