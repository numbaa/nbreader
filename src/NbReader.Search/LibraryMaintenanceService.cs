using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NbReader.Search;

public sealed class LibraryMaintenanceService
{
    private readonly string _connectionString;

    public LibraryMaintenanceService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<UnorganizedVolumeEntry>> GetUnorganizedVolumesAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT v.volume_id,
                   v.title,
                   s.source_path,
                   v.created_at
            FROM volume v
            INNER JOIN source s ON s.source_id = v.source_id
            LEFT JOIN series se ON se.series_id = v.series_id
            WHERE v.series_id IS NULL OR se.series_id IS NULL
            ORDER BY v.created_at DESC, v.volume_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", NormalizeLimit(limit));

        var rows = new List<UnorganizedVolumeEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAtText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            rows.Add(new UnorganizedVolumeEntry(
                VolumeId: reader.GetInt64(0),
                VolumeTitle: reader.GetString(1),
                SourcePath: reader.GetString(2),
                CreatedAt: ParseSqliteTimestampOrNow(createdAtText)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<FailedImportTaskEntry>> GetFailedImportTasksAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.task_id,
                   t.raw_input,
                   t.normalized_locator,
                   t.input_kind,
                   t.status,
                   t.updated_at,
                   (
                       SELECT e.message
                       FROM import_task_event e
                       WHERE e.task_id = t.task_id
                         AND lower(e.status) = 'failed'
                       ORDER BY e.occurred_at DESC, e.event_id DESC
                       LIMIT 1
                   ) AS last_error_message
            FROM import_task t
            WHERE lower(t.status) = 'failed'
            ORDER BY t.updated_at DESC, t.task_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", NormalizeLimit(limit));

        var rows = new List<FailedImportTaskEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var updatedAtText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            rows.Add(new FailedImportTaskEntry(
                TaskId: Guid.Parse(reader.GetString(0)),
                RawInput: reader.GetString(1),
                NormalizedLocator: reader.GetString(2),
                InputKind: reader.GetString(3),
                Status: reader.GetString(4),
                UpdatedAt: ParseSqliteTimestampOrNow(updatedAtText),
                LastErrorMessage: reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    public async Task<bool> RetryFailedTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE import_task
            SET status = 'Pending',
                updated_at = $updatedAt
            WHERE task_id = $taskId
              AND lower(status) = 'failed';
            """;
        update.Parameters.AddWithValue("$updatedAt", now);
        update.Parameters.AddWithValue("$taskId", taskId.ToString());

        var affected = await update.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        using var appendEvent = connection.CreateCommand();
        appendEvent.Transaction = transaction;
        appendEvent.CommandText = """
            INSERT INTO import_task_event (task_id, status, event_type, message, occurred_at)
            VALUES ($taskId, 'Pending', 'retry_requested', 'Retry requested by user.', $occurredAt);
            """;
        appendEvent.Parameters.AddWithValue("$taskId", taskId.ToString());
        appendEvent.Parameters.AddWithValue("$occurredAt", now);
        await appendEvent.ExecuteNonQueryAsync(cancellationToken);

        transaction.Commit();
        return true;
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return 200;
        }

        return Math.Min(limit, 1000);
    }

    private static DateTimeOffset ParseSqliteTimestampOrNow(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }
}
