using NbReader.Core.Abstractions;
using SharpCompress.Archives.Rar;

namespace NbReader.Core.Services;

/// <summary>
/// CBR 文件源：将 RAR 压缩包中的图片条目作为漫画页。
/// </summary>
public class CbrFileSource : IFileSource
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    private readonly List<byte[]> _pages = new();

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public int PageCount => _pages.Count;

    /// <summary>
    /// 从 CBR（RAR）文件创建文件源。所有图片条目在构造时解压到内存。
    /// </summary>
    /// <param name="filePath">CBR 文件路径。</param>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出。</exception>
    /// <exception cref="InvalidOperationException">当压缩包损坏或格式无效时抛出。</exception>
    public CbrFileSource(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件不存在: {filePath}", filePath);

        Name = Path.GetFileNameWithoutExtension(filePath);

        using var archive = RarArchive.Open(filePath);

        var imageEntries = archive.Entries
            .Where(e => !e.IsDirectory)
            .Where(e =>
            {
                var ext = Path.GetExtension(e.Key);
                return ext != null && ImageExtensions.Contains(ext);
            })
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in imageEntries)
        {
            using var entryStream = entry.OpenEntryStream();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            _pages.Add(ms.ToArray());
        }
    }

    /// <inheritdoc/>
    public Task<Stream> GetPageStreamAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        return Task.FromResult<Stream>(new MemoryStream(_pages[pageIndex]));
    }

    /// <inheritdoc/>
    public Task<Stream?> GetThumbnailStreamAsync(int pageIndex)
    {
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _pages.Clear();
    }
}
