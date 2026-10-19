using BrewAlert.Core.Models;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
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

    [Fact]
    public async Task NewDatabase_IsStampedWithSchemaVersion()
    {
        await _sut.GetAllAsync();

        Assert.Equal(1L, await ScalarAsync("PRAGMA user_version;"));
    }

    [Fact]
    public async Task PreVersioningDatabase_UpgradesInPlaceAndKeepsRows()
    {
        await ExecuteAsync("""
            CREATE TABLE BrewHistory (Id TEXT PRIMARY KEY, CompletedAtUtc TEXT NOT NULL, ProfileName TEXT NOT NULL,
                Type INTEGER NOT NULL, Icon TEXT NOT NULL, DurationSeconds INTEGER NOT NULL);
            INSERT INTO BrewHistory VALUES ('6f1c1a4e-4c1b-4a8e-9a55-1d2f3b4c5d6e', '2026-10-01T08:00:00.0000000Z', 'Çay', 0, '♨', 900);
            """);

        var entry = Assert.Single(await _sut.GetAllAsync());

        Assert.Equal("Çay", entry.ProfileName);
        Assert.Equal(1L, await ScalarAsync("PRAGMA user_version;"));
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var conn = new SqliteConnection($"Data Source={_testDbPath};Pooling=False");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var conn = new SqliteConnection($"Data Source={_testDbPath};Pooling=False");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
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
