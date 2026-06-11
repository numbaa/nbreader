namespace NbReader.Core.Models;

/// <summary>
/// ComicInfo.xml 中单页标注信息。
/// </summary>
public class ComicInfoPage
{
    /// <summary>图片文件名（Page 标签的 Image 属性）</summary>
    public string? Image { get; set; }

    /// <summary>页面类型：FrontCover / InnerCover / BackCover / Story / Advertisement / Editorial / Preview / Deleted / Other</summary>
    public string Type { get; set; } = "Story";

    /// <summary>双页的左右标记（Left / Right），可选</summary>
    public string? Key { get; set; }
}
