namespace NbReader.Search;

public sealed record FailedImportTaskEntry(
    Guid TaskId,
    string RawInput,
    string NormalizedLocator,
    string InputKind,
    string Status,
    DateTimeOffset UpdatedAt,
    string? LastErrorMessage);
