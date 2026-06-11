using System.Text;
using NbReader.Core.Models;
using NbReader.Core.Services;

namespace NbReader.Tests.Core;

/// <summary>
/// ComicInfoXmlParser 单元测试。
/// </summary>
public class ComicInfoXmlParserTests
{
    private readonly ComicInfoXmlParser _parser = new();

    // ═══════════════════════════════════════════════════════════
    // 解析基本字段
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ValidXml_Should_Extract_All_Fields()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>Ero Biiky</Title>
              <Series>Ero Biiky</Series>
              <Number>1</Number>
              <Summary>A story about...</Summary>
              <Writer>Eco Heeky</Writer>
              <Genre>big breasts; nakadashi; ahegao</Genre>
              <LanguageISO>zh</LanguageISO>
              <Manga>Yes</Manga>
              <PageCount>174</PageCount>
              <Count>174</Count>
              <CoverImage>cover.jpg</CoverImage>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Title.Should().Be("Ero Biiky");
        data.Series.Should().Be("Ero Biiky");
        data.Number.Should().Be(1);
        data.Summary.Should().Be("A story about...");
        data.Writer.Should().Be("Eco Heeky");
        data.Genre.Should().Be("big breasts; nakadashi; ahegao");
        data.LanguageISO.Should().Be("zh");
        data.Manga.Should().Be("Yes");
        data.PageCount.Should().Be(174);
        data.CoverImage.Should().Be("cover.jpg");
    }

    [Fact]
    public void Parse_EmptyXml_Should_Return_Empty_Data()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Title.Should().BeNull();
        data.Series.Should().BeNull();
        data.Pages.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingOptionalFields_Should_Return_Null()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>Only Title</Title>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Title.Should().Be("Only Title");
        data.Series.Should().BeNull();
        data.Writer.Should().BeNull();
        data.LanguageISO.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyElements_Should_Return_Null()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title></Title>
              <Genre></Genre>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Title.Should().BeNull();
        data.Genre.Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceOnly_Should_Return_Null()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>   </Title>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Title.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 解析 Pages/Page 子元素
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Parse_Pages_Should_Extract_Page_Types()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>Test Pages</Title>
              <Pages>
                <Page Image="cover.jpg" Type="FrontCover" />
                <Page Image="01.jpg" Type="Story" />
                <Page Image="02.jpg" Type="Story" />
                <Page Image="ad.jpg" Type="Advertisement" />
                <Page Image="deleted.jpg" Type="Deleted" />
                <Page Image="back.jpg" Type="BackCover" />
              </Pages>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Pages.Should().HaveCount(6);
        data.Pages[0].Image.Should().Be("cover.jpg");
        data.Pages[0].Type.Should().Be("FrontCover");
        data.Pages[3].Type.Should().Be("Advertisement");
        data.Pages[4].Type.Should().Be("Deleted");
        data.Pages[5].Type.Should().Be("BackCover");
    }

    [Fact]
    public void Parse_Pages_Default_Type_Should_Be_Story()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Pages>
                <Page Image="01.jpg" />
                <Page Image="02.jpg" Key="Left" />
              </Pages>
            </ComicInfo>
            """;

        var data = ParseXml(xml);

        data.Pages[0].Type.Should().Be("Story");
        data.Pages[1].Key.Should().Be("Left");
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助方法测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetGenreTags_Should_Split_Semicolons()
    {
        var data = new ComicInfoData { Genre = "big breasts; nakadashi; ahegao" };
        var tags = data.GetGenreTags();
        tags.Should().BeEquivalentTo(["big breasts", "nakadashi", "ahegao"]);
    }

    [Fact]
    public void GetGenreTags_Should_Handle_Empty()
    {
        var data = new ComicInfoData { Genre = "" };
        data.GetGenreTags().Should().BeEmpty();

        data.Genre = null;
        data.GetGenreTags().Should().BeEmpty();
    }

    [Fact]
    public void GetPrimaryArtist_Should_Prefer_Writer()
    {
        var data = new ComicInfoData { Writer = "Alice", Penciller = "Bob" };
        data.GetPrimaryArtist().Should().Be("Alice");
    }

    [Fact]
    public void GetPrimaryArtist_Should_Fallback_To_Penciller()
    {
        var data = new ComicInfoData { Writer = null, Penciller = "Bob" };
        data.GetPrimaryArtist().Should().Be("Bob");
    }

    [Fact]
    public void GetArtists_Should_Collect_Both()
    {
        var data = new ComicInfoData { Writer = "Alice, Eve", Penciller = "Bob" };
        var artists = data.GetArtists();
        artists.Should().BeEquivalentTo(["Alice", "Eve", "Bob"]);
    }

    [Fact]
    public void GetCoverImageFileName_Should_Prefer_FrontCover_Page()
    {
        var data = new ComicInfoData
        {
            CoverImage = "default.jpg",
            Pages = new List<ComicInfoPage>
            {
                new() { Image = "real_cover.png", Type = "FrontCover" }
            }
        };
        data.GetCoverImageFileName().Should().Be("real_cover.png");
    }

    [Fact]
    public void GetCoverImageFileName_Should_Fallback_To_CoverImage()
    {
        var data = new ComicInfoData
        {
            CoverImage = "default.jpg",
            Pages = new List<ComicInfoPage>
            {
                new() { Image = "01.jpg", Type = "Story" }
            }
        };
        data.GetCoverImageFileName().Should().Be("default.jpg");
    }

    [Fact]
    public void GetCoverImageFileName_Should_Return_Null_When_No_Cover()
    {
        var data = new ComicInfoData();
        data.GetCoverImageFileName().Should().BeNull();
    }

    [Fact]
    public void GetDeletedPageImages_Should_Return_Deleted_Pages()
    {
        var data = new ComicInfoData
        {
            Pages = new List<ComicInfoPage>
            {
                new() { Image = "01.jpg", Type = "Story" },
                new() { Image = "02.jpg", Type = "Deleted" },
                new() { Image = "03.jpg", Type = "Story" },
                new() { Image = "04.jpg", Type = "Deleted" }
            }
        };

        var deleted = data.GetDeletedPageImages();
        deleted.Should().BeEquivalentTo(["02.jpg", "04.jpg"]);
    }

    // ═══════════════════════════════════════════════════════════
    // CBZ 文件解析
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void ParseFromCbz_With_ComicInfo_Should_Return_Data()
    {
        var cbzPath = @"C:\Users\ntu\Downloads\manga\ok\正相反的你与我\正相反的你与我_第01话.zip";

        // 跳过不存在的测试文件
        if (!File.Exists(cbzPath))
            return;

        var data = _parser.ParseFromCbz(cbzPath);

        data.Should().NotBeNull();
        data!.Title.Should().Be("第1话（44P）");
        data.Series.Should().Be("正相反的你与我");
        data.Number.Should().Be(1);
        data.Summary.Should().Contain("正相反的你与我");
        data.Writer.Should().Be("阿贺沢红茶");
        data.Genre.Should().BeNull(); // empty element → null
    }

    [Fact]
    public void ParseFromCbz_Without_ComicInfo_Should_Return_Null()
    {
        // 创建一个不含 ComicInfo.xml 的 CBZ
        var tempPath = Path.GetTempFileName() + ".cbz";
        try
        {
            using (var fs = File.Create(tempPath))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("page001.jpg");
                using var es = entry.Open();
                es.Write(new byte[] { 0xFF, 0xD8 }); // minimal JPEG header
            }

            var data = _parser.ParseFromCbz(tempPath);
            data.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ParseFromCbz_NonExistentFile_Should_Return_Null()
    {
        var data = _parser.ParseFromCbz(@"C:\nonexistent\file.cbz");
        data.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 生成 XML
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GenerateXml_Should_Contain_All_Fields()
    {
        var data = new ComicInfoData
        {
            Title = "Test Title",
            Series = "Test Series",
            Number = 3,
            Summary = "A great story",
            Writer = "Author Name",
            Genre = "action; drama",
            LanguageISO = "ja",
            Manga = "Yes",
            PageCount = 200
        };

        var xml = _parser.GenerateXml(data);

        xml.Should().Contain("<Title>Test Title</Title>");
        xml.Should().Contain("<Series>Test Series</Series>");
        xml.Should().Contain("<Number>3</Number>");
        xml.Should().Contain("<Summary>A great story</Summary>");
        xml.Should().Contain("<Writer>Author Name</Writer>");
        xml.Should().Contain("<Genre>action; drama</Genre>");
        xml.Should().Contain("<LanguageISO>ja</LanguageISO>");
        xml.Should().Contain("<Manga>Yes</Manga>");
        xml.Should().Contain("<PageCount>200</PageCount>");
        xml.Should().Contain("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    [Fact]
    public void GenerateXml_Should_Include_Pages()
    {
        var data = new ComicInfoData
        {
            Title = "With Pages",
            Pages = new List<ComicInfoPage>
            {
                new() { Image = "cover.jpg", Type = "FrontCover" },
                new() { Image = "01.jpg", Type = "Story" },
                new() { Image = "deleted.jpg", Type = "Deleted" }
            }
        };

        var xml = _parser.GenerateXml(data);

        xml.Should().Contain("<Page Image=\"cover.jpg\" Type=\"FrontCover\" />");
        xml.Should().Contain("<Page Image=\"01.jpg\" />"); // Story default not written
        xml.Should().Contain("<Page Image=\"deleted.jpg\" Type=\"Deleted\" />");
    }

    [Fact]
    public void GenerateXml_RoundTrip_Should_Preserve_Data()
    {
        var original = new ComicInfoData
        {
            Title = "Round Trip",
            Series = "Test Series",
            Number = 5,
            Summary = "Testing round trip",
            Writer = "Alice",
            Penciller = "Bob",
            Genre = "comedy; slice of life",
            LanguageISO = "en",
            Manga = "No",
            PageCount = 42,
            CoverImage = "cover.png",
            Pages = new List<ComicInfoPage>
            {
                new() { Image = "cover.png", Type = "FrontCover" },
                new() { Image = "01.jpg", Type = "Story" },
                new() { Image = "ad.jpg", Type = "Advertisement" }
            }
        };

        // 生成 → 解析 → 比较
        var xml = _parser.GenerateXml(original);
        var parsed = ParseXml(xml);

        parsed.Title.Should().Be(original.Title);
        parsed.Series.Should().Be(original.Series);
        parsed.Number.Should().Be(original.Number);
        parsed.Summary.Should().Be(original.Summary);
        parsed.Writer.Should().Be(original.Writer);
        parsed.Penciller.Should().Be(original.Penciller);
        parsed.Genre.Should().Be(original.Genre);
        parsed.LanguageISO.Should().Be(original.LanguageISO);
        parsed.Manga.Should().Be(original.Manga);
        parsed.PageCount.Should().Be(original.PageCount);
        parsed.CoverImage.Should().Be(original.CoverImage);
        parsed.Pages.Should().HaveCount(3);
        parsed.Pages[0].Image.Should().Be("cover.png");
        parsed.Pages[0].Type.Should().Be("FrontCover");
        parsed.Pages[2].Type.Should().Be("Advertisement");
    }

    [Fact]
    public void GenerateXml_Should_Skip_Null_Fields()
    {
        var data = new ComicInfoData { Title = "Minimal" };
        var xml = _parser.GenerateXml(data);

        xml.Should().NotContain("<Series>");
        xml.Should().NotContain("<Summary>");
        xml.Should().NotContain("<Writer>");
    }

    [Fact]
    public void GenerateXml_Should_Not_Include_Pages_When_Empty()
    {
        var data = new ComicInfoData { Title = "No Pages" };
        var xml = _parser.GenerateXml(data);

        xml.Should().NotContain("<Pages>");
    }

    // ═══════════════════════════════════════════════════════════
    // 真实文件中的自定义命名空间（ty:）
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Parse_With_Custom_Namespaces_Should_Not_Throw()
    {
        // 真实 ComicInfo.xml 包含 ty:PublishingStatusTachiyomi 等扩展元素
        var cbzPath = @"C:\Users\ntu\Downloads\manga\ok\正相反的你与我\正相反的你与我_第01话.zip";

        if (!File.Exists(cbzPath))
            return;

        var data = _parser.ParseFromCbz(cbzPath);

        data.Should().NotBeNull();
        // 核心字段仍应正确提取
        data!.Title.Should().Be("第1话（44P）");
        data.Series.Should().Be("正相反的你与我");
        data.Number.Should().Be(1);
        data.Writer.Should().Be("阿贺沢红茶");
    }

    [Fact]
    public void GenerateXml_Should_Produce_Valid_Xml()
    {
        var data = new ComicInfoData
        {
            Title = "Validation Test",
            Genre = "test"
        };

        var xml = _parser.GenerateXml(data);

        // 反序列化应成功
        var parsed = ParseXml(xml);
        parsed.Title.Should().Be("Validation Test");
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════════════════════

    private ComicInfoData ParseXml(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return _parser.Parse(stream);
    }
}
