namespace NbReader.Core.Abstractions;

/// <summary>
/// 压缩包条目信息。
/// </summary>
public record ArchiveEntry(string Name, long Size, bool IsDirectory);

/// <summary>
/// 压缩包服务：处理 ZIP/RAR 等格式的解压。
/// </summary>
public interface IArchiveService
{
    /// <summary>支持的扩展名</summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>判断是否支持该文件</summary>
    bool CanOpen(string filePath);

    /// <summary>列出所有条目</summary>
    Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(string filePath);

    /// <summary>提取指定条目</summary>
    Task<Stream> ExtractEntryAsync(string filePath, string entryName);
}
