using NbReader.Core.Abstractions;

namespace NbReader.Core.Services;

/// <summary>
/// 图片文件夹文件源：扫描文件夹中的图片文件，按文件名排序。
/// </summary>
public class DirectoryFileSource : IFileSource
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    private readonly List<string> _imagePaths;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public int PageCount => _imagePaths.Count;

    /// <summary>
    /// 从指定文件夹创建文件源。
    /// </summary>
    /// <param name="directoryPath">文件夹路径。</param>
    /// <param name="recursive">是否递归扫描子文件夹。</param>
    /// <exception cref="DirectoryNotFoundException">当文件夹不存在时抛出。</exception>
    public DirectoryFileSource(string directoryPath, bool recursive = false)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");

        Name = Path.GetFileName(directoryPath);

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        _imagePaths = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public Task<Stream> GetPageStreamAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _imagePaths.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        return Task.FromResult<Stream>(File.OpenRead(_imagePaths[pageIndex]));
    }

    /// <inheritdoc/>
    public Task<Stream?> GetThumbnailStreamAsync(int pageIndex)
    {
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _imagePaths.Clear();
    }
}
