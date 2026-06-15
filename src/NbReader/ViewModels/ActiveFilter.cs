namespace NbReader.ViewModels;

/// <summary>
/// 活跃的筛选条件，显示为 Chip。
/// </summary>
public class ActiveFilter
{
    /// <summary>筛选类型：language / content_type / tag</summary>
    public string FilterType { get; init; } = string.Empty;

    /// <summary>显示文本</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>筛选值（传给后端查询）</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>标签 ID（仅 tag 类型使用）</summary>
    public int? TagId { get; init; }
}
