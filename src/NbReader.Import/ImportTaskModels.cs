using System;

namespace NbReader.Import;

public enum ImportTaskStatus
{
    Pending,
    Scanning,
    Analyzing,
    AwaitingConfirmation,
    Importing,
    PostProcessing,
    Completed,
    Failed,
    Canceled,
}

public enum ImportInputKind
{
    Unknown,
    ZipFile,
    ImageDirectory,
    SeriesDirectory,
}

public sealed record ImportTask(
    Guid TaskId,
    string RawInput,
    string NormalizedLocator,
    ImportInputKind InputKind,
    ImportTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ImportTaskEvent(
    Guid TaskId,
    ImportTaskStatus Status,
    string EventType,
    string? Message,
    DateTimeOffset OccurredAt);
