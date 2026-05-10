namespace BrewAlert.Infrastructure.Persistence;

using System.Globalization;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite-backed brew history. Schema is created lazily on first call.
/// Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class SqliteBrewHistoryRepository : IBrewHistoryRepository
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS BrewHistory (
            Id TEXT PRIMARY KEY,
            CompletedAtUtc TEXT NOT NULL,
            ProfileName TEXT NOT NULL,
            Type INTEGER NOT NULL,
            Icon TEXT NOT NULL,
            DurationSeconds INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_BrewHistory_CompletedAt
            ON BrewHistory(CompletedAtUtc DESC);
        """;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<SqliteBrewHistoryRepository> _logger;
    private bool _schemaReady;

    public SqliteBrewHistoryRepository(
        string dbPath,
        ILogger<SqliteBrewHistoryRepository> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(dbPath);
        _logger = logger;

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
        }.ToString();
    }

    public async Task AppendAsync(BrewHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _semaphore.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO BrewHistory (Id, CompletedAtUtc, ProfileName, Type, Icon, DurationSeconds)
                VALUES ($id, $completed, $name, $type, $icon, $duration)
                ON CONFLICT(Id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
            cmd.Parameters.AddWithValue("$completed", entry.CompletedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$name", entry.ProfileName);
            cmd.Parameters.AddWithValue("$type", (int)entry.Type);
            cmd.Parameters.AddWithValue("$icon", entry.Icon);
            cmd.Parameters.AddWithValue("$duration", entry.DurationSeconds);
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("Appended brew history entry {EntryId} ({ProfileName}).", entry.Id, entry.ProfileName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<BrewHistoryEntry>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        if (limit <= 0) return [];
        await _semaphore.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, CompletedAtUtc, ProfileName, Type, Icon, DurationSeconds
                FROM BrewHistory
                ORDER BY CompletedAtUtc DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);
            return await ReadEntriesAsync(cmd, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<BrewHistoryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, CompletedAtUtc, ProfileName, Type, Icon, DurationSeconds
                FROM BrewHistory
                ORDER BY CompletedAtUtc DESC;
                """;
            return await ReadEntriesAsync(cmd, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        try
        {
            await conn.OpenAsync(ct);
            if (!_schemaReady)
            {
                await using var schemaCmd = conn.CreateCommand();
                schemaCmd.CommandText = CreateTableSql;
                await schemaCmd.ExecuteNonQueryAsync(ct);
                _schemaReady = true;
            }
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static async Task<IReadOnlyList<BrewHistoryEntry>> ReadEntriesAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<BrewHistoryEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new BrewHistoryEntry(
                Id: Guid.Parse(reader.GetString(0)),
                CompletedAtUtc: DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ProfileName: reader.GetString(2),
                Type: (BrewType)reader.GetInt32(3),
                Icon: reader.GetString(4),
                DurationSeconds: reader.GetInt32(5)));
        }
        return list;
    }
}
