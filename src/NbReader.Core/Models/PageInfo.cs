namespace NbReader.Core.Models;

/// <summary>
/// 单页信息。
/// </summary>
public class PageInfo
{
    /// <summary>页码（0-based）</summary>
    public int Index { get; set; }

    /// <summary>条目名称（压缩包中的路径）</summary>
    public string EntryName { get; set; } = string.Empty;

    /// <summary>图片原始宽度</summary>
    public int? Width { get; set; }

    /// <summary>图片原始高度</summary>
    public int? Height { get; set; }
}
