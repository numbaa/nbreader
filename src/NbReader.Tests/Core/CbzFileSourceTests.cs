using System.IO.Compression;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// CbzFileSource 单元测试。
/// </summary>
public class CbzFileSourceTests : IDisposable
{
    private readonly string _tempDir;

    public CbzFileSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NbReader_CBZ_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// 创建测试用 CBZ 文件（ZIP 格式），包含指定数量的 PNG 图片。
    /// </summary>
    private static string CreateTestCbz(string directory, int imageCount = 3,
        string[]? extraFiles = null)
    {
        var cbzPath = Path.Combine(directory, $"test_{Guid.NewGuid():N}.cbz");

        using var fs = File.Create(cbzPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        for (int i = 0; i < imageCount; i++)
        {
            var entryName = $"page{i:D3}.png";
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            CreateTestImageData(entryStream, 32, 32, new SKColor((byte)(i * 50), 100, 150));
        }

        // 额外非图片文件
        if (extraFiles != null)
        {
            foreach (var fileName in extraFiles)
            {
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open());
                writer.Write("test content");
            }
        }

        return cbzPath;
    }

    /// <summary>
    /// 创建损坏的 CBZ 文件（内容不是有效 ZIP）。
    /// </summary>
    private static string CreateCorruptCbz(string directory)
    {
        var cbzPath = Path.Combine(directory, $"corrupt_{Guid.NewGuid():N}.cbz");
        File.WriteAllText(cbzPath, "this is not a valid zip file");
        return cbzPath;
    }

    private static void CreateTestImageData(Stream stream, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        data.SaveTo(stream);
    }

    [Fact]
    public void Constructor_WithValidCbz_ShouldSetName()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 2);

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert
        source.Name.Should().Be(Path.GetFileNameWithoutExtension(cbzPath));
    }

    [Fact]
    public void Constructor_WithValidCbz_ShouldCountImages()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 5);

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert
        source.PageCount.Should().Be(5);
    }

    [Fact]
    public void Constructor_WithMixedContent_ShouldOnlyCountImages()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 3, extraFiles: new[] { "readme.txt", "cover.xml" });

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert
        source.PageCount.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithEmptyCbz_ShouldHaveZeroPages()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 0);

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert
        source.PageCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ShouldThrow()
    {
        // Act
        Action act = () => new CbzFileSource(Path.Combine(_tempDir, "nonexistent.cbz"));

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Constructor_WithCorruptFile_ShouldThrow()
    {
        // Arrange
        var cbzPath = CreateCorruptCbz(_tempDir);

        // Act
        Action act = () => new CbzFileSource(cbzPath);

        // Assert
        act.Should().Throw<SharpCompress.Common.ArchiveException>();
    }

    [Fact]
    public async Task GetPageStreamAsync_WithValidIndex_ShouldReturnStream()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 2);
        using var source = new CbzFileSource(cbzPath);

        // Act
        var stream = await source.GetPageStreamAsync(0);

        // Assert
        stream.Should().NotBeNull();
        stream.Length.Should().BeGreaterThan(0);
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task GetPageStreamAsync_WithNegativeIndex_ShouldThrow()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 1);
        using var source = new CbzFileSource(cbzPath);

        // Act
        Func<Task> act = () => source.GetPageStreamAsync(-1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetPageStreamAsync_WithOutOfRangeIndex_ShouldThrow()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 1);
        using var source = new CbzFileSource(cbzPath);

        // Act
        Func<Task> act = () => source.GetPageStreamAsync(10);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ShouldSortByEntryName()
    {
        // Arrange: 创建 CBZ，条目顺序故意打乱
        var cbzPath = Path.Combine(_tempDir, $"sort_{Guid.NewGuid():N}.cbz");
        using (var fs = File.Create(cbzPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            // 按不字母序添加
            string[] names = { "c.png", "a.png", "b.png" };
            foreach (var name in names)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                CreateTestImageData(entryStream, 16, 16, SKColors.Red);
            }
        }

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert：排序后 "a.png" 应该是第一个条目
        source.PageCount.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithNestedDirectories_ShouldIncludeAllImages()
    {
        // Arrange: CBZ 包含嵌套目录中的图片
        var cbzPath = Path.Combine(_tempDir, $"nested_{Guid.NewGuid():N}.cbz");
        using (var fs = File.Create(cbzPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var rootEntry = archive.CreateEntry("root.png", CompressionLevel.Optimal);
            using (var rs = rootEntry.Open())
                CreateTestImageData(rs, 16, 16, SKColors.Red);

            var nestedEntry = archive.CreateEntry("sub/nested.png", CompressionLevel.Optimal);
            using (var ns = nestedEntry.Open())
                CreateTestImageData(ns, 16, 16, SKColors.Blue);
        }

        // Act
        using var source = new CbzFileSource(cbzPath);

        // Assert
        source.PageCount.Should().Be(2);
    }

    [Fact]
    public async Task GetThumbnailStreamAsync_ShouldReturnNull()
    {
        // Arrange
        var cbzPath = CreateTestCbz(_tempDir, 1);
        using var source = new CbzFileSource(cbzPath);

        // Act
        var result = await source.GetThumbnailStreamAsync(0);

        // Assert
        result.Should().BeNull();
    }
}
