using System.IO.Compression;

namespace NbReader.Reader;

public sealed class UnifiedVolumePageSource
{
    public Stream? OpenPageStream(string sourcePath, string pageLocator)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(pageLocator))
        {
            return null;
        }

        // Directory volumes usually store absolute locators; zip volumes store entry paths.
        if (File.Exists(pageLocator))
        {
            return File.OpenRead(pageLocator);
        }

        if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return OpenZipEntryAsMemoryStream(sourcePath, pageLocator);
        }

        if (Directory.Exists(sourcePath) && !Path.IsPathRooted(pageLocator))
        {
            var combined = Path.Combine(sourcePath, pageLocator);
            if (File.Exists(combined))
            {
                return File.OpenRead(combined);
            }
        }

        return null;
    }

    private static Stream? OpenZipEntryAsMemoryStream(string zipFilePath, string pageLocator)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);

        var entry = archive.GetEntry(pageLocator)
            ?? archive.Entries.FirstOrDefault(x => string.Equals(x.FullName, pageLocator, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var memory = new MemoryStream();
        using var entryStream = entry.Open();
        entryStream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
