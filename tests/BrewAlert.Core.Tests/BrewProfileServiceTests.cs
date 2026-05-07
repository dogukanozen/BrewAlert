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
    public async Task GetAllProfiles_WhenAllDefaultsPresent_ShouldNotSeed()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>(BrewProfileService.DefaultProfiles));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(BrewProfileService.DefaultProfiles.Count, result.Count);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_WhenOnlyOneDefaultPresent_ShouldSeedMissingDefaults()
    {
        // Simulates an existing install that only has "Çay" from an older version
        var onlyTea = new List<BrewProfile>
        {
            new() { Name = "Çay", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(15) }
        };
        var afterSeed = BrewProfileService.DefaultProfiles.ToList();

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<BrewProfile>>(onlyTea),
                Task.FromResult<IReadOnlyList<BrewProfile>>(afterSeed));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(BrewProfileService.DefaultProfiles.Count, result.Count);
        // Only the missing defaults (all except Çay) should be saved
        var expectedSaveCount = BrewProfileService.DefaultProfiles.Count - 1;
        await _repository.Received(expectedSaveCount).SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
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
