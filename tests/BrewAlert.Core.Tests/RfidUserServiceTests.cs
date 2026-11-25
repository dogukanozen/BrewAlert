using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using NSubstitute;
using Xunit;

namespace BrewAlert.Core.Tests;

public class RfidUserServiceTests
{
    private readonly IRfidUserRepository _repository = Substitute.For<IRfidUserRepository>();

    [Fact]
    public void NormalizeCardId_TrimsUppercasesAndReturnsNullForBlank()
    {
        Assert.Equal("0012AB34", RfidUserService.NormalizeCardId("  0012ab34 \r\n"));
        Assert.Null(RfidUserService.NormalizeCardId("   "));
        Assert.Null(RfidUserService.NormalizeCardId(null));
    }

    [Fact]
    public async Task RegisterAsync_NewCard_SavesNormalizedUserAndTimestamp()
    {
        var fixedNow = new DateTimeOffset(2026, 11, 25, 10, 30, 0, TimeSpan.Zero);
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedNow);
        var sut = new RfidUserService(_repository, fakeTime);

        _repository.GetByCardIdAsync("0012AB", Arg.Any<CancellationToken>()).Returns((RfidUser?)null);

        var saved = await sut.RegisterAsync(" 0012ab ", " Ahmet Yılmaz ");

        Assert.Equal("0012AB", saved.CardId);
        Assert.Equal("Ahmet Yılmaz", saved.Name);
        Assert.Equal(fixedNow.UtcDateTime, saved.CreatedAtUtc);
        await _repository.Received(1).SaveAsync(saved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveUserAsync_WithoutCard_SavesCardlessOperator()
    {
        var sut = new RfidUserService(_repository);

        var saved = await sut.SaveUserAsync("  Mehmet  ", cardId: null);

        Assert.Null(saved.CardId);
        Assert.Equal("Mehmet", saved.Name);
        await _repository.Received(1).SaveAsync(saved, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByCardIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveUserAsync_WithExistingUserId_AttachesAndRemovesCard()
    {
        var sut = new RfidUserService(_repository);
        var existing = new RfidUser { Id = Guid.NewGuid(), CardId = null, Name = "Elif" };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([existing]);

        var updated = await sut.SaveUserAsync("Elif Kaya", "abcd1234", existing.Id);
        Assert.Same(existing, updated);
        Assert.Equal("Elif Kaya", updated.Name);
        Assert.Equal("ABCD1234", updated.CardId);

        var detached = await sut.SaveUserAsync("Elif Kaya", null, existing.Id);
        Assert.Null(detached.CardId);
    }

    [Fact]
    public async Task RegisterAsync_ExistingCard_UpdatesNameInsteadOfCreatingNew()
    {
        var sut = new RfidUserService(_repository);
        var existing = new RfidUser
        {
            Id = Guid.NewGuid(),
            CardId = "CARD01",
            Name = "Eski İsim",
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _repository.GetByCardIdAsync("CARD01", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await sut.RegisterAsync("card01", "Yeni İsim");

        Assert.Same(existing, result);
        Assert.Equal("Yeni İsim", result.Name);
        await _repository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("1234", "")]
    [InlineData("1234", "   ")]
    [InlineData(null, "")]
    public async Task RegisterAsync_BlankName_ThrowsArgumentException(string? cardId, string name)
    {
        var sut = new RfidUserService(_repository);
        await Assert.ThrowsAsync<ArgumentException>(() => sut.RegisterAsync(cardId, name));
    }

    [Fact]
    public async Task FindByCardIdAsync_Blank_ReturnsNullWithoutCallingRepo()
    {
        var sut = new RfidUserService(_repository);
        Assert.Null(await sut.FindByCardIdAsync("   "));
        await _repository.DidNotReceive().GetByCardIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByName()
    {
        var sut = new RfidUserService(_repository);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new RfidUser { CardId = "2", Name = "Zeynep" },
            new RfidUser { CardId = null, Name = "Ali" },
        ]);

        var users = await sut.GetAllAsync();

        Assert.Equal(["Ali", "Zeynep"], users.Select(u => u.Name));
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var sut = new RfidUserService(_repository);
        var id = Guid.NewGuid();
        await sut.DeleteAsync(id);
        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ResolveDuration_ReturnsOverrideWhenValid_AndFallsBackToDefaultOtherwise()
    {
        var tea = new BrewProfile
        {
            Id = Guid.NewGuid(),
            Name = "Çay",
            BrewDuration = TimeSpan.FromMinutes(15),
        };
        var user = new RfidUser
        {
            Name = "Ahmet",
            ProfileDurationMinutes = new Dictionary<Guid, int>
            {
                [tea.Id] = 20,
            },
        };

        Assert.Equal(TimeSpan.FromMinutes(20), RfidUserService.ResolveDuration(user, tea));
        Assert.True(RfidUserService.HasCustomDuration(user, tea));

        user.ProfileDurationMinutes[tea.Id] = 0;
        Assert.Equal(TimeSpan.FromMinutes(15), RfidUserService.ResolveDuration(user, tea));
        Assert.False(RfidUserService.HasCustomDuration(user, tea));

        user.ProfileDurationMinutes[tea.Id] = 999;
        Assert.Equal(TimeSpan.FromMinutes(15), RfidUserService.ResolveDuration(user, tea));
        Assert.False(RfidUserService.HasCustomDuration(user, tea));

        user.ProfileDurationMinutes[tea.Id] = 15;
        Assert.Equal(TimeSpan.FromMinutes(15), RfidUserService.ResolveDuration(user, tea));
        Assert.False(RfidUserService.HasCustomDuration(user, tea));

        Assert.Equal(TimeSpan.FromMinutes(15), RfidUserService.ResolveDuration(null, tea));
        Assert.False(RfidUserService.HasCustomDuration(null, tea));
    }

    [Fact]
    public void CreatePersonalizedProfile_DoesNotMutateOriginalProfile()
    {
        var coffee = new BrewProfile
        {
            Id = Guid.NewGuid(),
            Name = "Filtre Kahve",
            Type = BrewType.Coffee,
            BrewDuration = TimeSpan.FromMinutes(4),
            Icon = "☕",
            Description = "Filtre kahve",
        };
        var user = new RfidUser
        {
            Name = "Deniz",
            HasConfiguredProfiles = true,
            ProfileDurationMinutes = new Dictionary<Guid, int> { [coffee.Id] = 10 },
        };

        var personalized = RfidUserService.CreatePersonalizedProfile(coffee, user);

        Assert.NotSame(coffee, personalized);
        Assert.Equal(coffee.Id, personalized.Id);
        Assert.Equal(coffee.Name, personalized.Name);
        Assert.Equal(coffee.Type, personalized.Type);
        Assert.Equal(coffee.Icon, personalized.Icon);
        Assert.Equal(coffee.Description, personalized.Description);
        Assert.Equal(TimeSpan.FromMinutes(10), personalized.BrewDuration);
        Assert.Equal(TimeSpan.FromMinutes(4), coffee.BrewDuration);
    }

    [Fact]
    public async Task SaveProfilePreferencesAndAcceptDefaults_PersistAndValidate()
    {
        var sut = new RfidUserService(_repository);
        var teaId = Guid.NewGuid();
        var user = new RfidUser { Id = Guid.NewGuid(), Name = "Elif", CardId = "CARD99" };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([user]);

        var updated = await sut.SaveProfilePreferencesAsync(user.Id, new Dictionary<Guid, int> { [teaId] = 20 });

        Assert.True(updated.HasConfiguredProfiles);
        Assert.Equal(20, updated.ProfileDurationMinutes[teaId]);
        await _repository.Received(1).SaveAsync(user, Arg.Any<CancellationToken>());

        var acceptedDefaults = await sut.AcceptDefaultProfilesAsync(user.Id);
        Assert.True(acceptedDefaults.HasConfiguredProfiles);
        Assert.Empty(acceptedDefaults.ProfileDurationMinutes);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.SaveProfilePreferencesAsync(user.Id, new Dictionary<Guid, int> { [teaId] = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.SaveProfilePreferencesAsync(user.Id, new Dictionary<Guid, int> { [teaId] = 121 }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SaveProfilePreferencesAsync(Guid.NewGuid(), new Dictionary<Guid, int>()));
    }

    [Fact]
    public async Task SaveUserAsync_UpdatingExistingOperator_PreservesProfilePreferences()
    {
        var sut = new RfidUserService(_repository);
        var teaId = Guid.NewGuid();
        var existing = new RfidUser
        {
            Id = Guid.NewGuid(),
            Name = "Selin",
            CardId = "CARD01",
            HasConfiguredProfiles = true,
            ProfileDurationMinutes = new Dictionary<Guid, int> { [teaId] = 22 },
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([existing]);

        var updated = await sut.SaveUserAsync("Selin Demir", "CARD02", existing.Id);

        Assert.True(updated.HasConfiguredProfiles);
        Assert.Equal(22, updated.ProfileDurationMinutes[teaId]);
    }
}
