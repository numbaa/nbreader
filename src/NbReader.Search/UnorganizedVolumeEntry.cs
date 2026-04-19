namespace NbReader.Search;

public sealed record UnorganizedVolumeEntry(
    long VolumeId,
    string VolumeTitle,
    string SourcePath,
    DateTimeOffset CreatedAt);
