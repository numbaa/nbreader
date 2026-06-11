namespace NbReader.Core.Models;

/// <summary>
/// 阅读历史记录（每次打开漫画追加一条，保留全部时间线）。
/// </summary>
public class ReadHistoryEntry
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>关联的资源 ID</summary>
    public int ResourceId { get; set; }

    /// <summary>最后阅读的页码（0-based）</summary>
    public int LastPageIndex { get; set; }

    /// <summary>阅读时间（ISO 8601）</summary>
    public string ReadDate { get; set; } = string.Empty;

    // ── 以下由 JOIN 填充，不持久化 ──

    /// <summary>漫画标题</summary>
    public string? ComicTitle { get; set; }

    /// <summary>封面路径</summary>
    public string? CoverPath { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages { get; set; }
}
