using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class SqliteBrewHistoryRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteBrewHistoryRepository _sut;

    public SqliteBrewHistoryRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"brewalert_history_{Guid.NewGuid()}.db");
        _sut = new SqliteBrewHistoryRepository(_testDbPath, NullLogger<SqliteBrewHistoryRepository>.Instance);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ShouldReturnEmptyList()
    {
        var result = await _sut.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Append_PersistsAllFields()
    {
        var entry = NewEntry("Turkish Tea", BrewType.Tea, "🍵", 480);

        await _sut.AppendAsync(entry);
        var all = await _sut.GetAllAsync();

        Assert.Single(all);
        var stored = all[0];
        Assert.Equal(entry.Id, stored.Id);
        Assert.Equal("Turkish Tea", stored.ProfileName);
        Assert.Equal(BrewType.Tea, stored.Type);
        Assert.Equal("🍵", stored.Icon);
        Assert.Equal(480, stored.DurationSeconds);
        // Round-trip to UTC + ISO-8601 may lose sub-microsecond precision; allow small delta.
        Assert.True(Math.Abs((stored.CompletedAtUtc - entry.CompletedAtUtc).TotalSeconds) < 1);
    }

    [Fact]
    public async Task GetRecent_ReturnsNewestFirst_LimitedToCount()
    {
        var baseTime = DateTime.UtcNow.AddHours(-5);
        for (int i = 0; i < 5; i++)
        {
            await _sut.AppendAsync(new BrewHistoryEntry(
                Id: Guid.NewGuid(),
                CompletedAtUtc: baseTime.AddMinutes(i * 10),
                ProfileName: $"Brew {i}",
                Type: BrewType.Coffee,
                Icon: "☕",
                DurationSeconds: 240));
        }

        var recent = await _sut.GetRecentAsync(3);

        Assert.Equal(3, recent.Count);
        Assert.Equal("Brew 4", recent[0].ProfileName);
        Assert.Equal("Brew 3", recent[1].ProfileName);
        Assert.Equal("Brew 2", recent[2].ProfileName);
    }

    [Fact]
    public async Task GetRecent_WithZeroLimit_ReturnsEmpty()
    {
        await _sut.AppendAsync(NewEntry("X", BrewType.Tea, "🍵", 60));
        var recent = await _sut.GetRecentAsync(0);
        Assert.Empty(recent);
    }

    [Fact]
    public async Task Append_DuplicateId_DoesNotInsertTwice()
    {
        var id = Guid.NewGuid();
        var entry = new BrewHistoryEntry(id, DateTime.UtcNow, "X", BrewType.Tea, "🍵", 60);

        await _sut.AppendAsync(entry);
        await _sut.AppendAsync(entry);

        var all = await _sut.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task Schema_IsIdempotent_AcrossInstances()
    {
        await _sut.AppendAsync(NewEntry("First", BrewType.Tea, "🍵", 60));

        var second = new SqliteBrewHistoryRepository(_testDbPath, NullLogger<SqliteBrewHistoryRepository>.Instance);
        await second.AppendAsync(NewEntry("Second", BrewType.Coffee, "☕", 120));

        var all = await second.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    private static BrewHistoryEntry NewEntry(string name, BrewType type, string icon, int duration)
        => new(Guid.NewGuid(), DateTime.UtcNow, name, type, icon, duration);

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { /* file may be locked briefly on Windows */ }
        }
    }
}
