using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NbReader.Core.Abstractions;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Converters;

/// <summary>
/// IImage → Avalonia Bitmap 转换工具。
/// </summary>
public static class ImageConverter
{
    /// <summary>
    /// 将 Core 层 IImage 转换为 Avalonia 可渲染的 Bitmap。
    /// </summary>
    public static Bitmap? ToAvaloniaBitmap(IImage? image)
    {
        if (image is not SkiaImage skiaImage)
            return null;

        var skBitmap = skiaImage.Bitmap;
        if (skBitmap.Handle == IntPtr.Zero || skBitmap.Width == 0 || skBitmap.Height == 0)
            return null;

        // 编码为 PNG 流，再用 Avalonia 原生方式解码
        using var skImage = SKImage.FromBitmap(skBitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 90);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;

        return new Bitmap(stream);
    }
}
