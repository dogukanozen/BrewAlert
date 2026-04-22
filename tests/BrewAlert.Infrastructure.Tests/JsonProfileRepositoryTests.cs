using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly JsonProfileRepository _sut;

    public JsonProfileRepositoryTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"brewalert_test_{Guid.NewGuid()}.json");
        _sut = new JsonProfileRepository(NullLogger<JsonProfileRepository>.Instance, _testFilePath);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ShouldReturnEmptyList()
    {
        var result = await _sut.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Save_ShouldPersistProfile()
    {
        var profile = new BrewProfile
        {
            Name = "Test Tea",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(5)
        };

        await _sut.SaveAsync(profile);
        var result = await _sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Test Tea", result[0].Name);
    }

    [Fact]
    public async Task Save_ShouldUpdateExistingProfile()
    {
        var profile = new BrewProfile
        {
            Name = "Original",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(5)
        };

        await _sut.SaveAsync(profile);
        profile.Name = "Updated";
        await _sut.SaveAsync(profile);

        var result = await _sut.GetAllAsync();
        Assert.Single(result);
        Assert.Equal("Updated", result[0].Name);
    }

    [Fact]
    public async Task GetById_ShouldReturnCorrectProfile()
    {
        var profile = new BrewProfile
        {
            Name = "Find Me",
            Type = BrewType.Coffee,
            BrewDuration = TimeSpan.FromMinutes(4)
        };

        await _sut.SaveAsync(profile);
        var found = await _sut.GetByIdAsync(profile.Id);

        Assert.NotNull(found);
        Assert.Equal("Find Me", found.Name);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNull()
    {
        var found = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task Delete_ShouldRemoveProfile()
    {
        var profile = new BrewProfile
        {
            Name = "Delete Me",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(3)
        };

        await _sut.SaveAsync(profile);
        await _sut.DeleteAsync(profile.Id);

        var result = await _sut.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task MultipleProfiles_ShouldAllPersist()
    {
        for (int i = 0; i < 5; i++)
        {
            await _sut.SaveAsync(new BrewProfile
            {
                Name = $"Profile {i}",
                Type = BrewType.Tea,
                BrewDuration = TimeSpan.FromMinutes(i + 1)
            });
        }

        var result = await _sut.GetAllAsync();
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task Delete_WhenIdNotFound_ShouldNotThrow()
    {
        await _sut.SaveAsync(new BrewProfile
        {
            Name = "Existing",
            Type = BrewType.Tea,
            BrewDuration = TimeSpan.FromMinutes(3)
        });

        var ex = await Record.ExceptionAsync(() => _sut.DeleteAsync(Guid.NewGuid()));

        Assert.Null(ex);
        var remaining = await _sut.GetAllAsync();
        Assert.Single(remaining);
    }

    [Fact]
    public async Task GetAll_WhenFileContainsMalformedJson_ShouldThrow()
    {
        await File.WriteAllTextAsync(_testFilePath, "{ this is not valid json !!!");

        await Assert.ThrowsAnyAsync<Exception>(() => _sut.GetAllAsync());
    }

    [Fact]
    public async Task Save_ShouldPreserveAllProfileFields()
    {
        var id = Guid.NewGuid();
        var profile = new BrewProfile
        {
            Id = id,
            Name = "Full Fields",
            Type = BrewType.Coffee,
            BrewDuration = TimeSpan.FromMinutes(4),
            Icon = "☕",
            Description = "Full-bodied"
        };

        await _sut.SaveAsync(profile);
        var found = await _sut.GetByIdAsync(id);

        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
        Assert.Equal("Full Fields", found.Name);
        Assert.Equal(BrewType.Coffee, found.Type);
        Assert.Equal(TimeSpan.FromMinutes(4), found.BrewDuration);
        Assert.Equal("☕", found.Icon);
        Assert.Equal("Full-bodied", found.Description);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }
}
