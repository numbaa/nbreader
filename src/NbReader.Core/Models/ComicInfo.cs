namespace NbReader.Core.Models;

/// <summary>
/// 漫画元数据。
/// </summary>
public class ComicInfo
{
    /// <summary>显示名称</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>作者</summary>
    public string? Author { get; set; }

    /// <summary>文件路径</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>封面缩略图路径（缓存）</summary>
    public string? CoverPath { get; set; }

    /// <summary>总页数</summary>
    public int PageCount { get; set; }

    /// <summary>添加日期</summary>
    public DateTime AddedDate { get; set; } = DateTime.Now;

    /// <summary>最后阅读日期</summary>
    public DateTime? LastReadDate { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }
}
