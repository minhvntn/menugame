using System.Globalization;
using GameUpdater.Core.Abstractions;
using GameUpdater.Shared.Models;
using Microsoft.Data.Sqlite;

namespace GameUpdater.Data.Repositories;

public sealed class SqliteGameRepository : IGameRepository
{
    private readonly string _connectionString;

    public SqliteGameRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task<IReadOnlyList<GameRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameRecord>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Category, InstallPath, Version, LaunchRelativePath, LaunchArguments, LastScannedAt, LastUpdatedAt, Notes, SortOrder, IsHot
            FROM Games
            ORDER BY SortOrder ASC, Name COLLATE NOCASE ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(MapGame(reader));
        }

        return games;
    }

    public async Task<GameRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Category, InstallPath, Version, LaunchRelativePath, LaunchArguments, LastScannedAt, LastUpdatedAt, Notes, SortOrder, IsHot
            FROM Games
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapGame(reader);
        }

        return null;
    }

    public async Task<int> InsertAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Games (Name, Category, InstallPath, Version, LaunchRelativePath, LaunchArguments, LastScannedAt, LastUpdatedAt, Notes, SortOrder, IsHot)
            VALUES ($name, $category, $installPath, $version, $launchRelativePath, $launchArguments, $lastScannedAt, $lastUpdatedAt, $notes, $sortOrder, $isHot);

            SELECT last_insert_rowid();
            """;
        AddParameters(command, game);

        var insertedId = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async Task UpdateAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Games
            SET
                Name = $name,
                Category = $category,
                InstallPath = $installPath,
                Version = $version,
                LaunchRelativePath = $launchRelativePath,
                LaunchArguments = $launchArguments,
                LastScannedAt = $lastScannedAt,
                LastUpdatedAt = $lastUpdatedAt,
                Notes = $notes,
                SortOrder = $sortOrder,
                IsHot = $isHot
            WHERE Id = $id;
            """;
        AddParameters(command, game);
        command.Parameters.AddWithValue("$id", game.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Games WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static GameRecord MapGame(SqliteDataReader reader)
    {
        return new GameRecord
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Category = reader.GetString(2),
            InstallPath = reader.GetString(3),
            Version = reader.GetString(4),
            LaunchRelativePath = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            LaunchArguments = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            LastScannedAt = ParseNullableDateTime(reader.IsDBNull(7) ? null : reader.GetString(7)),
            LastUpdatedAt = ParseNullableDateTime(reader.IsDBNull(8) ? null : reader.GetString(8)),
            Notes = reader.GetString(9),
            SortOrder = reader.GetInt32(10),
            IsHot = !reader.IsDBNull(11) && reader.GetInt32(11) != 0
        };
    }

    private static void AddParameters(SqliteCommand command, GameRecord game)
    {
        command.Parameters.AddWithValue("$name", game.Name);
        command.Parameters.AddWithValue("$category", game.Category);
        command.Parameters.AddWithValue("$installPath", game.InstallPath);
        command.Parameters.AddWithValue("$version", game.Version);
        command.Parameters.AddWithValue("$launchRelativePath", game.LaunchRelativePath ?? string.Empty);
        command.Parameters.AddWithValue("$launchArguments", game.LaunchArguments ?? string.Empty);
        command.Parameters.AddWithValue("$lastScannedAt", ToDbValue(game.LastScannedAt));
        command.Parameters.AddWithValue("$lastUpdatedAt", ToDbValue(game.LastUpdatedAt));
        command.Parameters.AddWithValue("$notes", game.Notes);
        command.Parameters.AddWithValue("$sortOrder", game.SortOrder);
        command.Parameters.AddWithValue("$isHot", game.IsHot ? 1 : 0);
    }

    private static object ToDbValue(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value;
    }

    private static DateTime? ParseNullableDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
