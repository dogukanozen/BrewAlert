using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewFreshnessEvaluatorTests
{
    [Theory]
    [InlineData(BrewType.Tea, 30, BrewFreshnessLevel.Fresh)]
    [InlineData(BrewType.Tea, 60, BrewFreshnessLevel.Cooling)]
    [InlineData(BrewType.Tea, 130, BrewFreshnessLevel.Stale)]
    [InlineData(BrewType.Coffee, 59, BrewFreshnessLevel.Fresh)]
    [InlineData(BrewType.Coffee, 60, BrewFreshnessLevel.Cooling)]
    [InlineData(BrewType.Coffee, 119, BrewFreshnessLevel.Cooling)]
    [InlineData(BrewType.Coffee, 120, BrewFreshnessLevel.Stale)]
    public void Evaluate_UsesTypeSpecificThresholds(BrewType type, int ageMinutes, BrewFreshnessLevel expected)
    {
        var level = BrewFreshnessEvaluator.Evaluate(type, TimeSpan.FromMinutes(ageMinutes));
        Assert.Equal(expected, level);
    }
}
