namespace NbReader.Search;

public sealed record SeriesSearchResult(
    long SeriesId,
    string Title,
    int VolumeCount,
    DateTimeOffset LatestUpdatedAt,
    int? Year,
    SeriesSearchReadingStatus ReadingStatus);
