using System.IO.Compression;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// FileSourceFactory 单元测试。
/// </summary>
public class FileSourceFactoryTests : IDisposable
{
    private readonly string _tempDir;

    public FileSourceFactoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NbReader_Factory_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static void CreateTestImage(string filePath)
    {
        using var bitmap = new SKBitmap(16, 16, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Green);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var fs = File.Create(filePath);
        data.SaveTo(fs);
    }

    [Fact]
    public void Create_WithDirectoryPath_ShouldReturnDirectoryFileSource()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "page.png"));

        // Act
        using var source = FileSourceFactory.Create(_tempDir);

        // Assert
        source.Should().BeOfType<DirectoryFileSource>();
        source.PageCount.Should().Be(1);
    }

    [Fact]
    public void Create_WithCbzPath_ShouldReturnCbzFileSource()
    {
        // Arrange
        var cbzPath = Path.Combine(_tempDir, "comic.cbz");
        using (var fs = File.Create(cbzPath))
        using (var archive = new System.IO.Compression.ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("page001.png", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            CreateTestImageData(entryStream);
        }

        // Act
        using var source = FileSourceFactory.Create(cbzPath);

        // Assert
        source.Should().BeOfType<CbzFileSource>();
    }

    [Fact]
    public void Create_WithZipPath_ShouldReturnCbzFileSource()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "comic.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new System.IO.Compression.ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("page001.png", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            CreateTestImageData(entryStream);
        }

        // Act
        using var source = FileSourceFactory.Create(zipPath);

        // Assert
        source.Should().BeOfType<CbzFileSource>();
    }

    [Fact]
    public void Create_WithUnsupportedExtension_ShouldThrow()
    {
        // Arrange
        var txtPath = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(txtPath, "hello");

        // Act
        Action act = () => FileSourceFactory.Create(txtPath);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Create_WithNonExistentPath_ShouldThrow()
    {
        // Act
        Action act = () => FileSourceFactory.Create(Path.Combine(_tempDir, "nonexistent.cbz"));

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Create_WithNullPath_ShouldThrow()
    {
        // Act
        Action act = () => FileSourceFactory.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyPath_ShouldThrow()
    {
        // Act
        Action act = () => FileSourceFactory.Create("  ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static void CreateTestImageData(Stream stream)
    {
        using var bitmap = new SKBitmap(16, 16, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        data.SaveTo(stream);
    }
}
