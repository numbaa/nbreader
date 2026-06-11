namespace NbReader.Core.Models;

/// <summary>
/// 阅读进度（每资源仅最新一条，用于断点续读）。
/// </summary>
public class ReadingProgress
{
    /// <summary>关联的资源 ID（主键）</summary>
    public int ResourceId { get; set; }

    /// <summary>当前页码（0-based）</summary>
    public int CurrentPage { get; set; }

    /// <summary>最后阅读时间（ISO 8601）</summary>
    public string LastReadTime { get; set; } = string.Empty;

    /// <summary>总页数（由查询填充，不持久化到 reading_progress 表）</summary>
    public int TotalPages { get; set; }

    /// <summary>阅读进度百分比（0-100）</summary>
    public double ProgressPercent => TotalPages > 0 ? (double)(CurrentPage + 1) / TotalPages * 100.0 : 0;
}
