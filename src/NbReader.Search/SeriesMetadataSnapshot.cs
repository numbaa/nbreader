namespace NbReader.Search;

public sealed record SeriesMetadataSnapshot(
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> Tags);
