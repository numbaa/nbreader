namespace NbReader.Core.Models;

/// <summary>
/// 用户自定义分类（主观分组，如"正在追""已读完"）。
/// </summary>
public class Category
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>分类名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>排序权重（用户可拖拽排序）</summary>
    public int SortOrder { get; set; }

    /// <summary>该分类下的漫画数量（由查询填充，不持久化）</summary>
    public int ResourceCount { get; set; }
}
