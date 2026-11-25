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
}
