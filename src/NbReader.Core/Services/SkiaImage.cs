using NbReader.Core.Abstractions;
using SkiaSharp;

namespace NbReader.Core.Services;

/// <summary>
/// SkiaSharp 位图实现，包装 SKBitmap。
/// </summary>
public class SkiaImage : IImage
{
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public object NativeImage => _bitmap;

    public SKBitmap Bitmap => _bitmap;

    public SkiaImage(SKBitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _bitmap.Dispose();
            _disposed = true;
        }
    }
}
