using System;
using Microsoft.Data.Sqlite;
using NbReader.Import;

namespace NbReader.Infrastructure;

public sealed class SqliteImportTaskStore : IImportTaskStore
{
    private readonly string _connectionString;

    public SqliteImportTaskStore(AppDatabase database)
    {
        _connectionString = database.ConnectionString;
    }

    public ImportTask? FindByNormalizedLocator(string normalizedLocator)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_id, raw_input, normalized_locator, input_kind, status, created_at, updated_at
            FROM import_task
            WHERE normalized_locator = $locator
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$locator", normalizedLocator);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var taskId = Guid.Parse(reader.GetString(0));
        var rawInput = reader.GetString(1);
        var locator = reader.GetString(2);
        var inputKind = ParseInputKind(reader.GetString(3));
        var status = ParseStatus(reader.GetString(4));
        var createdAt = DateTimeOffset.Parse(reader.GetString(5));
        var updatedAt = DateTimeOffset.Parse(reader.GetString(6));

        return new ImportTask(taskId, rawInput, locator, inputKind, status, createdAt, updatedAt);
    }

    public void UpsertTask(ImportTask task)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO import_task (task_id, raw_input, normalized_locator, input_kind, status, created_at, updated_at)
            VALUES ($taskId, $rawInput, $locator, $inputKind, $status, $createdAt, $updatedAt)
            ON CONFLICT(task_id) DO UPDATE SET
                raw_input = excluded.raw_input,
                normalized_locator = excluded.normalized_locator,
                input_kind = excluded.input_kind,
                status = excluded.status,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$taskId", task.TaskId.ToString());
        command.Parameters.AddWithValue("$rawInput", task.RawInput);
        command.Parameters.AddWithValue("$locator", task.NormalizedLocator);
        command.Parameters.AddWithValue("$inputKind", task.InputKind.ToString());
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", task.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void AppendEvent(ImportTaskEvent taskEvent)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO import_task_event (task_id, status, event_type, message, occurred_at)
            VALUES ($taskId, $status, $eventType, $message, $occurredAt);
            """;
        command.Parameters.AddWithValue("$taskId", taskEvent.TaskId.ToString());
        command.Parameters.AddWithValue("$status", taskEvent.Status.ToString());
        command.Parameters.AddWithValue("$eventType", taskEvent.EventType);
        command.Parameters.AddWithValue("$message", (object?)taskEvent.Message ?? DBNull.Value);
        command.Parameters.AddWithValue("$occurredAt", taskEvent.OccurredAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static ImportTaskStatus ParseStatus(string raw)
    {
        return Enum.TryParse<ImportTaskStatus>(raw, ignoreCase: true, out var value)
            ? value
            : ImportTaskStatus.Pending;
    }

    private static ImportInputKind ParseInputKind(string raw)
    {
        return Enum.TryParse<ImportInputKind>(raw, ignoreCase: true, out var value)
            ? value
            : ImportInputKind.Unknown;
    }
}
