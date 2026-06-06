using NbReader.Core.Abstractions;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// SkiaImageLoader 单元测试。
/// 使用程序生成的测试图片，无需外部文件。
/// </summary>
public class SkiaImageLoaderTests
{
    private readonly SkiaImageLoader _loader = new();

    /// <summary>
    /// 生成测试用图片流。
    /// </summary>
    private static Stream CreateTestImageStream(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        // 绘制一个十字线方便验证渲染
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = false
        };
        canvas.DrawLine(0, height / 2f, width, height / 2f, paint);
        canvas.DrawLine(width / 2f, 0, width / 2f, height, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task LoadAsync_FromStream_ShouldReturnValidImage()
    {
        // Arrange
        using var stream = CreateTestImageStream(200, 100, SKColors.CornflowerBlue);

        // Act
        using var image = await _loader.LoadAsync(stream);

        // Assert
        image.Should().NotBeNull();
        image.Width.Should().Be(200);
        image.Height.Should().Be(100);
        image.NativeImage.Should().BeOfType<SKBitmap>();
    }

    [Fact]
    public async Task LoadAsync_FromFile_ShouldReturnValidImage()
    {
        // Arrange
        var tmpFile = Path.GetTempFileName() + ".png";
        try
        {
            using (var stream = CreateTestImageStream(64, 64, SKColors.Red))
            using (var fileStream = File.Create(tmpFile))
            {
                stream.CopyTo(fileStream);
            }

            // Act
            using var image = await _loader.LoadAsync(tmpFile);

            // Assert
            image.Should().NotBeNull();
            image.Width.Should().Be(64);
            image.Height.Should().Be(64);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task LoadAsync_FromStream_ShouldSupportMultipleFormats()
    {
        // Test PNG (already tested)

        // Test JPEG
        using var bitmap = new SKBitmap(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Green);
        canvas.Flush();
        using var skImage = SKImage.FromBitmap(bitmap);
        using var jpegData = skImage.Encode(SKEncodedImageFormat.Jpeg, 85);
        var jpegStream = new MemoryStream();
        jpegData.SaveTo(jpegStream);
        jpegStream.Position = 0;

        using var image = await _loader.LoadAsync(jpegStream);
        image.Width.Should().Be(32);
        image.Height.Should().Be(32);
    }

    [Fact]
    public async Task LoadAsync_FromStream_WithGrayscaleImage_ShouldConvertToBgra()
    {
        // Arrange: 创建灰度图
        using var grayBitmap = new SKBitmap(50, 50, SKColorType.Gray8, SKAlphaType.Opaque);
        using var grayCanvas = new SKCanvas(grayBitmap);
        grayCanvas.Clear(new SKColor(128, 128, 128));
        grayCanvas.Flush();
        using var grayImage = SKImage.FromBitmap(grayBitmap);
        using var grayData = grayImage.Encode(SKEncodedImageFormat.Png, 80);
        var stream = new MemoryStream();
        grayData.SaveTo(stream);
        stream.Position = 0;

        // Act
        using var image = await _loader.LoadAsync(stream);

        // Assert: 应被转为 Bgra8888
        image.Width.Should().Be(50);
        image.Height.Should().Be(50);
        var skBitmap = (SKBitmap)image.NativeImage;
        skBitmap.ColorType.Should().Be(SKColorType.Bgra8888);
    }

    [Fact]
    public void LoadAsync_NullStream_ShouldThrow()
    {
        // Act
        Func<Task> act = () => _loader.LoadAsync((Stream)null!);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void LoadAsync_NonExistentFile_ShouldThrow()
    {
        // Act
        Func<Task> act = () => _loader.LoadAsync("Z:\\nonexistent\\file.png");

        // Assert
        act.Should().ThrowAsync<FileNotFoundException>();
    }
}
