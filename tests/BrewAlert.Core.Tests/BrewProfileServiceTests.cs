using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using NSubstitute;
using Xunit;

namespace BrewAlert.Core.Tests;

public class BrewProfileServiceTests
{
    private static readonly Guid TeaId    = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CoffeeId = new("00000000-0000-0000-0000-000000000002");

    private readonly IProfileRepository _repository;
    private readonly BrewProfileService _sut;

    public BrewProfileServiceTests()
    {
        _repository = Substitute.For<IProfileRepository>();
        _sut = new BrewProfileService(_repository);
    }

    [Fact]
    public async Task GetAllProfiles_WhenEmpty_SeedsAllDefaults()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>([]));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(BrewProfileService.DefaultProfiles.Count, result.Count);
        await _repository.Received(BrewProfileService.DefaultProfiles.Count)
            .SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_WhenAllDefaultsPresent_DoesNotSeed()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>(BrewProfileService.DefaultProfiles));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(BrewProfileService.DefaultProfiles.Count, result.Count);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_WhenDefaultRenamed_DoesNotAddDuplicate()
    {
        // Simulates user renaming "Çay" to "Demlik" — stable ID still present.
        var renamed = new BrewProfile { Id = TeaId, Name = "Demlik", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(15) };
        var coffee  = BrewProfileService.DefaultProfiles.First(p => p.Id == CoffeeId);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>([renamed, coffee]));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(2, result.Count);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<BrewProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_WhenLegacyInstall_MigratesExistingProfileToStableId()
    {
        // Simulates upgrading from a version where defaults had random GUIDs.
        var originalCreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var legacyTea = new BrewProfile { Id = Guid.NewGuid(), Name = "Çay", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(15), CreatedAtUtc = originalCreatedAt };
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>([legacyTea]));

        var result = await _sut.GetAllProfilesAsync();

        // SaveAsync must precede DeleteAsync to avoid a data-loss window on crash.
        Received.InOrder(() =>
        {
            _repository.SaveAsync(Arg.Is<BrewProfile>(p => p.Id == TeaId), Arg.Any<CancellationToken>());
            _repository.DeleteAsync(legacyTea.Id, Arg.Any<CancellationToken>());
        });
        Assert.Equal(2, result.Count);
        var migratedTea = result.First(p => p.Id == TeaId);
        Assert.Equal(originalCreatedAt, migratedTea.CreatedAtUtc);
    }

    [Fact]
    public async Task GetAllProfiles_WhenOneMissing_SeedsOnlyMissing()
    {
        var tea = BrewProfileService.DefaultProfiles.First(p => p.Id == TeaId);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>([tea]));

        var result = await _sut.GetAllProfilesAsync();

        Assert.Equal(2, result.Count);
        await _repository.Received(1).SaveAsync(
            Arg.Is<BrewProfile>(p => p.Id == CoffeeId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllProfiles_DoesNotCallGetAllAsyncTwice()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BrewProfile>>([]));

        await _sut.GetAllProfilesAsync();

        // Only the initial read; result is built in-memory, no second disk round-trip.
        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveProfile_DelegatesToRepository()
    {
        var profile = new BrewProfile { Name = "Test", Type = BrewType.Tea, BrewDuration = TimeSpan.FromMinutes(3) };
        await _sut.SaveProfileAsync(profile);
        await _repository.Received(1).SaveAsync(profile, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProfile_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        await _sut.DeleteProfileAsync(id);
        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DefaultProfiles_HaveStableIds()
    {
        Assert.Equal(TeaId,    BrewProfileService.DefaultProfiles.First(p => p.Type == BrewType.Tea).Id);
        Assert.Equal(CoffeeId, BrewProfileService.DefaultProfiles.First(p => p.Type == BrewType.Coffee).Id);
    }

    [Fact]
    public void DefaultProfiles_AllHavePositiveDuration()
    {
        Assert.True(BrewProfileService.DefaultProfiles.All(p => p.BrewDuration > TimeSpan.Zero));
    }
}
