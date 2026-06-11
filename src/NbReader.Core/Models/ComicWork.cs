namespace NbReader.Core.Models;

/// <summary>
/// 抽象作品（同一本漫画，跨源/跨版本聚合）。
/// </summary>
public class ComicWork
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>规范化标题（去作者/团体前缀）</summary>
    public string CanonicalTitle { get; set; } = string.Empty;

    /// <summary>简介</summary>
    public string? Description { get; set; }
}
