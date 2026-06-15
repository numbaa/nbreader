using System.IO.Compression;
using System.Security.Cryptography;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;
using SharpCompress.Archives.Rar;

namespace NbReader.Core.Services;

/// <summary>
/// 本地扫描入库服务：扫描监控目录，发现 CBZ/CBR/图片文件夹，自动导入书架。
/// </summary>
public class LibraryScanner
{
    private readonly IStorageService _storage;
    private readonly IComicInfoParser _comicInfoParser;

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz", ".zip", ".cbr"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    private readonly string _coverCacheDir;

    public LibraryScanner(IStorageService storage, IComicInfoParser comicInfoParser)
    {
        _storage = storage;
        _comicInfoParser = comicInfoParser;
        _coverCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NbReader", "covers");
        Directory.CreateDirectory(_coverCacheDir);
    }

    // ═══════════════════════════════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 扫描所有已启用的监控目录，导入新发现的漫画。
    /// </summary>
    public Task<List<ComicResource>> ScanAllAsync()
    {
        return Task.FromResult(ScanAll());
    }

    private List<ComicResource> ScanAll()
    {
        var results = new List<ComicResource>();
        var dirs = _storage.GetMonitoredDirectories();

        foreach (var dir in dirs)
        {
            if (!dir.Enabled) continue;
            var imported = ScanDirectory(dir.Path);
            results.AddRange(imported);
        }

        return results;
    }

    /// <summary>
    /// 扫描单个目录（递归），导入新发现的 CBZ/CBR/图片文件夹。
    /// </summary>
    /// <param name="directoryPath">要扫描的目录路径。</param>
    /// <returns>新导入的漫画资源列表。</returns>
    public Task<List<ComicResource>> ScanDirectoryAsync(string directoryPath)
    {
        return Task.FromResult(ScanDirectory(directoryPath));
    }

    private List<ComicResource> ScanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return new List<ComicResource>();

        var results = new List<ComicResource>();

        // 收集所有 CBZ/CBR 文件
        var archiveFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => ArchiveExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        // 收集图片文件夹（目录内直接含图片文件的一级子目录）
        var imageFolders = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
            .Where(dir =>
            {
                try
                {
                    return Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                        .Any(f => ImageExtensions.Contains(Path.GetExtension(f)));
                }
                catch
                {
                    return false;
                }
            })
            .ToList();

        // 防止子文件夹被重复导入：如果父目录本身已是图片文件夹，跳过其子目录
        imageFolders = imageFolders
            .Where(folder => !imageFolders.Any(other =>
                other != folder && folder.StartsWith(other + Path.DirectorySeparatorChar)))
            .ToList();

        foreach (var file in archiveFiles)
        {
            try
            {
                var resource = ImportFile(file);
                if (resource is not null)
                    results.Add(resource);
            }
            catch
            {
                // 单个文件导入失败不中断整体扫描
            }
        }

        foreach (var folder in imageFolders)
        {
            try
            {
                var resource = ImportFile(folder);
                if (resource is not null)
                    results.Add(resource);
            }
            catch
            {
                // 单个文件夹导入失败不中断整体扫描
            }
        }

