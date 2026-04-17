namespace NbReader.Catalog;

public sealed record SeriesOverview(
    long SeriesId,
    string Title,
    int VolumeCount,
    DateTimeOffset LatestUpdatedAt);
