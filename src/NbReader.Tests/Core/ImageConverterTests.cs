using Avalonia;
using Avalonia.Headless;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// ImageConverter 单元测试：
/// 验证 SKBitmap → Avalonia Bitmap 转换。
/// </summary>
public class ImageConverterTests : IDisposable
{
    private static bool _initialized;

    public ImageConverterTests()
    {
        if (!_initialized)
        {
            AppBuilder.Configure<Application>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
            _initialized = true;
        }
    }

    public void Dispose() { }

    [Fact]
    public void ToAvaloniaBitmap_WithValidImage_ShouldReturnNonNullBitmap()
    {
        // Arrange
        var bitmap = CreateBgraBitmap(100, 50, SKColors.Orange);
        using var skiaImage = new SkiaImage(bitmap);

        // Act
        var result = NbReader.Converters.ImageConverter.ToAvaloniaBitmap(skiaImage);

        // Assert
        result.Should().NotBeNull();
        result!.Dispose();
    }

    [Fact]
    public void ToAvaloniaBitmap_WithNullImage_ShouldReturnNull()
    {
        // Act
        var result = NbReader.Converters.ImageConverter.ToAvaloniaBitmap(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToAvaloniaBitmap_WithZeroSizeBitmap_ShouldReturnNull()
    {
        // Arrange
        var bitmap = new SKBitmap(0, 0);
        using var skiaImage = new SkiaImage(bitmap);

        // Act
        var result = NbReader.Converters.ImageConverter.ToAvaloniaBitmap(skiaImage);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToAvaloniaBitmap_WithGrayImage_ShouldConvertSuccessfully()
    {
        // Arrange: 灰度图 → 应被正常编解码
        var bitmap = new SKBitmap(40, 40, SKColorType.Gray8, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(100, 100, 100));
        canvas.Flush();
        using var skiaImage = new SkiaImage(bitmap);

        // Act
        var result = NbReader.Converters.ImageConverter.ToAvaloniaBitmap(skiaImage);

        // Assert
        result.Should().NotBeNull();
        result!.Dispose();
    }

    /// <summary>
    /// 辅助方法：创建 BGRA8888 Premul SKBitmap 并填充颜色。
    /// </summary>
    private static SKBitmap CreateBgraBitmap(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        return bitmap;
    }
}
