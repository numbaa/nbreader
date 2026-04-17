namespace NbReader.Reader;

public enum ReaderDisplayMode
{
    SinglePage,
    DualPage,
}

public enum ReaderReadingDirection
{
    LeftToRight,
    RightToLeft,
}

public sealed record ReaderSpread(int AnchorPageIndex, int? LeftPageIndex, int? RightPageIndex)
{
    public IReadOnlyList<int> VisiblePageIndices
    {
        get
        {
            var result = new List<int>(capacity: 2);
            if (LeftPageIndex is int left)
            {
                result.Add(left);
            }

            if (RightPageIndex is int right && right != LeftPageIndex)
            {
                result.Add(right);
            }

            return result;
        }
    }
}

public static class ReaderSpreadRules
{
    public static int NormalizeAnchorIndex(int requestedPageIndex, int totalPages, ReaderDisplayMode mode, bool coverSinglePage)
    {
        if (totalPages <= 0)
        {
            return 0;
        }

        var clamped = Math.Clamp(requestedPageIndex, 0, totalPages - 1);
        if (mode == ReaderDisplayMode.SinglePage)
        {
            return clamped;
        }

        if (coverSinglePage && clamped == 0)
        {
            return 0;
        }

        if (coverSinglePage)
        {
            var offset = clamped - 1;
            return (offset / 2) * 2 + 1;
        }

        return (clamped / 2) * 2;
    }

    public static ReaderSpread BuildSpread(
        int anchorPageIndex,
        int totalPages,
        ReaderDisplayMode mode,
        ReaderReadingDirection direction,
        bool coverSinglePage)
    {
        if (totalPages <= 0)
        {
            return new ReaderSpread(0, null, null);
        }

        var normalizedAnchor = NormalizeAnchorIndex(anchorPageIndex, totalPages, mode, coverSinglePage);
        if (mode == ReaderDisplayMode.SinglePage)
        {
            return new ReaderSpread(normalizedAnchor, normalizedAnchor, null);
        }

        if (coverSinglePage && normalizedAnchor == 0)
        {
            return new ReaderSpread(0, null, 0);
        }

        var first = normalizedAnchor;
        var second = first + 1 < totalPages ? first + 1 : (int?)null;

        return direction == ReaderReadingDirection.LeftToRight
            ? new ReaderSpread(normalizedAnchor, first, second)
            : new ReaderSpread(normalizedAnchor, second, first);
    }

    public static int GetInitialAnchorIndex(
        int totalPages,
        ReaderDisplayMode mode,
        ReaderReadingDirection direction,
        bool coverSinglePage)
    {
        if (totalPages <= 0)
        {
            return 0;
        }

        var requested = direction == ReaderReadingDirection.LeftToRight ? 0 : totalPages - 1;
        return NormalizeAnchorIndex(requested, totalPages, mode, coverSinglePage);
    }
}