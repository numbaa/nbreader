using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NbReader.Search;

public sealed class SeriesSearchService
{
    private readonly string _connectionString;

    public SeriesSearchService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(SeriesSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder(
            """
            WITH series_agg AS (
                SELECT s.series_id,
                       s.title,
                       COUNT(DISTINCT v.volume_id) AS volume_count,
                       COALESCE(MAX(v.created_at), s.created_at) AS latest_updated_at,
                       CAST(strftime('%Y', COALESCE(MIN(v.created_at), s.created_at)) AS INTEGER) AS release_year,
                       COUNT(DISTINCT rp.volume_id) AS progress_volume_count,
                       SUM(CASE WHEN rp.completed = 1 THEN 1 ELSE 0 END) AS completed_volume_count
                FROM series s
                LEFT JOIN volume v ON v.series_id = s.series_id
                LEFT JOIN reading_progress rp ON rp.volume_id = v.volume_id
                WHERE 1 = 1
            """);

        if (!string.IsNullOrWhiteSpace(query.TitleKeyword))
        {
            sql.Append(
                """
                  AND (s.title LIKE $titleLike OR s.normalized_title LIKE $titleLike)
                """);
        }

        if (!string.IsNullOrWhiteSpace(query.AuthorKeyword))
        {
            sql.Append(
                """
                  AND EXISTS (
                        SELECT 1
                        FROM series_person sp
                        INNER JOIN person p ON p.person_id = sp.person_id
                        WHERE sp.series_id = s.series_id
                          AND (p.name LIKE $authorLike OR p.normalized_name LIKE $authorLike)
                  )
                """);
        }

        if (!string.IsNullOrWhiteSpace(query.TagKeyword))
        {
            sql.Append(
                """
                  AND EXISTS (
                        SELECT 1
                        FROM series_tag st
                        INNER JOIN tag t ON t.tag_id = st.tag_id
                        WHERE st.series_id = s.series_id
                          AND (t.name LIKE $tagLike OR t.normalized_name LIKE $tagLike)
                  )
                """);
        }

        sql.Append(
            """
                GROUP BY s.series_id, s.title, s.created_at
            )
            SELECT series_id,
                   title,
                   volume_count,
                   latest_updated_at,
                   release_year,
                   CASE
                       WHEN progress_volume_count = 0 THEN 'unread'
                       WHEN completed_volume_count = progress_volume_count THEN 'completed'
                       ELSE 'in_progress'
                   END AS reading_status
            FROM series_agg
            WHERE 1 = 1
            """);

        if (query.Year is not null)
        {
            sql.Append(" AND release_year = $year");
        }

        if (query.ReadingStatus != SeriesSearchReadingStatus.Any)
        {
            sql.Append(" AND reading_status = $readingStatus");
        }

        sql.Append(query.SortBy switch
        {
            SeriesSearchSortBy.TitleAsc => " ORDER BY title COLLATE NOCASE ASC, series_id DESC",
            _ => " ORDER BY latest_updated_at DESC, series_id DESC",
        });

        sql.Append(" LIMIT $limit;");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        command.Parameters.AddWithValue("$limit", NormalizeLimit(query.Limit));

        if (!string.IsNullOrWhiteSpace(query.TitleKeyword))
        {
            command.Parameters.AddWithValue("$titleLike", BuildLikePattern(query.TitleKeyword));
        }

        if (!string.IsNullOrWhiteSpace(query.AuthorKeyword))
        {
            command.Parameters.AddWithValue("$authorLike", BuildLikePattern(query.AuthorKeyword));
        }

        if (!string.IsNullOrWhiteSpace(query.TagKeyword))
        {
            command.Parameters.AddWithValue("$tagLike", BuildLikePattern(query.TagKeyword));
        }

        if (query.Year is not null)
        {
            command.Parameters.AddWithValue("$year", query.Year.Value);
        }

        if (query.ReadingStatus != SeriesSearchReadingStatus.Any)
        {
            command.Parameters.AddWithValue("$readingStatus", ToDbStatus(query.ReadingStatus));
        }

        var rows = new List<SeriesSearchResult>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var latestText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            int? year = reader.IsDBNull(4) ? null : reader.GetInt32(4);
            rows.Add(new SeriesSearchResult(
                SeriesId: reader.GetInt64(0),
                Title: reader.GetString(1),
                VolumeCount: reader.GetInt32(2),
                LatestUpdatedAt: ParseSqliteTimestampOrNow(latestText),
                Year: year,
                ReadingStatus: ParseDbStatus(reader.GetString(5))));
        }

        return rows;
    }

    private static string BuildLikePattern(string keyword)
    {
        var trimmed = keyword.Trim();
        return $"%{trimmed}%";
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return 200;
        }

        return Math.Min(limit, 1000);
    }

    private static string ToDbStatus(SeriesSearchReadingStatus status)
    {
        return status switch
        {
            SeriesSearchReadingStatus.Unread => "unread",
            SeriesSearchReadingStatus.InProgress => "in_progress",
            SeriesSearchReadingStatus.Completed => "completed",
            _ => "any",
        };
    }

    private static SeriesSearchReadingStatus ParseDbStatus(string? text)
    {
        return text switch
        {
            "unread" => SeriesSearchReadingStatus.Unread,
            "in_progress" => SeriesSearchReadingStatus.InProgress,
            "completed" => SeriesSearchReadingStatus.Completed,
            _ => SeriesSearchReadingStatus.Any,
        };
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
