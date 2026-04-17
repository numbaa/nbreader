namespace NbReader.Reader;

public sealed class NearbyPageWindowPolicy
{
    public NearbyPageWindowPolicy(int radius)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        Radius = radius;
    }

    public int Radius { get; }

    public IReadOnlySet<int> GetWindowIndices(int currentPageIndex, int totalPages)
    {
        var result = new HashSet<int>();
        if (totalPages <= 0)
        {
            return result;
        }

        var clampedIndex = Math.Clamp(currentPageIndex, 0, totalPages - 1);
        var start = Math.Max(0, clampedIndex - Radius);
        var end = Math.Min(totalPages - 1, clampedIndex + Radius);

        for (var index = start; index <= end; index++)
        {
            result.Add(index);
        }

        return result;
    }
}