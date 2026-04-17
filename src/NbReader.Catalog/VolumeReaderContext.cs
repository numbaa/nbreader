namespace NbReader.Catalog;

public sealed record VolumeReaderContext(
    long VolumeId,
    string VolumeTitle,
    string SourcePath,
    IReadOnlyList<string> PageLocators);
