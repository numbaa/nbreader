using System.IO.Compression;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;
using NbReader.Core.Services;
using SkiaSharp;

namespace NbReader.Tests.Core;

/// <summary>
/// LibraryScanner 单元测试：覆盖本地扫描、导入、去重、元数据提取。
/// </summary>
public class LibraryScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteStorageService _storage;
    private readonly ComicInfoXmlParser _parser;
    private readonly LibraryScanner _scanner;

    public LibraryScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NbReader_Scan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _storage = new SqliteStorageService(":memory:");
        _parser = new ComicInfoXmlParser();
        _scanner = new LibraryScanner(_storage, _parser);
    }

    public void Dispose()
    {
        _storage.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════

    private static string CreateTestCbz(string directory, int imageCount = 3,
        string? comicInfoXml = null, string[]? extraFiles = null)
    {
        var cbzPath = Path.Combine(directory, $"test_{Guid.NewGuid():N}.cbz");

        using var fs = File.Create(cbzPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        for (int i = 0; i < imageCount; i++)
        {
            var entry = archive.CreateEntry($"page{i:D3}.png", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            CreateTestImageData(entryStream, 32, 32, new SKColor((byte)(i * 50), 100, 150));
        }

        if (comicInfoXml is not null)
        {
            var entry = archive.CreateEntry("ComicInfo.xml", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(comicInfoXml);
        }

        if (extraFiles is not null)
        {
            foreach (var fileName in extraFiles)
            {
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open());
                writer.Write("test");
            }
        }

        return cbzPath;
    }

    private static void CreateTestImageData(Stream stream, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        data.SaveTo(stream);
    }

    private static string CreateTestImageFolder(string parentDir, int imageCount = 3)
    {
        var folderPath = Path.Combine(parentDir, $"folder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folderPath);

        for (int i = 0; i < imageCount; i++)
        {
            var imagePath = Path.Combine(folderPath, $"page{i:D3}.png");
            using var fs = File.Create(imagePath);
            CreateTestImageData(fs, 32, 32, new SKColor((byte)(i * 50), 100, 150));
        }

        return folderPath;
    }

    // ═══════════════════════════════════════════════════════════
    // ImportFileAsync — 基本导入
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFileAsync_ValidCbz_Should_Create_Resource()
    {
        var cbzPath = CreateTestCbz(_tempDir, 5);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.Title.Should().Be(Path.GetFileNameWithoutExtension(cbzPath));
        resource.PageCount.Should().Be(5);
        resource.SourceType.Should().Be("local");
        resource.SourceId.Should().Be(Path.GetFullPath(cbzPath));
        resource.IsBookmarked.Should().BeTrue();
        resource.IsDownloaded.Should().BeTrue();
        resource.LocalPath.Should().Be(Path.GetFullPath(cbzPath));
        resource.FileHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImportFileAsync_ValidImageFolder_Should_Create_Resource()
    {
        var folderPath = CreateTestImageFolder(_tempDir, 4);

        var resource = await _scanner.ImportFileAsync(folderPath);

        resource.Should().NotBeNull();
        resource!.Title.Should().Be(Path.GetFileName(folderPath));
        resource.PageCount.Should().Be(4);
        resource.SourceType.Should().Be("local");
        resource.IsDownloaded.Should().BeTrue();
        resource.FileHash.Should().NotBeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // ImportFileAsync — 去重
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFileAsync_Duplicate_SourceId_Should_Return_Null()
    {
        var cbzPath = CreateTestCbz(_tempDir, 3);

        var first = await _scanner.ImportFileAsync(cbzPath);
        first.Should().NotBeNull();

        var second = await _scanner.ImportFileAsync(cbzPath);
        second.Should().BeNull();
    }

    [Fact]
    public async Task ImportFileAsync_SameContent_DifferentPath_Should_Return_Null()
    {
        var cbzPath1 = CreateTestCbz(_tempDir, 3);
        var cbzPath2 = Path.Combine(_tempDir, $"copy_{Guid.NewGuid():N}.cbz");
        File.Copy(cbzPath1, cbzPath2);

        var first = await _scanner.ImportFileAsync(cbzPath1);
        first.Should().NotBeNull();

        var second = await _scanner.ImportFileAsync(cbzPath2);
        second.Should().BeNull(); // Same hash, should be skipped
    }

    // ═══════════════════════════════════════════════════════════
    // ImportFileAsync — ComicInfo.xml 元数据提取
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFileAsync_CbzWithComicInfo_Should_Extract_Metadata()
    {
        var comicInfoXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>Ero Biiky</Title>
              <Series>Ero Biiky Series</Series>
              <Number>3</Number>
              <Writer>Eco Heeky</Writer>
              <Penciller>Some Artist</Penciller>
              <Genre>big breasts; nakadashi; ahegao</Genre>
              <LanguageISO>zh</LanguageISO>
              <Manga>Yes</Manga>
              <Summary>A story about...</Summary>
            </ComicInfo>
            """;

        var cbzPath = CreateTestCbz(_tempDir, 10, comicInfoXml);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.Title.Should().Be("Ero Biiky");
        resource.VolumeNumber.Should().Be(3);
        resource.Language.Should().Be("zh");
        resource.ContentType.Should().Be("manga");

        // 验证标签已导入
        var tags = _storage.GetResourceTags(resource.Id);
        tags.Should().NotBeEmpty();
        tags.Should().Contain(t => t.Name == "big breasts");
        tags.Should().Contain(t => t.Name == "nakadashi");
        tags.Should().Contain(t => t.Name == "ahegao");
        // 作者标签 type='artist'
        tags.Should().Contain(t => t.Name == "Eco Heeky" && t.Type == "artist");
        tags.Should().Contain(t => t.Name == "Some Artist" && t.Type == "artist");
    }

    [Fact]
    public async Task ImportFileAsync_CbzWithComicInfo_MangaNo_Should_Set_ContentType_Comic()
    {
        var comicInfoXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>American Comic</Title>
              <Manga>No</Manga>
            </ComicInfo>
            """;

        var cbzPath = CreateTestCbz(_tempDir, 5, comicInfoXml);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.ContentType.Should().Be("comic");
    }

    [Fact]
    public async Task ImportFileAsync_CbzWithoutComicInfo_Should_Use_Filename_As_Title()
    {
        var cbzPath = CreateTestCbz(_tempDir, 3);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.Title.Should().Be(Path.GetFileNameWithoutExtension(cbzPath));
        resource.Language.Should().BeNull();
        resource.ContentType.Should().BeNull();
        resource.VolumeNumber.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // ImportFileAsync — 封面提取
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFileAsync_CbzWithFrontCover_Should_Extract_Cover()
    {
        var comicInfoXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ComicInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Title>Test Cover</Title>
              <Pages>
                <Page Image="page000.png" Type="FrontCover" />
                <Page Image="page001.png" Type="Story" />
                <Page Image="page002.png" Type="Story" />
              </Pages>
            </ComicInfo>
            """;

        var cbzPath = CreateTestCbz(_tempDir, 3, comicInfoXml);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.CoverPath.Should().NotBeNullOrEmpty();
        File.Exists(resource.CoverPath).Should().BeTrue();
    }

    [Fact]
    public async Task ImportFileAsync_CbzWithoutComicInfo_Should_Use_FirstPage_As_Cover()
    {
        var cbzPath = CreateTestCbz(_tempDir, 3);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        resource!.CoverPath.Should().NotBeNullOrEmpty();
        File.Exists(resource.CoverPath).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // ImportFileAsync — 未分类归入
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFileAsync_Should_Add_To_Uncategorized()
    {
        var cbzPath = CreateTestCbz(_tempDir, 3);

        var resource = await _scanner.ImportFileAsync(cbzPath);

        resource.Should().NotBeNull();
        var categories = _storage.GetCategories();
        categories.Should().Contain(c => c.Name == "未分类");
    }

    // ═══════════════════════════════════════════════════════════
    // ScanDirectoryAsync
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScanDirectoryAsync_EmptyDirectory_Should_Return_Empty()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var results = await _scanner.ScanDirectoryAsync(emptyDir);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanDirectoryAsync_WithCbzFiles_Should_Import_All()
    {
        var scanDir = Path.Combine(_tempDir, "scan");
        Directory.CreateDirectory(scanDir);
        CreateTestCbz(scanDir, 3);
        CreateTestCbz(scanDir, 5);

        var results = await _scanner.ScanDirectoryAsync(scanDir);

        results.Should().HaveCount(2);
        results.All(r => r.PageCount > 0).Should().BeTrue();
    }

    [Fact]
    public async Task ScanDirectoryAsync_WithImageFolders_Should_Import_All()
    {
        var scanDir = Path.Combine(_tempDir, "scan");
        Directory.CreateDirectory(scanDir);
        CreateTestImageFolder(scanDir, 3);
        CreateTestImageFolder(scanDir, 4);

        var results = await _scanner.ScanDirectoryAsync(scanDir);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanDirectoryAsync_NestedDirectories_Should_Find_Recursively()
    {
        var scanDir = Path.Combine(_tempDir, "scan");
        var subDir = Path.Combine(scanDir, "sub");
        Directory.CreateDirectory(subDir);
        CreateTestCbz(scanDir, 3);
        CreateTestCbz(subDir, 4);

        var results = await _scanner.ScanDirectoryAsync(scanDir);

        results.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════
    // ScanAllAsync — 监控目录
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScanAllAsync_Should_Only_Scan_Enabled_Directories()
    {
        var enabledDir = Path.Combine(_tempDir, "enabled");
        var disabledDir = Path.Combine(_tempDir, "disabled");
        Directory.CreateDirectory(enabledDir);
        Directory.CreateDirectory(disabledDir);
        CreateTestCbz(enabledDir, 3);
        CreateTestCbz(disabledDir, 5); // 不同页数，确保 hash 不同

        _storage.AddMonitoredDirectory(enabledDir);
        _storage.AddMonitoredDirectory(disabledDir);

        var results = await _scanner.ScanAllAsync();

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAllAsync_No_Monitored_Directories_Should_Return_Empty()
    {
        var results = await _scanner.ScanAllAsync();
        results.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // ComputeFileHash
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ComputeFileHash_SameContent_Should_Produce_Same_Hash()
    {
        var cbzPath1 = CreateTestCbz(_tempDir, 3);
        var cbzPath2 = Path.Combine(_tempDir, $"copy_{Guid.NewGuid():N}.cbz");
        File.Copy(cbzPath1, cbzPath2);

        var resource1 = await _scanner.ImportFileAsync(cbzPath1);
        // Import second - should be blocked by hash
        var resource2 = await _scanner.ImportFileAsync(cbzPath2);

        resource1.Should().NotBeNull();
        resource2.Should().BeNull(); // Dedup by hash
    }

    [Fact]
    public async Task ComputeFileHash_DifferentContent_Should_Produce_Different_Hash()
    {
        var cbzPath1 = CreateTestCbz(_tempDir, 3);
        var cbzPath2 = CreateTestCbz(_tempDir, 5); // Different page count

        var resource1 = await _scanner.ImportFileAsync(cbzPath1);
        var resource2 = await _scanner.ImportFileAsync(cbzPath2);

        resource1.Should().NotBeNull();
        resource2.Should().NotBeNull();
        resource1!.FileHash.Should().NotBe(resource2!.FileHash);
    }
}
