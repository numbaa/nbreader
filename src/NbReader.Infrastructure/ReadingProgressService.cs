using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NbReader.Infrastructure;

public sealed class ReadingProgressService
{
    private readonly string _connectionString;

    public ReadingProgressService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<ReadingProgressSnapshot?> GetProgressAsync(long volumeId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT volume_id,
                   current_page,
                   max_page_reached,
                   completed,
                   last_read_at,
                   reading_mode,
                   reading_direction,
                   updated_at
            FROM reading_progress
            WHERE volume_id = $volumeId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$volumeId", volumeId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReadingProgressSnapshot(
            VolumeId: reader.GetInt64(0),
            CurrentPageIndex: reader.GetInt32(1),
            MaxPageReached: reader.GetInt32(2),
            Completed: reader.GetInt32(3) == 1,
            LastReadAt: ParseSqliteTimestampOrNow(reader.IsDBNull(4) ? null : reader.GetString(4)),
            ReadingMode: reader.IsDBNull(5) ? null : reader.GetString(5),
            ReadingDirection: reader.IsDBNull(6) ? null : reader.GetString(6),
            UpdatedAt: ParseSqliteTimestampOrNow(reader.IsDBNull(7) ? null : reader.GetString(7)));
    }

    public async Task UpsertProgressAsync(ReadingProgressSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO reading_progress (
                volume_id,
                current_page,
                max_page_reached,
                completed,
                last_read_at,
                reading_mode,
                reading_direction,
                updated_at)
            VALUES (
                $volumeId,
                $currentPage,
                $maxPageReached,
                $completed,
                $lastReadAt,
                $readingMode,
                $readingDirection,
                $updatedAt)
            ON CONFLICT(volume_id) DO UPDATE SET
                current_page = excluded.current_page,
                max_page_reached = CASE
                    WHEN excluded.max_page_reached > reading_progress.max_page_reached THEN excluded.max_page_reached
                    ELSE reading_progress.max_page_reached
                END,
                completed = excluded.completed,
                last_read_at = excluded.last_read_at,
                reading_mode = excluded.reading_mode,
                reading_direction = excluded.reading_direction,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$volumeId", snapshot.VolumeId);
        command.Parameters.AddWithValue("$currentPage", Math.Max(0, snapshot.CurrentPageIndex));
        command.Parameters.AddWithValue("$maxPageReached", Math.Max(0, snapshot.MaxPageReached));
        command.Parameters.AddWithValue("$completed", snapshot.Completed ? 1 : 0);
        command.Parameters.AddWithValue("$lastReadAt", snapshot.LastReadAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$readingMode", (object?)snapshot.ReadingMode ?? DBNull.Value);
        command.Parameters.AddWithValue("$readingDirection", (object?)snapshot.ReadingDirection ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", snapshot.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentReadingEntry>> GetRecentReadingsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT rp.volume_id,
                   v.series_id,
                   COALESCE(s.title, 'Unsorted') AS series_title,
                   v.title,
                   rp.current_page,
                   COUNT(p.page_id) AS page_count,
                   rp.completed,
                   rp.last_read_at
            FROM reading_progress rp
            INNER JOIN volume v ON v.volume_id = rp.volume_id
            LEFT JOIN series s ON s.series_id = v.series_id
            LEFT JOIN page p ON p.volume_id = v.volume_id
            GROUP BY rp.volume_id, v.series_id, s.title, v.title, rp.current_page, rp.completed, rp.last_read_at
            ORDER BY rp.last_read_at DESC, rp.volume_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var rows = new List<RecentReadingEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new RecentReadingEntry(
                VolumeId: reader.GetInt64(0),
                SeriesId: reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                SeriesTitle: reader.GetString(2),
                VolumeTitle: reader.GetString(3),
                CurrentPageIndex: reader.GetInt32(4),
                PageCount: reader.GetInt32(5),
                Completed: reader.GetInt32(6) == 1,
                LastReadAt: ParseSqliteTimestampOrNow(reader.IsDBNull(7) ? null : reader.GetString(7))));
        }

        return rows;
    }

    public async Task<RecentReadingEntry?> GetContinueReadingAsync(CancellationToken cancellationToken = default)
    {
        var rows = await GetRecentReadingsAsync(limit: 20, cancellationToken);
        return rows.FirstOrDefault(row => !row.Completed)
            ?? rows.FirstOrDefault();
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
