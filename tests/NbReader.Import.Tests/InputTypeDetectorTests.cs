using System;
using System.IO;

namespace NbReader.Import.Tests;

public class InputTypeDetectorTests
{
    [Fact]
    public void Detect_ShouldReturnZipFile_ForZipPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-kind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var zipPath = Path.Combine(root, "sample.zip");
            File.WriteAllText(zipPath, "zip-placeholder");

            var kind = InputTypeDetector.Detect(PathNormalizer.NormalizeLocator(zipPath));

            Assert.Equal(ImportInputKind.ZipFile, kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Detect_ShouldReturnImageDirectory_WhenDirectoryContainsImagesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-kind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "001.jpg"), "a");
            File.WriteAllText(Path.Combine(root, "002.png"), "b");

            var kind = InputTypeDetector.Detect(PathNormalizer.NormalizeLocator(root));

            Assert.Equal(ImportInputKind.ImageDirectory, kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Detect_ShouldReturnSeriesDirectory_WhenDirectoryContainsOnlySubDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-kind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Vol.01"));
            Directory.CreateDirectory(Path.Combine(root, "Vol.02"));

            var kind = InputTypeDetector.Detect(PathNormalizer.NormalizeLocator(root));

            Assert.Equal(ImportInputKind.SeriesDirectory, kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
