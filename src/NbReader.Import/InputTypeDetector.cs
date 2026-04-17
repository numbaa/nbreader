using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NbReader.Import;

public static class InputTypeDetector
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
    ];

    public static ImportInputKind Detect(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return ImportInputKind.Unknown;
        }

        if (File.Exists(normalizedPath))
        {
            var extension = Path.GetExtension(normalizedPath);
            return extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                ? ImportInputKind.ZipFile
                : ImportInputKind.Unknown;
        }

        if (!Directory.Exists(normalizedPath))
        {
            return ImportInputKind.Unknown;
        }

        try
        {
            var fileCount = Directory.EnumerateFiles(normalizedPath)
                .Count(filePath => IsImageFile(filePath));
            var dirCount = Directory.EnumerateDirectories(normalizedPath).Count();

            if (fileCount > 0 && dirCount == 0)
            {
                return ImportInputKind.ImageDirectory;
            }

            if (fileCount == 0 && dirCount > 0)
            {
                return ImportInputKind.SeriesDirectory;
            }

            return ImportInputKind.Unknown;
        }
        catch (IOException)
        {
            return ImportInputKind.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return ImportInputKind.Unknown;
        }
    }

    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return ImageExtensions.Contains(extension);
    }
}
