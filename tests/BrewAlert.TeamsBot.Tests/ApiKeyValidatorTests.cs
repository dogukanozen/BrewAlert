using BrewAlert.TeamsBot;
using Xunit;

namespace BrewAlert.TeamsBot.Tests;

public sealed class ApiKeyValidatorTests
{
    [Fact]
    public void IsValid_WhenValuesMatch_ReturnsTrue()
    {
        Assert.True(ApiKeyValidator.IsValid("brew-secret", "brew-secret"));
    }

    [Theory]
    [InlineData("", "brew-secret")]
    [InlineData("brew-secret", "")]
    [InlineData("brew-secret", null)]
    [InlineData("brew-secret", "different")]
    public void IsValid_WhenValuesCannotAuthenticate_ReturnsFalse(string expected, string? provided)
    {
        Assert.False(ApiKeyValidator.IsValid(expected, provided));
    }
}
