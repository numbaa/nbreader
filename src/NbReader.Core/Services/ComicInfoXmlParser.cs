using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;

namespace NbReader.Core.Services;

/// <summary>
/// ComicInfo.xml 解析器：从 CBZ 内提取并解析 ComicRack 标准元数据。
/// </summary>
public class ComicInfoXmlParser : IComicInfoParser
{
    // ComicInfo.xml 在 ZIP 内的标准路径
    private const string ComicInfoEntryName = "ComicInfo.xml";

    /// <summary>
    /// 从 XML 流解析 ComicInfo。
    /// </summary>
    public ComicInfoData Parse(Stream xmlStream)
    {
        var data = new ComicInfoData();

        using var reader = new StreamReader(xmlStream, Encoding.UTF8);
        var doc = XDocument.Load(reader);
        var root = doc.Root;
        if (root is null)
            return data;

        // 直接子元素（忽略命名空间前缀差异）
        data.Title = GetElementValue(root, "Title");
        data.Series = GetElementValue(root, "Series");
        data.Number = GetElementValueAsInt(root, "Number");
        data.Summary = GetElementValue(root, "Summary");
        data.Genre = GetElementValue(root, "Genre");
        data.LanguageISO = GetElementValue(root, "LanguageISO");
        data.Manga = GetElementValue(root, "Manga");
        data.PageCount = GetElementValueAsInt(root, "PageCount");
        data.Writer = GetElementValue(root, "Writer");
        data.Penciller = GetElementValue(root, "Penciller");
        data.CoverImage = GetElementValue(root, "CoverImage");

        // 解析 Pages/Page 子元素
        var pagesElement = root.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Pages", StringComparison.OrdinalIgnoreCase));
        if (pagesElement is not null)
        {
            foreach (var pageElement in pagesElement.Elements()
                .Where(e => string.Equals(e.Name.LocalName, "Page", StringComparison.OrdinalIgnoreCase)))
            {
                data.Pages.Add(new ComicInfoPage
                {
                    Image = GetAttributeValue(pageElement, "Image"),
                    Type = GetAttributeValue(pageElement, "Type") ?? "Story",
                    Key = GetAttributeValue(pageElement, "Key")
                });
            }
        }

        return data;
    }

    /// <summary>
    /// 从 CBZ（ZIP）文件中提取 ComicInfo.xml 并解析。
    /// 不存在则返回 null。
    /// </summary>
    public ComicInfoData? ParseFromCbz(string cbzPath)
    {
        try
        {
            using var fs = File.OpenRead(cbzPath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

            // 查找 ComicInfo.xml（可能在根目录或子目录中）
            var entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.FullName), ComicInfoEntryName, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                return null;

            using var entryStream = entry.Open();
            return Parse(entryStream);
        }
        catch (Exception)
        {
            // 文件损坏、格式不兼容等 → 静默返回 null
            return null;
        }
    }

    /// <summary>
    /// 将 ComicInfoData 序列化为符合 ComicRack 格式的 XML 字符串。
    /// </summary>
    public string GenerateXml(ComicInfoData data)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("ComicInfo",
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                BuildElement("Title", data.Title),
                BuildElement("Series", data.Series),
                BuildElement("Number", data.Number?.ToString()),
                BuildElement("Summary", data.Summary),
                BuildElement("Writer", data.Writer),
                BuildElement("Penciller", data.Penciller),
                BuildElement("Genre", data.Genre),
                BuildElement("LanguageISO", data.LanguageISO),
                BuildElement("Manga", data.Manga),
                BuildElement("CoverImage", data.CoverImage),
                BuildElement("PageCount", data.PageCount?.ToString()),
                BuildElement("Count", data.PageCount?.ToString()),
                BuildPagesElement(data.Pages)
            )
        );

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        doc.Save(writer, SaveOptions.None);
        writer.Flush();
        ms.Position = 0;

        using var resultReader = new StreamReader(ms);
        return resultReader.ReadToEnd();
    }

    // ═══════════════════════════════════════════════════════════
    // XML 辅助方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 按本地名查找元素的文本值（忽略命名空间）。
    /// </summary>
    private static string? GetElementValue(XElement parent, string localName)
    {
        var element = parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        return element is not null && !string.IsNullOrWhiteSpace(element.Value)
            ? element.Value.Trim()
            : null;
    }

    /// <summary>
    /// 按本地名查找元素的整数值。
    /// </summary>
    private static int? GetElementValueAsInt(XElement parent, string localName)
    {
        var value = GetElementValue(parent, localName);
        return value is not null && int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// 读取属性值。
    /// </summary>
    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        var attr = element.Attributes()
            .FirstOrDefault(a => string.Equals(a.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase));
        return attr?.Value;
    }

    /// <summary>
    /// 辅助：仅当值非空时创建 XML 元素。
    /// </summary>
    private static XElement? BuildElement(string name, string? value)
    {
        return value is not null ? new XElement(name, value) : null;
    }

    /// <summary>
    /// 构建 Pages 元素（含子 Page 元素）。
    /// </summary>
    private static XElement? BuildPagesElement(List<ComicInfoPage> pages)
    {
        if (pages.Count == 0) return null;

        var pagesElement = new XElement("Pages");
        foreach (var page in pages)
        {
            var pageElement = new XElement("Page");
            if (page.Image is not null)
                pageElement.SetAttributeValue("Image", page.Image);
            if (page.Type is not null && page.Type != "Story")
                pageElement.SetAttributeValue("Type", page.Type);
            if (page.Key is not null)
                pageElement.SetAttributeValue("Key", page.Key);
            pagesElement.Add(pageElement);
        }

        return pagesElement;
    }
}
