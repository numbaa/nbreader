namespace NbReader.Core.Models;

/// <summary>
/// 具体漫画资源（书架的原子条目，阅读的最小单位）。
/// </summary>
public class ComicResource
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>所属抽象作品 ID（可为空）</summary>
    public int? WorkId { get; set; }

    /// <summary>完整标题（含版本信息，如 "[Eco Heeky] Ero Biiky [Chinese] [DL版]"）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>来源类型：local / nhentai / ehentai / ...</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>在来源中的唯一标识（local: 绝对路径；nhentai: gallery ID）</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>在线地址（可重新访问）</summary>
    public string? SourceUrl { get; set; }

    /// <summary>封面缩略图本地缓存路径</summary>
    public string? CoverPath { get; set; }

    /// <summary>总页数</summary>
    public int PageCount { get; set; }

    /// <summary>语言：chinese / japanese / english / ...</summary>
    public string? Language { get; set; }

    /// <summary>内容类型：manga / doujinshi / artist cg / ...</summary>
    public string? ContentType { get; set; }

    /// <summary>加入书架时间（ISO 8601）</summary>
    public string AddedDate { get; set; } = string.Empty;

    /// <summary>最后阅读时间（ISO 8601）</summary>
    public string? LastReadDate { get; set; }

    /// <summary>是否收藏（在书架中可见）</summary>
    public bool IsBookmarked { get; set; }

    /// <summary>是否已下载到本地</summary>
    public bool IsDownloaded { get; set; }

    /// <summary>本地文件路径（下载后填充）</summary>
    public string? LocalPath { get; set; }

    /// <summary>所属系列 ID</summary>
    public int? SeriesId { get; set; }

    /// <summary>单行本卷号（如 1, 2, 3）</summary>
    public int? VolumeNumber { get; set; }

    /// <summary>连载话号（如 1, 2, 3）</summary>
    public int? ChapterNumber { get; set; }

    /// <summary>文件指纹（CBZ/CBR: SHA256 前 1MB；文件夹: 元数据哈希；在线未下载: null）</summary>
    public string? FileHash { get; set; }
}
