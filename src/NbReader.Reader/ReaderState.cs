namespace NbReader.Reader;

public enum ReaderLifecycle
{
    Idle,
    VolumeReady,
    PageLoading,
    PageReady,
    Error,
}

public sealed record ReaderState(
    long VolumeId,
    string VolumeTitle,
    string SourcePath,
    IReadOnlyList<string> PageLocators,
    int CurrentPageIndex,
    ReaderLifecycle Lifecycle,
    string? ErrorMessage)
{
    public static ReaderState Empty { get; } = new(
        0,
        string.Empty,
        string.Empty,
        Array.Empty<string>(),
        0,
        ReaderLifecycle.Idle,
        null);

    public int TotalPages => PageLocators.Count;

    public int CurrentPageNumber => TotalPages == 0 ? 0 : CurrentPageIndex + 1;

    public bool CanMovePrevious => TotalPages > 0 && CurrentPageIndex > 0;

    public bool CanMoveNext => TotalPages > 0 && CurrentPageIndex < TotalPages - 1;
}

public static class ReaderStateMachine
{
    public static ReaderState OpenVolume(long volumeId, string volumeTitle, string sourcePath, IReadOnlyList<string> pageLocators)
    {
        return new ReaderState(
            volumeId,
            volumeTitle,
            sourcePath,
            pageLocators,
            0,
            ReaderLifecycle.VolumeReady,
            null);
    }

    public static ReaderState NavigateTo(ReaderState current, int targetPageIndex)
    {
        if (current.TotalPages == 0)
        {
            return current;
        }

        var nextIndex = Math.Clamp(targetPageIndex, 0, current.TotalPages - 1);
        return current with
        {
            CurrentPageIndex = nextIndex,
            Lifecycle = ReaderLifecycle.PageLoading,
            ErrorMessage = null,
        };
    }

    public static ReaderState MarkPageReady(ReaderState current)
    {
        return current with
        {
            Lifecycle = ReaderLifecycle.PageReady,
            ErrorMessage = null,
        };
    }

    public static ReaderState MarkError(ReaderState current, string errorMessage)
    {
        return current with
        {
            Lifecycle = ReaderLifecycle.Error,
            ErrorMessage = errorMessage,
        };
    }
}