using System.Globalization;
using GameUpdater.Core.Abstractions;
using GameUpdater.Shared.Models;
using Microsoft.Data.Sqlite;

namespace GameUpdater.Data.Repositories;

public sealed class SqliteLogRepository : ILogRepository
{
    private readonly string _connectionString;

    public SqliteLogRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task AddAsync(UpdateLogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO UpdateLogs (GameId, GameName, Action, Status, Message, CreatedAt)
            VALUES ($gameId, $gameName, $action, $status, $message, $createdAt);
            """;
        command.Parameters.AddWithValue("$gameId", entry.GameId.HasValue ? entry.GameId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$gameName", entry.GameName);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$status", entry.Status);
        command.Parameters.AddWithValue("$message", entry.Message);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UpdateLogEntry>> GetRecentAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        var logs = new List<UpdateLogEntry>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, GameId, GameName, Action, Status, Message, CreatedAt
            FROM UpdateLogs
            ORDER BY CreatedAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new UpdateLogEntry
            {
                Id = reader.GetInt32(0),
                GameId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                GameName = reader.GetString(2),
                Action = reader.GetString(3),
                Status = reader.GetString(4),
                Message = reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return logs;
    }
}
