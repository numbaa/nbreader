using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NbReader.Import.Tests;

public class ImageEnumerationTests
{
    [Fact]
    public void DirectoryImageEnumerator_ShouldFilterAndSortNaturally()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-enum-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "10.jpg"), "a");
            File.WriteAllText(Path.Combine(root, "2.jpg"), "b");
            File.WriteAllText(Path.Combine(root, "1.jpg"), "c");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "ignored");

            var enumerator = new DirectoryImageEnumerator();
            var pages = enumerator.Enumerate(root)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], pages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZipImageEnumerator_ShouldFilterAndSortNaturally()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-enum-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var zipPath = Path.Combine(root, "sample.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                CreateEntry(zip, "10.jpg", "a");
                CreateEntry(zip, "2.jpg", "b");
                CreateEntry(zip, "1.jpg", "c");
                CreateEntry(zip, "notes.txt", "ignored");
            }

            var enumerator = new ZipImageEnumerator();
            var pages = enumerator.Enumerate(zipPath)
                .Select(entry => entry.EntryPath)
                .ToArray();

            Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], pages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
