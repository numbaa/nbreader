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
                   COUNT(p.page_id) AS page_count,
                   v.created_at
            FROM volume v
            LEFT JOIN page p ON p.volume_id = v.volume_id
            WHERE v.series_id = $seriesId
            GROUP BY v.volume_id, v.series_id, v.title, v.created_at
            ORDER BY v.created_at ASC, v.volume_id ASC;
            """;
        command.Parameters.AddWithValue("$seriesId", seriesId);

        var rows = new List<VolumeOverview>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAtText = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            rows.Add(new VolumeOverview(
                VolumeId: reader.GetInt64(0),
                SeriesId: reader.GetInt64(1),
                Title: reader.GetString(2),
                PageCount: reader.GetInt32(3),
                CreatedAt: ParseSqliteTimestampOrNow(createdAtText)));
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
