using System;
using System.Collections.Generic;
using System.IO;

namespace NbReader.Import;

public static class ImageFileRules
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

    public static bool IsSupportedImagePath(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return IsSupportedImageExtension(extension);
    }

    public static bool IsSupportedImageExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return ImageExtensions.Contains(extension);
    }
}