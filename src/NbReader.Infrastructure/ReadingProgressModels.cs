namespace NbReader.Infrastructure;

public sealed record ReadingProgressSnapshot(
    long VolumeId,
    int CurrentPageIndex,
    int MaxPageReached,
    bool Completed,
    DateTimeOffset LastReadAt,
    string? ReadingMode,
    string? ReadingDirection,
    DateTimeOffset UpdatedAt);

public sealed record RecentReadingEntry(
    long VolumeId,
    long SeriesId,
    string SeriesTitle,
    string VolumeTitle,
    int CurrentPageIndex,
    int PageCount,
    bool Completed,
    DateTimeOffset LastReadAt);
