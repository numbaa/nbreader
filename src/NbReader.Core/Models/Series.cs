namespace NbReader.Core.Models;

/// <summary>
/// 系列（多卷/多话聚合。作者走 tags 表，不存储在 Series 上）。
/// </summary>
public class Series
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>系列名（如 "One Piece"）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>简介</summary>
    public string? Description { get; set; }
}
