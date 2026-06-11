namespace NbReader.Core.Models;

/// <summary>
/// 标签（全局去重，英文规范名）。作者也是 tag（type='artist'）。
/// </summary>
public class Tag
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>英文规范名（如 "big breasts"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>标签类型：general / artist / character / parody / language / category</summary>
    public string Type { get; set; } = "general";

    /// <summary>是否待人工确认</summary>
    public bool NeedsReview { get; set; }
}
