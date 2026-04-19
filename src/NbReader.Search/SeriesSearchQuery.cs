namespace NbReader.Search;

public enum SeriesSearchReadingStatus
{
    Any,
    Unread,
    InProgress,
    Completed,
}

public enum SeriesSearchSortBy
{
    LatestUpdatedDesc,
    TitleAsc,
}

public sealed record SeriesSearchQuery(
    string? TitleKeyword = null,
    string? AuthorKeyword = null,
    string? TagKeyword = null,
    int? Year = null,
    SeriesSearchReadingStatus ReadingStatus = SeriesSearchReadingStatus.Any,
    SeriesSearchSortBy SortBy = SeriesSearchSortBy.LatestUpdatedDesc,
    int Limit = 200);
