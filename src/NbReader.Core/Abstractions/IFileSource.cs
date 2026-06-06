namespace NbReader.Core.Abstractions;

/// <summary>
/// 漫画文件源：抽象本地文件、压缩包、在线源等。
/// </summary>
public interface IFileSource : IDisposable
{
    /// <summary>源名称（用于显示）</summary>
    string Name { get; }

    /// <summary>总页数</summary>
    int PageCount { get; }

    /// <summary>获取指定页的图片流</summary>
    Task<Stream> GetPageStreamAsync(int pageIndex);

    /// <summary>获取指定页的缩略图流（可选，用于预览）</summary>
    Task<Stream?> GetThumbnailStreamAsync(int pageIndex);
}
