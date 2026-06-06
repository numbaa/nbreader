namespace NbReader.Core.Abstractions;

/// <summary>
/// 跨平台位图抽象（解耦具体图片库）。
/// </summary>
public interface IImage : IDisposable
{
    int Width { get; }
    int Height { get; }
    object NativeImage { get; }
}

/// <summary>
/// 图片加载器：将流解码为可渲染的位图对象。
/// </summary>
public interface IImageLoader
{
    /// <summary>从流加载图片</summary>
    Task<IImage> LoadAsync(Stream stream);

    /// <summary>从文件加载图片</summary>
    Task<IImage> LoadAsync(string filePath);
}
