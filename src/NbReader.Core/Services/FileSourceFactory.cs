using NbReader.Core.Abstractions;

namespace NbReader.Core.Services;

/// <summary>
/// 文件源工厂：根据路径自动选择合适的 IFileSource 实现。
/// </summary>
public static class FileSourceFactory
{
    /// <summary>
    /// 根据路径创建对应的文件源。
    /// </summary>
    /// <param name="path">文件或文件夹路径。</param>
    /// <returns>对应的 IFileSource 实例。</returns>
    /// <exception cref="ArgumentException">当路径为空时抛出。</exception>
    /// <exception cref="FileNotFoundException">当路径不存在时抛出。</exception>
    /// <exception cref="NotSupportedException">当文件格式不支持时抛出。</exception>
    public static IFileSource Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        // 目录：按图片文件夹处理
        if (Directory.Exists(path))
            return new DirectoryFileSource(path);

        // 文件：按扩展名路由
        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".cbz" or ".zip" => new CbzFileSource(path),
                ".cbr" => new CbrFileSource(path),
                _ => throw new NotSupportedException($"不支持的文件格式: {ext}")
            };
        }

        throw new FileNotFoundException($"路径不存在: {path}");
    }
}
