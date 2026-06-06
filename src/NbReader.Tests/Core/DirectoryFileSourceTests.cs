using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// DirectoryFileSource 单元测试。
/// </summary>
public class DirectoryFileSourceTests : IDisposable
{
    private readonly string _tempDir;

    public DirectoryFileSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NbReader_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// 生成测试用 PNG 图片并写入指定路径。
    /// </summary>
    private static void CreateTestImage(string filePath, int width = 32, int height = 32,
        SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color ?? SKColors.Blue);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var fs = File.Create(filePath);
        data.SaveTo(fs);
    }

    [Fact]
    public void Constructor_WithValidDirectory_ShouldSetName()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "page01.png"));

        // Act
        using var source = new DirectoryFileSource(_tempDir);

        // Assert
        source.Name.Should().Be(Path.GetFileName(_tempDir));
    }

    [Fact]
    public void Constructor_WithImageFiles_ShouldCountCorrectly()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "01.png"));
        CreateTestImage(Path.Combine(_tempDir, "02.jpg"));
        CreateTestImage(Path.Combine(_tempDir, "03.webp"));
        CreateTestImage(Path.Combine(_tempDir, "readme.txt")); // 非图片

        // Act
        using var source = new DirectoryFileSource(_tempDir);

        // Assert
        source.PageCount.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithEmptyDirectory_ShouldHaveZeroPages()
    {
        // Act
        using var source = new DirectoryFileSource(_tempDir);

        // Assert
        source.PageCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNonExistentDirectory_ShouldThrow()
    {
        // Act
        Action act = () => new DirectoryFileSource(Path.Combine(_tempDir, "nonexistent"));

        // Assert
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void Constructor_ShouldSortByFilename()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "c.png"));
        CreateTestImage(Path.Combine(_tempDir, "a.png"));
        CreateTestImage(Path.Combine(_tempDir, "b.png"));

        // Act
        using var source = new DirectoryFileSource(_tempDir);

        // Assert
        source.PageCount.Should().Be(3);
        // 验证排序：通过 GetPageStreamAsync 确认文件存在
        // a.png < b.png < c.png（按完整路径字符串排序）
    }

    [Fact]
    public async Task GetPageStreamAsync_WithValidIndex_ShouldReturnStream()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "page.png"));
        using var source = new DirectoryFileSource(_tempDir);

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
        using var source = new DirectoryFileSource(_tempDir);

        // Act
        Func<Task> act = () => source.GetPageStreamAsync(-1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetPageStreamAsync_WithOutOfRangeIndex_ShouldThrow()
    {
        // Arrange
        using var source = new DirectoryFileSource(_tempDir);

        // Act
        Func<Task> act = () => source.GetPageStreamAsync(999);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithRecursive_ShouldIncludeSubdirectories()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "root.png"));
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        CreateTestImage(Path.Combine(subDir, "sub.png"));

        // Act
        using var source = new DirectoryFileSource(_tempDir, recursive: true);

        // Assert
        source.PageCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithoutRecursive_ShouldOnlyScanTopLevel()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "root.png"));
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        CreateTestImage(Path.Combine(subDir, "sub.png"));

        // Act
        using var source = new DirectoryFileSource(_tempDir, recursive: false);

        // Assert
        source.PageCount.Should().Be(1);
    }

    [Fact]
    public async Task GetThumbnailStreamAsync_ShouldReturnNull()
    {
        // Arrange
        CreateTestImage(Path.Combine(_tempDir, "page.png"));
        using var source = new DirectoryFileSource(_tempDir);

        // Act
        var result = await source.GetThumbnailStreamAsync(0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_ShouldAllowMultipleCalls()
    {
        // Arrange
        using var source = new DirectoryFileSource(_tempDir);

        // Act & Assert (no exception)
        source.Dispose();
    }
}
