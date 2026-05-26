namespace BrewAlert.Core.Models;

/// <summary>How drinkable a completed brew still is, based on type and age.</summary>
public enum BrewFreshnessLevel
{
    Fresh,
    Cooling,
    Stale
}
