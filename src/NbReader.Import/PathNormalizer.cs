using System;
using System.IO;

namespace NbReader.Import;

public static class PathNormalizer
{
    public static string NormalizeLocator(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ArgumentException("Input path cannot be empty.", nameof(rawPath));
        }

        var fullPath = Path.GetFullPath(rawPath.Trim());
        var normalized = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        normalized = TrimTrailingSeparators(normalized);

        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToLowerInvariant();
        }

        return normalized;
    }

    private static string TrimTrailingSeparators(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var rootLength = root.Length;

        while (fullPath.Length > rootLength && fullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullPath = fullPath[..^1];
        }

        return fullPath;
    }
}
