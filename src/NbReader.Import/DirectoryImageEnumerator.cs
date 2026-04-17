using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NbReader.Import;

public sealed class DirectoryImageEnumerator
{
    public IReadOnlyList<string> Enumerate(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return [];
        }

        var entries = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(ImageFileRules.IsSupportedImagePath)
            .OrderBy(path => Path.GetFileName(path), NaturalStringComparer.Instance)
            .ToArray();

        return entries;
    }
}