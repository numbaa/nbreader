using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NbReader.Catalog;

public sealed class SeriesQueryService
{
    private readonly string _connectionString;

    public SeriesQueryService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<SeriesOverview>> GetSeriesListAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.series_id,
                   s.title,
                   COUNT(DISTINCT v.volume_id) AS volume_count,
                   COALESCE(MAX(v.created_at), s.created_at) AS latest_updated_at
            FROM series s
            LEFT JOIN volume v ON v.series_id = s.series_id
            GROUP BY s.series_id, s.title, s.created_at
            ORDER BY latest_updated_at DESC, s.series_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var rows = new List<SeriesOverview>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var latestText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            rows.Add(new SeriesOverview(
                SeriesId: reader.GetInt64(0),
                Title: reader.GetString(1),
                VolumeCount: reader.GetInt32(2),
                LatestUpdatedAt: ParseSqliteTimestampOrNow(latestText)));
        }

        return rows;
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
