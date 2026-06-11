namespace NbReader.Core.Models;

/// <summary>
/// 从 ComicInfo.xml 解析出的元数据。
/// </summary>
public class ComicInfoData
{
    /// <summary>标题（映射到 ComicResource.Title）</summary>
    public string? Title { get; set; }

    /// <summary>系列名（映射到 Series.Title）</summary>
    public string? Series { get; set; }

    /// <summary>卷号（映射到 ComicResource.VolumeNumber）</summary>
    public int? Number { get; set; }

    /// <summary>简介（映射到 ComicWork.Description）</summary>
    public string? Summary { get; set; }

    /// <summary>分类标签，分号分隔（逐条映射到 Tags）</summary>
    public string? Genre { get; set; }

    /// <summary>语言 ISO 代码（映射到 ComicResource.Language）</summary>
    public string? LanguageISO { get; set; }

    /// <summary>是否为日漫：Yes → "manga"，No → "comic"（映射到 ComicResource.ContentType）</summary>
    public string? Manga { get; set; }

    /// <summary>声明页数（仅做校验参考，以 Zip 实际图片数为准）</summary>
    public int? PageCount { get; set; }

    /// <summary>作者（映射到 Tags，type='artist'）</summary>
    public string? Writer { get; set; }

    /// <summary>画师（映射到 Tags，type='artist'）</summary>
    public string? Penciller { get; set; }

    /// <summary>封面图片文件名（CoverImage 标签）</summary>
    public string? CoverImage { get; set; }

    /// <summary>逐页标注列表（Pages/Page 子元素）</summary>
    public List<ComicInfoPage> Pages { get; set; } = new();

    /// <summary>
    /// 从 Genre 字符串解析出的标签名列表（分号分隔）。
    /// </summary>
    public List<string> GetGenreTags()
    {
        if (string.IsNullOrWhiteSpace(Genre))
            return new List<string>();

        return Genre
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 获取首位作者名（Writer 或 Penciller）。
    /// </summary>
    public string? GetPrimaryArtist()
    {
        return !string.IsNullOrWhiteSpace(Writer) ? Writer.Trim() :
               !string.IsNullOrWhiteSpace(Penciller) ? Penciller.Trim() :
               null;
    }

    /// <summary>
    /// 获取所有作者/画师名列表。
    /// </summary>
    public List<string> GetArtists()
    {
        var artists = new List<string>();
        if (!string.IsNullOrWhiteSpace(Writer))
            artists.AddRange(Writer.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()));
        if (!string.IsNullOrWhiteSpace(Penciller))
            artists.AddRange(Penciller.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()));
        return artists.Distinct().ToList();
    }

    /// <summary>
    /// 查找 FrontCover 标注的页的 Image 文件名。
    /// 优先级：Pages 中的 FrontCover 标注 > CoverImage 属性。
    /// </summary>
    public string? GetCoverImageFileName()
    {
        var frontCover = Pages.FirstOrDefault(p =>
            string.Equals(p.Type, "FrontCover", StringComparison.OrdinalIgnoreCase));
        if (frontCover?.Image is not null)
            return frontCover.Image;

        return CoverImage;
    }

    /// <summary>
    /// 获取需要跳过的页的 Image 文件名列表（Type="Deleted"）。
    /// </summary>
    public HashSet<string> GetDeletedPageImages()
    {
        return Pages
            .Where(p => string.Equals(p.Type, "Deleted", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Image)
            .Where(img => img is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }
}
