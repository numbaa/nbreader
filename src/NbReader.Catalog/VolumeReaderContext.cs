namespace NbReader.Catalog;

public sealed record VolumeReaderContext(
    long VolumeId,
    long SeriesId,
    string VolumeTitle,
    string SourcePath,
    IReadOnlyList<string> PageLocators);
