using NbReader.Core.Abstractions;
using SkiaSharp;

namespace NbReader.Core.Services;

/// <summary>
/// SkiaSharp 图片加载器：支持 JPG/PNG/BMP/GIF/WebP 等格式。
/// </summary>
public class SkiaImageLoader : IImageLoader
{
    /// <summary>
    /// 支持的图片扩展名。
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".ico"
    };

    /// <inheritdoc />
    public Task<IImage> LoadAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Task.Run(() => LoadFromStream(stream));
    }

    /// <inheritdoc />
    public Task<IImage> LoadAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("图片文件不存在。", filePath);
        return Task.Run(() => LoadFromFile(filePath));
    }

    private static IImage LoadFromStream(Stream stream)
    {
        var bitmap = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("无法解码图片流。");

        if (bitmap.ColorType == SKColorType.Gray8)
        {
            // 灰度图转为 BGRA 以便 Avalonia 渲染
            var converted = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            bitmap.CopyTo(converted, SKColorType.Bgra8888);
            bitmap.Dispose();
            bitmap = converted;
        }

        return new SkiaImage(bitmap);
    }

    private static IImage LoadFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return LoadFromStream(stream);
    }
}
