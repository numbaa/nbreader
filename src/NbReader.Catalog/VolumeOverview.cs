namespace NbReader.Catalog;

public sealed record VolumeOverview(
    long VolumeId,
    long SeriesId,
    string Title,
    int PageCount,
    DateTimeOffset CreatedAt);