        return results;
    }

    /// <summary>
    /// 导入单个文件或图片文件夹到书架。
    /// </summary>
    /// <param name="path">文件或文件夹的绝对路径。</param>
    /// <returns>新创建的 ComicResource，若已存在则返回 null。</returns>
    public Task<ComicResource?> ImportFileAsync(string path)
    {
        return Task.FromResult(ImportFile(path));
    }

    /// <summary>
    /// 导入单个文件或图片文件夹到书架（同步实现）。
    /// </summary>
    private ComicResource? ImportFile(string path)
    {
        bool isDirectory = Directory.Exists(path);
        string sourceType = "local";
        string sourceId = Path.GetFullPath(path);

        // 1. 去重：按 (sourceType, sourceId) 检查
        var existing = _storage.GetResourceBySource(sourceType, sourceId);
        if (existing is not null)
            return null;

        // 2. 计算文件哈希
        string? fileHash = ComputeFileHash(path, isDirectory);

        // 3. 去重：按 hash 检查
        if (fileHash is not null)
        {
            var hashExisting = _storage.FindResourceByHash(fileHash);
            if (hashExisting is not null)
                return null;
        }

        // 4. 提取元数据：ComicInfo.xml > 文件名
        ComicInfoData? comicInfo = null;
        if (!isDirectory)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".cbz" or ".zip")
                comicInfo = _comicInfoParser.ParseFromCbz(path);
        }

        // 5. 确定标题
        string title;
        if (!string.IsNullOrWhiteSpace(comicInfo?.Title))
            title = comicInfo.Title;
        else
            title = Path.GetFileNameWithoutExtension(path);

        // 6. 计算页数
        int pageCount = CountPages(path, isDirectory);

        // 7. 创建资源
        var resource = new ComicResource
        {
            Title = title,
            SourceType = sourceType,
            SourceId = sourceId,
            PageCount = pageCount,
            Language = comicInfo?.LanguageISO,
            ContentType = DetermineContentType(comicInfo),
            AddedDate = DateTime.UtcNow.ToString("O"),
            IsBookmarked = true,
            IsDownloaded = true,
            LocalPath = isDirectory ? sourceId : sourceId,
            VolumeNumber = comicInfo?.Number,
            FileHash = fileHash
        };

        int resourceId = _storage.AddResource(resource);
        resource.Id = resourceId;

        // 8. 提取封面
        string? coverPath = ExtractCover(path, isDirectory, comicInfo, resourceId);
        if (coverPath is not null)
        {
            resource.CoverPath = coverPath;
            _storage.UpdateResource(resource);
        }

        // 9. 导入标签（ComicInfo.xml 中的 Genre + Writer/Penciller）
        if (comicInfo is not null)
            ImportComicInfoTags(resourceId, comicInfo);

        // 10. 归入「未分类」
        EnsureUncategorizedCategory(resourceId);

        return resource;
    }

    // ═══════════════════════════════════════════════════════════
    // 私有方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 根据 ComicInfo.xml 的 Manga 字段确定内容类型。
    /// </summary>
    private static string? DetermineContentType(ComicInfoData? comicInfo)
    {
        if (comicInfo?.Manga is null) return null;
        return comicInfo.Manga.Equals("Yes", StringComparison.OrdinalIgnoreCase) ? "manga"
             : comicInfo.Manga.Equals("No", StringComparison.OrdinalIgnoreCase) ? "comic"
             : null;
    }

    /// <summary>
    /// 计算文件指纹。
    /// CBZ/CBR: SHA256 前 1MB。
    /// 文件夹: SHA256(文件名|大小 排序拼接)。
    /// </summary>
    internal static string? ComputeFileHash(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                return ComputeDirectoryHash(path);
            }
            else
            {
                return ComputeFileHashFirst1MB(path);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// SHA256 of first 1MB of file.
    /// </summary>
    private static string? ComputeFileHashFirst1MB(string filePath)
    {
        const int maxBytes = 1024 * 1024; // 1MB

        using var fs = File.OpenRead(filePath);
        var buffer = new byte[Math.Min(maxBytes, fs.Length)];
        int bytesRead = fs.Read(buffer, 0, buffer.Length);

        var hash = SHA256.HashData(buffer.AsSpan(0, bytesRead));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// SHA256 of (sorted filename|filesize) concatenation for a directory.
    /// </summary>
    private static string? ComputeDirectoryHash(string directoryPath)
    {
        var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .Where(fi => ImageExtensions.Contains(fi.Extension))
            .OrderBy(fi => fi.Name, StringComparer.OrdinalIgnoreCase)
            .Select(fi => $"{fi.Name}|{fi.Length}")
            .ToList();

        if (files.Count == 0)
            return null;

        var content = string.Join("\n", files);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 计算漫画总页数。
    /// </summary>
    internal static int CountPages(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                return Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => ImageExtensions.Contains(Path.GetExtension(f)));
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext is ".cbz" or ".zip")
                return CountPagesInZip(path);

            if (ext is ".cbr")
                return CountPagesInRar(path);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountPagesInZip(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        return archive.Entries
            .Count(e => !e.FullName.EndsWith('/')
                        && ImageExtensions.Contains(Path.GetExtension(e.FullName)));
    }

    private static int CountPagesInRar(string filePath)
    {
        using var archive = RarArchive.Open(filePath);

        return archive.Entries
            .Count(e => !e.IsDirectory
                        && e.Key is not null
                        && ImageExtensions.Contains(Path.GetExtension(e.Key)));
    }

    /// <summary>
    /// 提取封面图片并缓存。
    /// 优先级：ComicInfo FrontCover 标注 > ComicInfo CoverImage > 第一张图片。
    /// </summary>
    internal string? ExtractCover(string path, bool isDirectory,
        ComicInfoData? comicInfo, int resourceId)
    {
        try
        {
            byte[]? coverBytes = null;

            if (!isDirectory)
            {
                coverBytes = ExtractCoverFromArchive(path, comicInfo);
            }
            else
            {
                coverBytes = ExtractCoverFromFolder(path);
            }

            if (coverBytes is null || coverBytes.Length == 0)
                return null;

            var coverPath = Path.Combine(_coverCacheDir, $"{resourceId}.jpg");
            File.WriteAllBytes(coverPath, coverBytes);
            return coverPath;
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ExtractCoverFromArchive(string filePath, ComicInfoData? comicInfo)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".cbz" or ".zip")
            return ExtractCoverFromZip(filePath, comicInfo);

        if (ext is ".cbr")
            return ExtractCoverFromRar(filePath, comicInfo);

        return null;
    }

    private byte[]? ExtractCoverFromZip(string filePath, ComicInfoData? comicInfo)
    {
        using var fs = File.OpenRead(filePath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        var imageEntries = archive.Entries
            .Where(e => !e.FullName.EndsWith('/')
                        && ImageExtensions.Contains(Path.GetExtension(e.FullName)))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageEntries.Count == 0)
            return null;

        // 优先级 1: FrontCover 标注
        string? coverFileName = comicInfo?.GetCoverImageFileName();
        if (coverFileName is not null)
        {
            var coverEntry = imageEntries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.FullName), coverFileName, StringComparison.OrdinalIgnoreCase));
            if (coverEntry is not null)
                return ReadEntryBytes(coverEntry);
        }

        // 优先级 2: 第一张图片
        return ReadEntryBytes(imageEntries[0]);
    }

    private byte[]? ExtractCoverFromRar(string filePath, ComicInfoData? comicInfo)
    {
        using var archive = RarArchive.Open(filePath);

        var imageEntries = archive.Entries
            .Where(e => !e.IsDirectory
                        && e.Key is not null
                        && ImageExtensions.Contains(Path.GetExtension(e.Key)))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageEntries.Count == 0)
            return null;

        // 优先级 1: FrontCover 标注
        string? coverFileName = comicInfo?.GetCoverImageFileName();
        if (coverFileName is not null)
        {
            var coverEntry = imageEntries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.Key), coverFileName, StringComparison.OrdinalIgnoreCase));
            if (coverEntry is not null)
                return ReadRarEntryBytes(coverEntry);
        }

        // 优先级 2: 第一张图片
        return ReadRarEntryBytes(imageEntries[0]);
    }

    private byte[]? ExtractCoverFromFolder(string folderPath)
    {
        var firstImage = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (firstImage is null)
            return null;

        return File.ReadAllBytes(firstImage);
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] ReadRarEntryBytes(SharpCompress.Archives.IArchiveEntry entry)
    {
        using var stream = entry.OpenEntryStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 从 ComicInfo.xml 导入标签到资源。
    /// </summary>
    private void ImportComicInfoTags(int resourceId, ComicInfoData comicInfo)
    {
        // 导入 Genre 标签（type='general'）
        foreach (var tagName in comicInfo.GetGenreTags())
        {
            var tag = _storage.GetOrCreateTag(tagName, "general");
            _storage.AddTagToResource(resourceId, tag.Id);
        }

        // 导入 Writer/Penciller 标签（type='artist'）
        foreach (var artist in comicInfo.GetArtists())
        {
            var tag = _storage.GetOrCreateTag(artist, "artist");
            _storage.AddTagToResource(resourceId, tag.Id);
        }
    }

    /// <summary>
    /// 确保「未分类」分类存在，并将资源归入。
    /// </summary>
    private void EnsureUncategorizedCategory(int resourceId)
    {
        var categories = _storage.GetCategories();
        var uncategorized = categories.FirstOrDefault(c => c.Name == "未分类");

        int catId;
        if (uncategorized is null)
            catId = _storage.CreateCategory("未分类");
        else
            catId = uncategorized.Id;

        _storage.AddToCategory(resourceId, catId);
    }
}
