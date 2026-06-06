namespace NbReader.Core.Models;

/// <summary>
/// 阅读进度。
/// </summary>
public class ReadingProgress
{
    /// <summary>漫画文件路径</summary>
    public string ComicPath { get; set; } = string.Empty;

    /// <summary>当前页码（0-based）</summary>
    public int CurrentPage { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages { get; set; }

    /// <summary>最后阅读时间</summary>
    public DateTime LastReadTime { get; set; } = DateTime.Now;

    /// <summary>阅读进度百分比（0-100）</summary>
    public double ProgressPercent => TotalPages > 0 ? (double)(CurrentPage + 1) / TotalPages * 100.0 : 0;
}
