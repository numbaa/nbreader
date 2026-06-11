namespace NbReader.Core.Models;

/// <summary>
/// 标签/作者多语言别名（Canonical + Alias 双层映射）。
/// </summary>
public class EntityAlias
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>关联的标签 ID</summary>
    public int TagId { get; set; }

    /// <summary>别名（如 "巨乳"、"大きな胸"）</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>语言：zh / ja / en / ...</summary>
    public string? Language { get; set; }

    /// <summary>限定来源，NULL = 通用</summary>
    public string? SourceType { get; set; }
}
