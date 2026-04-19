using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NbReader.Catalog;

public sealed class VolumeQueryService
{
    private readonly string _connectionString;

    public VolumeQueryService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<VolumeOverview>> GetVolumesBySeriesAsync(long seriesId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT v.volume_id,
                   v.series_id,
                   v.title,
                 v.volume_number,
                 COUNT(p.page_id) AS page_count,
                   v.created_at
            FROM volume v
            LEFT JOIN page p ON p.volume_id = v.volume_id
            WHERE v.series_id = $seriesId
             GROUP BY v.volume_id, v.series_id, v.title, v.volume_number, v.created_at
             ORDER BY CASE WHEN v.volume_number IS NULL THEN 1 ELSE 0 END ASC,
                v.volume_number ASC,
                v.created_at ASC,
                v.volume_id ASC;
            """;
        command.Parameters.AddWithValue("$seriesId", seriesId);

        var rows = new List<VolumeOverview>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAtText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            rows.Add(new VolumeOverview(
                VolumeId: reader.GetInt64(0),
                SeriesId: reader.GetInt64(1),
                Title: reader.GetString(2),
                VolumeNumber: reader.IsDBNull(3) ? null : reader.GetInt32(3),
                PageCount: reader.GetInt32(4),
                CreatedAt: ParseSqliteTimestampOrNow(createdAtText)));
        }

        return rows;
    }

    public async Task<VolumeReaderContext?> GetVolumeReaderContextAsync(long volumeId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var volumeCommand = connection.CreateCommand();
        volumeCommand.CommandText = """
            SELECT v.volume_id,
                 COALESCE(v.series_id, 0) AS series_id,
                   v.title,
                   s.source_path
            FROM volume v
            INNER JOIN source s ON s.source_id = v.source_id
            WHERE v.volume_id = $volumeId
            LIMIT 1;
            """;
        volumeCommand.Parameters.AddWithValue("$volumeId", volumeId);

        long id;
        long seriesId;
        string title;
        string sourcePath;
        using (var reader = await volumeCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetInt64(0);
            seriesId = reader.GetInt64(1);
            title = reader.GetString(2);
            sourcePath = reader.GetString(3);
        }

        using var pageCommand = connection.CreateCommand();
        pageCommand.CommandText = """
            SELECT p.page_locator
            FROM page p
            WHERE p.volume_id = $volumeId
            ORDER BY p.page_number ASC;
            """;
        pageCommand.Parameters.AddWithValue("$volumeId", volumeId);

        var locators = new List<string>();
        using (var pageReader = await pageCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await pageReader.ReadAsync(cancellationToken))
            {
                if (!pageReader.IsDBNull(0))
                {
                    locators.Add(pageReader.GetString(0));
                }
            }
        }

        return new VolumeReaderContext(id, seriesId, title, sourcePath, locators);
    }

    public async Task<long?> GetNextVolumeIdAsync(long currentVolumeId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH current_volume AS (
                SELECT volume_id, series_id, created_at
                FROM volume
                WHERE volume_id = $currentVolumeId
                LIMIT 1
            )
            SELECT v.volume_id
            FROM volume v
            INNER JOIN current_volume c ON c.series_id = v.series_id
            WHERE v.series_id IS NOT NULL
              AND (
                    v.created_at > c.created_at
                    OR (v.created_at = c.created_at AND v.volume_id > c.volume_id)
                  )
            ORDER BY v.created_at ASC, v.volume_id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$currentVolumeId", currentVolumeId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt64(result);
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
