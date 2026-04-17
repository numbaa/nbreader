using System;
using System.IO;

namespace NbReader.Import.Tests;

public class PathNormalizerTests
{
    [Fact]
    public void NormalizeLocator_ShouldTrimTrailingSeparators()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var withSeparator = root + Path.DirectorySeparatorChar;
            var normalizedA = PathNormalizer.NormalizeLocator(root);
            var normalizedB = PathNormalizer.NormalizeLocator(withSeparator);

            Assert.Equal(normalizedA, normalizedB);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NormalizeLocator_ShouldNormalizeWindowsCase()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "nbreader-case-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var upper = root.ToUpperInvariant();
            var lower = root.ToLowerInvariant();

            var normalizedUpper = PathNormalizer.NormalizeLocator(upper);
            var normalizedLower = PathNormalizer.NormalizeLocator(lower);

            Assert.Equal(normalizedLower, normalizedUpper);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
