using System.IO.Compression;
using NbReader.Reader;

namespace NbReader.Import.Tests;

public sealed class UnifiedVolumePageSourceTests
{
    [Fact]
    public void OpenPageStream_ShouldOpenAbsoluteFileLocator()
    {
        var tempDir = CreateTempDir();
        var pagePath = Path.Combine(tempDir, "001.jpg");
        var expected = new byte[] { 1, 2, 3, 4 };

        try
        {
            File.WriteAllBytes(pagePath, expected);

            var source = new UnifiedVolumePageSource();
            using var stream = source.OpenPageStream(sourcePath: "ignored", pageLocator: pagePath);

            Assert.NotNull(stream);
            using var memory = new MemoryStream();
            stream!.CopyTo(memory);
            Assert.Equal(expected, memory.ToArray());
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public void OpenPageStream_ShouldOpenRelativeLocatorFromDirectorySource()
    {
        var tempDir = CreateTempDir();
        var pagesDir = Path.Combine(tempDir, "pages");
        Directory.CreateDirectory(pagesDir);

        var pagePath = Path.Combine(pagesDir, "001.jpg");
        var expected = new byte[] { 9, 8, 7 };

        try
        {
            File.WriteAllBytes(pagePath, expected);

            var source = new UnifiedVolumePageSource();
            using var stream = source.OpenPageStream(sourcePath: tempDir, pageLocator: Path.Combine("pages", "001.jpg"));

            Assert.NotNull(stream);
            using var memory = new MemoryStream();
            stream!.CopyTo(memory);
            Assert.Equal(expected, memory.ToArray());
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public void OpenPageStream_ShouldOpenZipEntry_CaseInsensitiveFallback()
    {
        var tempDir = CreateTempDir();
        var zipPath = Path.Combine(tempDir, "vol.zip");
        var expected = new byte[] { 5, 6, 7, 8 };

        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Images/001.JPG");
                using var entryStream = entry.Open();
                entryStream.Write(expected, 0, expected.Length);
            }

            var source = new UnifiedVolumePageSource();
            using var stream = source.OpenPageStream(sourcePath: zipPath, pageLocator: "images/001.jpg");

            Assert.NotNull(stream);
            using var memory = new MemoryStream();
            stream!.CopyTo(memory);
            Assert.Equal(expected, memory.ToArray());
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public void OpenPageStream_ShouldReturnNull_WhenPageMissing()
    {
        var tempDir = CreateTempDir();

        try
        {
            var source = new UnifiedVolumePageSource();
            using var stream = source.OpenPageStream(sourcePath: tempDir, pageLocator: "missing.jpg");

            Assert.Null(stream);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbreader-page-source-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}