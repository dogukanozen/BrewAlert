using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using NSubstitute;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewProfileServiceTests
{
    private readonly IProfileRepository _repository;
    private readonly BrewProfileService _sut;

    public BrewProfileServiceTests()
    {
        _repository = Substitute.For<IProfileRepository>();
        _sut = new BrewProfileService(_repository);
    }

    [Fact]
    public async Task GetAllProfiles_WhenEmpty_ShouldSeedDefaults()
    {
        // First call returns empty, second call returns seeded profiles
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<BrewProfile>>(Array.Empty<BrewProfile>()),
                Task.FromResult<IReadOnlyList<BrewProfile>>(BrewProfileService.DefaultProfiles));

        var result = await _sut.GetAllProfilesAsync();

        Assert.NotEmpty(result);
        await _repository.Received(2).SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_WhenPopulated_ShouldNotSeed()
    {
        var existing = new List<BrewProfile>
        {
            new() { Name = "Custom Brew", Type = BrewType.Custom, BrewDuration = TimeSpan.FromMinutes(5) }
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(existing.AsReadOnly());

        var result = await _sut.GetAllProfilesAsync();

        Assert.Single(result);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveProfile_ShouldDelegateToRepository()
    {
        var profile = new BrewProfile
        {
            Name = "Test",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(3)
        };

        await _sut.SaveProfileAsync(profile);

        await _repository.Received(1).SaveAsync(profile, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProfile_ShouldDelegateToRepository()
    {
        var id = Guid.NewGuid();
        await _sut.DeleteProfileAsync(id);

        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DefaultProfiles_ShouldContainTeaAndCoffee()
    {
        var defaults = BrewProfileService.DefaultProfiles;

        Assert.Contains(defaults, p => p.Type == BrewType.Tea);
        Assert.Contains(defaults, p => p.Type == BrewType.Coffee);
        Assert.True(defaults.All(p => p.BrewDuration > TimeSpan.Zero));
    }
}
