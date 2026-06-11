using NbReader.Core.Models;

namespace NbReader.Core.Abstractions;

/// <summary>
/// ComicInfo.xml 解析器：从 CBZ 内提取并解析 ComicRack 标准元数据。
/// </summary>
public interface IComicInfoParser
{
    /// <summary>从 XML 流解析 ComicInfo</summary>
    ComicInfoData Parse(Stream xmlStream);

    /// <summary>从 CBZ 文件中提取并解析 ComicInfo.xml，不存在返回 null</summary>
    ComicInfoData? ParseFromCbz(string cbzPath);

    /// <summary>将 ComicInfoData 序列化为 ComicInfo.xml 字符串</summary>
    string GenerateXml(ComicInfoData data);
}
