using Microsoft.Data.Sqlite;

namespace GameUpdater.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA journal_mode = WAL;";
            await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Games (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                InstallPath TEXT NOT NULL,
                Version TEXT NOT NULL,
                LaunchRelativePath TEXT NOT NULL DEFAULT '',
                LaunchArguments TEXT NOT NULL DEFAULT '',
                LastScannedAt TEXT NULL,
                LastUpdatedAt TEXT NULL,
                Notes TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 999999,
                IsHot INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS UpdateLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GameId INTEGER NULL,
                GameName TEXT NOT NULL,
                Action TEXT NOT NULL,
                Status TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_UpdateLogs_CreatedAt
                ON UpdateLogs(CreatedAt DESC);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        await AddColumnIfMissingAsync(connection, "Games", "LaunchRelativePath", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Games", "LaunchArguments", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Games", "SortOrder", "INTEGER NOT NULL DEFAULT 999999", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Games", "IsHot", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        var exists = false;
        await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var existingName = reader.GetString(1);
            if (string.Equals(existingName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        if (exists)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
