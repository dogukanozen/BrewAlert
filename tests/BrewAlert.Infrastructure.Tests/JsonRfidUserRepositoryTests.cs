using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class JsonRfidUserRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly JsonRfidUserRepository _sut;

    public JsonRfidUserRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"brewalert_rfid_{Guid.NewGuid()}");
        _path = Path.Combine(_dir, "rfid-users.json");
        _sut = new JsonRfidUserRepository(NullLogger<JsonRfidUserRepository>.Instance, _path);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsUsers()
    {
        var user = new RfidUser { CardId = "000991122", Name = "Doğukan" };
        await _sut.SaveAsync(user);

        var all = await _sut.GetAllAsync();
        var loaded = Assert.Single(all);
        Assert.Equal(user.Id, loaded.Id);
        Assert.Equal("000991122", loaded.CardId);
        Assert.Equal("Doğukan", loaded.Name);
    }

    [Fact]
    public async Task GetByCardId_IsCaseInsensitiveAndTrims()
    {
        await _sut.SaveAsync(new RfidUser { CardId = "ABCD1234", Name = "Ayşe" });

        var found = await _sut.GetByCardIdAsync("  abcd1234 ");

        Assert.NotNull(found);
        Assert.Equal("Ayşe", found.Name);
    }

    [Fact]
    public async Task Save_WithSameCardId_UpsertsWithoutDuplicate()
    {
        await _sut.SaveAsync(new RfidUser { CardId = "12345678", Name = "First" });
        await _sut.SaveAsync(new RfidUser { CardId = "12345678", Name = "Updated" });

        var all = await _sut.GetAllAsync();
        var stored = Assert.Single(all);
        Assert.Equal("Updated", stored.Name);
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        var u1 = new RfidUser { CardId = "1111", Name = "One" };
        var u2 = new RfidUser { CardId = "2222", Name = "Two" };
        await _sut.SaveAsync(u1);
        await _sut.SaveAsync(u2);

        await _sut.DeleteAsync(u1.Id);

        var all = await _sut.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Two", all[0].Name);
    }

    [Fact]
    public async Task CorruptFile_IsBackedUpAndRecovers()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(_path, "{not valid json");

        var all = await _sut.GetAllAsync();

        Assert.Empty(all);
        Assert.True(File.Exists(_path + ".bak"));
    }

    [Fact]
    public async Task Save_MultipleCardlessUsers_KeepsAllOfThem()
    {
        await _sut.SaveAsync(new RfidUser { CardId = null, Name = "Ali" });
        await _sut.SaveAsync(new RfidUser { CardId = "   ", Name = "Veli" });

        var all = await _sut.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.All(all, u => Assert.Null(u.CardId));
    }

    [Fact]
    public async Task Save_ReassigningCardToAnotherOperator_ClearsCardFromPreviousOwner()
    {
        var alice = new RfidUser { CardId = "CARD01", Name = "Alice" };
        var bob = new RfidUser { CardId = null, Name = "Bob" };
        await _sut.SaveAsync(alice);
        await _sut.SaveAsync(bob);

        bob.CardId = "card01";
        await _sut.SaveAsync(bob);

        var all = await _sut.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Null(all.Single(u => u.Id == alice.Id).CardId);
        Assert.Equal("card01", all.Single(u => u.Id == bob.Id).CardId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
