using System;
using System.Collections.Generic;
using System.IO;
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

            CREATE TABLE IF NOT EXISTS source (
                source_id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_path TEXT NOT NULL UNIQUE,
                source_type TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS series (
                series_id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                normalized_title TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS volume (
                volume_id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_id INTEGER NOT NULL,
                series_id INTEGER NULL,
                title TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(source_id) REFERENCES source(source_id) ON DELETE CASCADE,
                UNIQUE(source_id, title)
            );

            CREATE TABLE IF NOT EXISTS page (
                page_id INTEGER PRIMARY KEY AUTOINCREMENT,
                volume_id INTEGER NOT NULL,
                page_number INTEGER NOT NULL,
                page_locator TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(volume_id) REFERENCES volume(volume_id) ON DELETE CASCADE,
                UNIQUE(volume_id, page_number)
            );

            CREATE TABLE IF NOT EXISTS import_task (
                task_id TEXT PRIMARY KEY,
                raw_input TEXT NOT NULL,
                normalized_locator TEXT NOT NULL UNIQUE,
                input_kind TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS import_task_event (
                event_id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                status TEXT NOT NULL,
                event_type TEXT NOT NULL,
                message TEXT NULL,
                occurred_at TEXT NOT NULL,
                FOREIGN KEY(task_id) REFERENCES import_task(task_id) ON DELETE CASCADE
            );

            INSERT INTO app_meta (key, value)
            VALUES ('schema_version', '1')
            ON CONFLICT(key) DO NOTHING;

            INSERT INTO app_meta (key, value)
            VALUES ('created_at', CURRENT_TIMESTAMP)
            ON CONFLICT(key) DO NOTHING;
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, "volume", "series_id", "INTEGER NULL");
    }

    private static void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName, string columnTypeDeclaration)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName});";

        var exists = false;
        using (var reader = pragma.ExecuteReader())
        {
            while (reader.Read())
            {
                var existingName = reader.GetString(1);
                if (string.Equals(existingName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnTypeDeclaration};";
        alter.ExecuteNonQuery();
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

    public SampleWriteResult UpsertSampleData(
        string sourcePath,
        string sourceType,
        string volumeTitle,
        IReadOnlyList<string> pageLocators)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException("Source type cannot be empty.", nameof(sourceType));
        }

        if (string.IsNullOrWhiteSpace(volumeTitle))
        {
            throw new ArgumentException("Volume title cannot be empty.", nameof(volumeTitle));
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var upsertSource = connection.CreateCommand();
        upsertSource.Transaction = transaction;
        upsertSource.CommandText = """
            INSERT INTO source (source_path, source_type)
            VALUES ($sourcePath, $sourceType)
            ON CONFLICT(source_path) DO UPDATE SET source_type = excluded.source_type;
            """;
        upsertSource.Parameters.AddWithValue("$sourcePath", sourcePath);
        upsertSource.Parameters.AddWithValue("$sourceType", sourceType);
        upsertSource.ExecuteNonQuery();

        using var selectSourceId = connection.CreateCommand();
        selectSourceId.Transaction = transaction;
        selectSourceId.CommandText = "SELECT source_id FROM source WHERE source_path = $sourcePath LIMIT 1;";
        selectSourceId.Parameters.AddWithValue("$sourcePath", sourcePath);
        var sourceId = Convert.ToInt64(selectSourceId.ExecuteScalar());

        using var upsertVolume = connection.CreateCommand();
        upsertVolume.Transaction = transaction;
        upsertVolume.CommandText = """
            INSERT INTO volume (source_id, title)
            VALUES ($sourceId, $title)
            ON CONFLICT(source_id, title) DO NOTHING;
            """;
        upsertVolume.Parameters.AddWithValue("$sourceId", sourceId);
        upsertVolume.Parameters.AddWithValue("$title", volumeTitle);
        upsertVolume.ExecuteNonQuery();

        using var selectVolumeId = connection.CreateCommand();
        selectVolumeId.Transaction = transaction;
        selectVolumeId.CommandText = "SELECT volume_id FROM volume WHERE source_id = $sourceId AND title = $title LIMIT 1;";
        selectVolumeId.Parameters.AddWithValue("$sourceId", sourceId);
        selectVolumeId.Parameters.AddWithValue("$title", volumeTitle);
        var volumeId = Convert.ToInt64(selectVolumeId.ExecuteScalar());

        var upsertCount = 0;
        using var clearPages = connection.CreateCommand();
        clearPages.Transaction = transaction;
        clearPages.CommandText = "DELETE FROM page WHERE volume_id = $volumeId;";
        clearPages.Parameters.AddWithValue("$volumeId", volumeId);
        clearPages.ExecuteNonQuery();

        for (var i = 0; i < pageLocators.Count; i++)
        {
            using var insertPage = connection.CreateCommand();
            insertPage.Transaction = transaction;
            insertPage.CommandText = """
                INSERT INTO page (volume_id, page_number, page_locator)
                VALUES ($volumeId, $pageNumber, $locator);
                """;
            insertPage.Parameters.AddWithValue("$volumeId", volumeId);
            insertPage.Parameters.AddWithValue("$pageNumber", i + 1);
            insertPage.Parameters.AddWithValue("$locator", pageLocators[i]);
            upsertCount += insertPage.ExecuteNonQuery();
        }

        transaction.Commit();

        return new SampleWriteResult(sourceId, volumeId, upsertCount, pageLocators.Count);
    }

    public int ReadPageCountByVolume(long volumeId)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM page WHERE volume_id = $volumeId;";
        command.Parameters.AddWithValue("$volumeId", volumeId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static string BuildDefaultVolumeTitle(string sourcePath)
    {
        var title = Path.GetFileNameWithoutExtension(sourcePath);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return Path.GetFileName(sourcePath);
    }
}

public sealed record SampleWriteResult(long SourceId, long VolumeId, int InsertedPageRows, int RequestedPageCount);