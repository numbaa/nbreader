using System;
using Microsoft.Data.Sqlite;

namespace NbReader.Infrastructure;

public sealed class AppDatabase
{
    public string DatabaseFilePath { get; }

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabaseFilePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true,
    }.ToString();

    public AppDatabase(string databaseFilePath)
    {
        DatabaseFilePath = databaseFilePath;
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            INSERT INTO app_meta (key, value)
            VALUES ('schema_version', '1')
            ON CONFLICT(key) DO NOTHING;

            INSERT INTO app_meta (key, value)
            VALUES ('created_at', CURRENT_TIMESTAMP)
            ON CONFLICT(key) DO NOTHING;
            """;
        command.ExecuteNonQuery();
    }

    public string? ReadMetaValue(string key)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }
}