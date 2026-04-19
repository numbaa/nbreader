using Microsoft.Data.Sqlite;

namespace NbReader.Search;

public sealed class SeriesMetadataEditService
{
    private readonly string _connectionString;

    public SeriesMetadataEditService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<SeriesMetadataSnapshot> GetSeriesMetadataAsync(long seriesId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var authors = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT p.name
                FROM series_person sp
                INNER JOIN person p ON p.person_id = sp.person_id
                WHERE sp.series_id = $seriesId
                  AND lower(sp.role) = 'author'
                ORDER BY sp.sort_order ASC, p.name COLLATE NOCASE ASC;
                """;
            command.Parameters.AddWithValue("$seriesId", seriesId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                authors.Add(reader.GetString(0));
            }
        }

        var tags = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT t.name
                FROM series_tag st
                INNER JOIN tag t ON t.tag_id = st.tag_id
                WHERE st.series_id = $seriesId
                ORDER BY t.name COLLATE NOCASE ASC;
                """;
            command.Parameters.AddWithValue("$seriesId", seriesId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tags.Add(reader.GetString(0));
            }
        }

        return new SeriesMetadataSnapshot(authors, tags);
    }

    public async Task<bool> ReplaceSeriesMetadataAsync(long seriesId, IReadOnlyList<string> authors, IReadOnlyList<string> tags, CancellationToken cancellationToken = default)
    {
        if (seriesId <= 0)
        {
            return false;
        }

        var normalizedAuthors = NormalizeItems(authors);
        var normalizedTags = NormalizeItems(tags);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        using (var exists = connection.CreateCommand())
        {
            exists.Transaction = transaction;
            exists.CommandText = "SELECT COUNT(1) FROM series WHERE series_id = $seriesId;";
            exists.Parameters.AddWithValue("$seriesId", seriesId);
            var hasSeries = Convert.ToInt32(await exists.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!hasSeries)
            {
                transaction.Rollback();
                return false;
            }
        }

        using (var clearAuthors = connection.CreateCommand())
        {
            clearAuthors.Transaction = transaction;
            clearAuthors.CommandText = "DELETE FROM series_person WHERE series_id = $seriesId AND lower(role) = 'author';";
            clearAuthors.Parameters.AddWithValue("$seriesId", seriesId);
            await clearAuthors.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var clearTags = connection.CreateCommand())
        {
            clearTags.Transaction = transaction;
            clearTags.CommandText = "DELETE FROM series_tag WHERE series_id = $seriesId;";
            clearTags.Parameters.AddWithValue("$seriesId", seriesId);
            await clearTags.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < normalizedAuthors.Count; index++)
        {
            var name = normalizedAuthors[index];
            var normalizedName = Normalize(name);

            using (var upsertPerson = connection.CreateCommand())
            {
                upsertPerson.Transaction = transaction;
                upsertPerson.CommandText = """
                    INSERT INTO person (name, normalized_name, name_pinyin, updated_at)
                    VALUES ($name, $normalizedName, $namePinyin, CURRENT_TIMESTAMP)
                    ON CONFLICT(normalized_name) DO UPDATE SET
                        name = excluded.name,
                        name_pinyin = COALESCE(NULLIF(person.name_pinyin, ''), excluded.name_pinyin),
                        updated_at = CURRENT_TIMESTAMP;
                    """;
                upsertPerson.Parameters.AddWithValue("$name", name);
                upsertPerson.Parameters.AddWithValue("$normalizedName", normalizedName);
                upsertPerson.Parameters.AddWithValue("$namePinyin", normalizedName);
                await upsertPerson.ExecuteNonQueryAsync(cancellationToken);
            }

            long personId;
            using (var queryPerson = connection.CreateCommand())
            {
                queryPerson.Transaction = transaction;
                queryPerson.CommandText = "SELECT person_id FROM person WHERE normalized_name = $normalizedName LIMIT 1;";
                queryPerson.Parameters.AddWithValue("$normalizedName", normalizedName);
                personId = Convert.ToInt64(await queryPerson.ExecuteScalarAsync(cancellationToken));
            }

            using var link = connection.CreateCommand();
            link.Transaction = transaction;
            link.CommandText = """
                INSERT INTO series_person (series_id, person_id, role, sort_order, source)
                VALUES ($seriesId, $personId, 'author', $sortOrder, 'manual')
                ON CONFLICT(series_id, person_id, role) DO UPDATE SET
                    sort_order = excluded.sort_order,
                    source = 'manual';
                """;
            link.Parameters.AddWithValue("$seriesId", seriesId);
            link.Parameters.AddWithValue("$personId", personId);
            link.Parameters.AddWithValue("$sortOrder", index);
            await link.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var tagName in normalizedTags)
        {
            var normalizedName = Normalize(tagName);

            using (var upsertTag = connection.CreateCommand())
            {
                upsertTag.Transaction = transaction;
                upsertTag.CommandText = """
                    INSERT INTO tag (name, normalized_name, category, updated_at)
                    VALUES ($name, $normalizedName, 'user', CURRENT_TIMESTAMP)
                    ON CONFLICT(category, normalized_name) DO UPDATE SET
                        name = excluded.name,
                        updated_at = CURRENT_TIMESTAMP;
                    """;
                upsertTag.Parameters.AddWithValue("$name", tagName);
                upsertTag.Parameters.AddWithValue("$normalizedName", normalizedName);
                await upsertTag.ExecuteNonQueryAsync(cancellationToken);
            }

            long tagId;
            using (var queryTag = connection.CreateCommand())
            {
                queryTag.Transaction = transaction;
                queryTag.CommandText = "SELECT tag_id FROM tag WHERE category = 'user' AND normalized_name = $normalizedName LIMIT 1;";
                queryTag.Parameters.AddWithValue("$normalizedName", normalizedName);
                tagId = Convert.ToInt64(await queryTag.ExecuteScalarAsync(cancellationToken));
            }

            using var link = connection.CreateCommand();
            link.Transaction = transaction;
            link.CommandText = """
                INSERT INTO series_tag (series_id, tag_id, source)
                VALUES ($seriesId, $tagId, 'manual')
                ON CONFLICT(series_id, tag_id) DO UPDATE SET source = 'manual';
                """;
            link.Parameters.AddWithValue("$seriesId", seriesId);
            link.Parameters.AddWithValue("$tagId", tagId);
            await link.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return true;
    }

    private static List<string> NormalizeItems(IReadOnlyList<string> items)
    {
        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string text)
    {
        return text.Trim().ToLowerInvariant();
    }
}
