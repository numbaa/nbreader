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

            CREATE TABLE IF NOT EXISTS person (
                person_id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                normalized_name TEXT NOT NULL UNIQUE,
                name_pinyin TEXT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_person_name_pinyin
            ON person(name_pinyin);

            CREATE TABLE IF NOT EXISTS tag (
                tag_id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                normalized_name TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'user',
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(category, normalized_name)
            );

            CREATE INDEX IF NOT EXISTS idx_tag_category
            ON tag(category);

            CREATE TABLE IF NOT EXISTS series_person (
                series_id INTEGER NOT NULL,
                person_id INTEGER NOT NULL,
                role TEXT NOT NULL DEFAULT 'author',
                sort_order INTEGER NOT NULL DEFAULT 0,
                source TEXT NULL,
                PRIMARY KEY (series_id, person_id, role),
                FOREIGN KEY(series_id) REFERENCES series(series_id) ON DELETE CASCADE,
                FOREIGN KEY(person_id) REFERENCES person(person_id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_series_person_person_role
            ON series_person(person_id, role);

            CREATE TABLE IF NOT EXISTS series_tag (
                series_id INTEGER NOT NULL,
                tag_id INTEGER NOT NULL,
                source TEXT NULL,
                PRIMARY KEY (series_id, tag_id),
                FOREIGN KEY(series_id) REFERENCES series(series_id) ON DELETE CASCADE,
                FOREIGN KEY(tag_id) REFERENCES tag(tag_id) ON DELETE CASCADE
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

            CREATE TABLE IF NOT EXISTS reading_progress (
                volume_id INTEGER PRIMARY KEY,
                current_page INTEGER NOT NULL,
                max_page_reached INTEGER NOT NULL,
                completed INTEGER NOT NULL DEFAULT 0,
                last_read_at TEXT NOT NULL,
                reading_mode TEXT NULL,
                reading_direction TEXT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY(volume_id) REFERENCES volume(volume_id) ON DELETE CASCADE,
                CHECK (current_page >= 0),
                CHECK (max_page_reached >= 0)
            );

            CREATE INDEX IF NOT EXISTS idx_reading_progress_last_read_at
            ON reading_progress(last_read_at DESC);

            CREATE INDEX IF NOT EXISTS idx_reading_progress_completed
            ON reading_progress(completed);

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
        EnsureColumnExists(connection, "volume", "volume_number", "INTEGER NULL");
        EnsureColumnExists(connection, "series", "title_pinyin", "TEXT NULL");

        EnsureSearchIndexSchema(connection);
        RebuildSearchIndexes(connection);
    }

    public void RebuildSearchIndexes()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        RebuildSearchIndexes(connection);
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

    private static void EnsureSearchIndexSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE VIRTUAL TABLE IF NOT EXISTS series_fts
            USING fts5(
                title,
                normalized_title,
                title_pinyin,
                tokenize = 'unicode61'
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS person_fts
            USING fts5(
                name,
                normalized_name,
                name_pinyin,
                tokenize = 'unicode61'
            );

            CREATE TRIGGER IF NOT EXISTS trg_series_ai
            AFTER INSERT ON series
            BEGIN
                INSERT INTO series_fts(rowid, title, normalized_title, title_pinyin)
                VALUES (new.series_id, new.title, new.normalized_title, COALESCE(new.title_pinyin, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_series_au
            AFTER UPDATE OF title, normalized_title, title_pinyin ON series
            BEGIN
                DELETE FROM series_fts WHERE rowid = old.series_id;
                INSERT INTO series_fts(rowid, title, normalized_title, title_pinyin)
                VALUES (new.series_id, new.title, new.normalized_title, COALESCE(new.title_pinyin, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_series_ad
            AFTER DELETE ON series
            BEGIN
                DELETE FROM series_fts WHERE rowid = old.series_id;
            END;

            CREATE TRIGGER IF NOT EXISTS trg_person_ai
            AFTER INSERT ON person
            BEGIN
                INSERT INTO person_fts(rowid, name, normalized_name, name_pinyin)
                VALUES (new.person_id, new.name, new.normalized_name, COALESCE(new.name_pinyin, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_person_au
            AFTER UPDATE OF name, normalized_name, name_pinyin ON person
            BEGIN
                DELETE FROM person_fts WHERE rowid = old.person_id;
                INSERT INTO person_fts(rowid, name, normalized_name, name_pinyin)
                VALUES (new.person_id, new.name, new.normalized_name, COALESCE(new.name_pinyin, ''));
            END;

            CREATE TRIGGER IF NOT EXISTS trg_person_ad
            AFTER DELETE ON person
            BEGIN
                DELETE FROM person_fts WHERE rowid = old.person_id;
            END;
            """;
        command.ExecuteNonQuery();
    }

    private static void RebuildSearchIndexes(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE series
            SET title_pinyin = COALESCE(NULLIF(title_pinyin, ''), normalized_title);

            UPDATE person
            SET name_pinyin = COALESCE(NULLIF(name_pinyin, ''), normalized_name);

            DELETE FROM series_fts;
            INSERT INTO series_fts(rowid, title, normalized_title, title_pinyin)
            SELECT series_id, title, normalized_title, COALESCE(title_pinyin, '')
            FROM series;

            DELETE FROM person_fts;
            INSERT INTO person_fts(rowid, name, normalized_name, name_pinyin)
            SELECT person_id, name, normalized_name, COALESCE(name_pinyin, '')
            FROM person;
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