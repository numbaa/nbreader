namespace NbReader.Catalog;

public sealed record VolumeOverview(
    long VolumeId,
    long SeriesId,
    string Title,
    int? VolumeNumber,
    int PageCount,
    DateTimeOffset CreatedAt);
