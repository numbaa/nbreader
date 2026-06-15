using NbReader.Core.Abstractions;
using NbReader.Core.Models;
using NbReader.Core.Services;
using NbReader.ViewModels;
using SkiaSharp;

namespace NbReader.Tests.UI;

/// <summary>
/// ReaderViewModel 的单元测试。
/// </summary>
public class ReaderViewModelTests : IDisposable
{
    private readonly SqliteStorageService _storage;

    public ReaderViewModelTests()
    {
        _storage = new SqliteStorageService(":memory:");
    }

    public void Dispose()
    {
        _storage.Dispose();
    }

    private static IImageLoader CreateFakeLoader()
    {
        return new FakeImageLoader();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
        vm.TotalPages.Should().Be(0);
        vm.ZoomLevel.Should().Be(1.0);
        vm.IsLoading.Should().BeFalse();
        vm.StatusText.Should().Be("就绪");
        vm.ComicName.Should().BeEmpty();
        vm.CurrentImage.Should().BeNull();
        vm.DisplayBitmap.Should().BeNull();
    }

    [Fact]
    public void GoToPrevPage_WhenAtFirstPage_ShouldNotChangePage()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Act
        vm.GoToPrevPageCommand.Execute(null);

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void GoToNextPage_WhenNoPages_ShouldNotChangePage()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Act
        vm.GoToNextPageCommand.Execute(null);

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void ZoomIn_ShouldIncreaseZoomLevel()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());
        var originalZoom = vm.ZoomLevel;

        // Act
        vm.ZoomInCommand.Execute(null);

        // Assert
        vm.ZoomLevel.Should().BeGreaterThan(originalZoom);
    }

    [Fact]
    public void ZoomOut_ShouldDecreaseZoomLevel()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Act
        vm.ZoomOutCommand.Execute(null);

        // Assert
        vm.ZoomLevel.Should().BeLessThan(1.0);
    }

    [Fact]
    public void ZoomReset_ShouldResetToOriginalZoom()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);

        // Act
        vm.ZoomResetCommand.Execute(null);

        // Assert
        vm.ZoomLevel.Should().Be(1.0);
    }

    // ─── FitMode 测试 ────────────────────────────────────────────────

    [Fact]
    public void FitMode_Default_ShouldBeUniform()
    {
        // Arrange & Act
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Assert
        vm.FitMode.Should().Be(FitMode.Uniform);
    }

    [Fact]
    public void FitModeText_ForEachMode_ShouldReturnChineseLabel()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Act & Assert
        vm.FitMode = FitMode.Uniform;
        vm.FitModeText.Should().Be("适应页面");

        vm.FitMode = FitMode.FillWidth;
        vm.FitModeText.Should().Be("适应宽度");

        vm.FitMode = FitMode.FillHeight;
        vm.FitModeText.Should().Be("适应高度");

        vm.FitMode = FitMode.Original;
        vm.FitModeText.Should().Be("原始大小");
    }

    [Fact]
    public void CycleFitMode_ShouldCycleInOrder()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());

        // Act & Assert: Uniform → FillWidth
        vm.CycleFitModeCommand.Execute(null);
        vm.FitMode.Should().Be(FitMode.FillWidth);

        // FillWidth → FillHeight
        vm.CycleFitModeCommand.Execute(null);
        vm.FitMode.Should().Be(FitMode.FillHeight);

        // FillHeight → Original
        vm.CycleFitModeCommand.Execute(null);
        vm.FitMode.Should().Be(FitMode.Original);

        // Original → Uniform (循环)
        vm.CycleFitModeCommand.Execute(null);
        vm.FitMode.Should().Be(FitMode.Uniform);
    }

    [Fact]
    public void CycleFitMode_AfterDirectSet_ShouldContinueFromCurrent()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader());
        vm.FitMode = FitMode.FillHeight;

        // Act
        vm.CycleFitModeCommand.Execute(null);

        // Assert: FillHeight → Original
        vm.FitMode.Should().Be(FitMode.Original);
    }

    // ─── ReadingMode 测试 ─────────────────────────────────────────────

    [Fact]
    public void ReadingMode_Default_ShouldBeSinglePage()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());
        vm.ReadingMode.Should().Be(ReadingMode.SinglePage);
    }

    [Fact]
    public void ReadingModeText_ForEachMode_ShouldReturnChineseLabel()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());

        vm.ReadingMode = ReadingMode.SinglePage;
        vm.ReadingModeText.Should().Be("单页");

        vm.ReadingMode = ReadingMode.DualPage;
        vm.ReadingModeText.Should().Be("双页");

        vm.ReadingMode = ReadingMode.Scroll;
        vm.ReadingModeText.Should().Be("滚动");
    }

    [Fact]
    public void CycleReadingMode_ShouldCycleInOrder()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());

        vm.CycleReadingModeCommand.Execute(null);
        vm.ReadingMode.Should().Be(ReadingMode.DualPage);

        vm.CycleReadingModeCommand.Execute(null);
        vm.ReadingMode.Should().Be(ReadingMode.Scroll);

        vm.CycleReadingModeCommand.Execute(null);
        vm.ReadingMode.Should().Be(ReadingMode.SinglePage);
    }

    [Fact]
    public void IsSinglePage_IsDualPage_IsScrollMode_ShouldReflectMode()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());

        vm.ReadingMode = ReadingMode.SinglePage;
        vm.IsSinglePage.Should().BeTrue();
        vm.IsDualPage.Should().BeFalse();
        vm.IsScrollMode.Should().BeFalse();

        vm.ReadingMode = ReadingMode.DualPage;
        vm.IsSinglePage.Should().BeFalse();
        vm.IsDualPage.Should().BeTrue();

        vm.ReadingMode = ReadingMode.Scroll;
        vm.IsScrollMode.Should().BeTrue();
    }

    // ─── ReadingDirection 测试 ────────────────────────────────────────

    [Fact]
    public void ReadingDirection_Default_ShouldBeLeftToRight()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());
        vm.ReadingDirection.Should().Be(ReadingDirection.LeftToRight);
    }

    [Fact]
    public void ReadingDirectionText_ShouldReturnLabel()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());

        vm.ReadingDirection = ReadingDirection.LeftToRight;
        vm.ReadingDirectionText.Should().Be("L→R");

        vm.ReadingDirection = ReadingDirection.RightToLeft;
        vm.ReadingDirectionText.Should().Be("R→L");
    }

    [Fact]
    public void CycleReadingDirection_ShouldToggle()
    {
        var vm = new ReaderViewModel(CreateFakeLoader());

        vm.CycleReadingDirectionCommand.Execute(null);
        vm.ReadingDirection.Should().Be(ReadingDirection.RightToLeft);

        vm.CycleReadingDirectionCommand.Execute(null);
        vm.ReadingDirection.Should().Be(ReadingDirection.LeftToRight);
    }

    // ─── 阅读进度持久化测试 ────────────────────────────────────────

    [Fact]
    public void SaveCurrentProgress_WhenNoResource_ShouldNotThrow()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Act: no resource set, save should be no-op
        var act = () => vm.SaveCurrentProgress();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SaveCurrentProgress_ShouldPersistToStorage()
    {
        // Arrange: 先注册一个漫画资源
        var resource = new ComicResource
        {
            Title = "Test Comic",
            SourceType = "local",
            SourceId = "/test/path.cbz",
            PageCount = 100,
            AddedDate = DateTime.UtcNow.ToString("O"),
            IsBookmarked = true
        };
        int resourceId = _storage.AddResource(resource);

        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // 模拟设置当前资源（通过反射或直接设字段）
        typeof(ReaderViewModel)
            .GetField("_currentResourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, resourceId);
        // 模拟翻到第 42 页
        typeof(ReaderViewModel)
            .GetProperty("CurrentPageIndex")!
            .SetValue(vm, 42);

        // Act
        vm.SaveCurrentProgress();

        // Assert
        var progress = _storage.GetProgress(resourceId);
        progress.Should().NotBeNull();
        progress!.CurrentPage.Should().Be(42);
    }

    [Fact]
    public void Close_ShouldSaveProgress()
    {
        // Arrange
        var resource = new ComicResource
        {
            Title = "Test Comic 2",
            SourceType = "local",
            SourceId = "/test/path2.cbz",
            PageCount = 50,
            AddedDate = DateTime.UtcNow.ToString("O"),
            IsBookmarked = true
        };
        int resourceId = _storage.AddResource(resource);

        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);
        typeof(ReaderViewModel)
            .GetField("_currentResourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, resourceId);
        typeof(ReaderViewModel)
            .GetProperty("CurrentPageIndex")!
            .SetValue(vm, 25);

        // Act
        vm.CloseCommand.Execute(null);

        // Assert
        var progress = _storage.GetProgress(resourceId);
        progress.Should().NotBeNull();
        progress!.CurrentPage.Should().Be(25);
        vm.CurrentPageIndex.Should().Be(0); // 关闭后重置
    }

    // ─── 设置持久化测试 ────────────────────────────────────────────

    [Fact]
    public void CycleFitMode_ShouldPersistSetting()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Act: 切换到 FillWidth
        vm.CycleFitModeCommand.Execute(null);
        vm.FitMode.Should().Be(FitMode.FillWidth);

        // Assert: 设置已持久化
        _storage.GetSetting("reader.fitMode").Should().Be("FillWidth");
    }

    [Fact]
    public void CycleReadingDirection_ShouldPersistSetting()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Act: 切换到 R→L
        vm.CycleReadingDirectionCommand.Execute(null);
        vm.ReadingDirection.Should().Be(ReadingDirection.RightToLeft);

        // Assert: 设置已持久化
        _storage.GetSetting("reader.readingDirection").Should().Be("RightToLeft");
    }

    [Fact]
    public void Constructor_ShouldRestoreSettingsFromStorage()
    {
        // Arrange: 预先写入设置
        _storage.SetSetting("reader.fitMode", "FillHeight");
        _storage.SetSetting("reader.readingDirection", "RightToLeft");

        // Act: 新建 VM 应恢复设置
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Assert
        vm.FitMode.Should().Be(FitMode.FillHeight);
        vm.ReadingDirection.Should().Be(ReadingDirection.RightToLeft);
    }

    [Fact]
    public void Constructor_ShouldDefaultWhenNoSavedSettings()
    {
        // Arrange & Act: 无预存设置时新建 VM
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Assert: 使用默认值
        vm.FitMode.Should().Be(FitMode.Uniform);
        vm.ReadingDirection.Should().Be(ReadingDirection.LeftToRight);
    }

    [Fact]
    public void Constructor_ShouldRestoreReadingModeFromStorage()
    {
        // Arrange
        _storage.SetSetting("reader.readingMode", "Scroll");

        // Act
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Assert
        vm.ReadingMode.Should().Be(ReadingMode.Scroll);
    }

    [Fact]
    public void CycleReadingMode_ShouldPersistSetting()
    {
        // Arrange
        var vm = new ReaderViewModel(CreateFakeLoader(), _storage);

        // Act: 切换到双页
        vm.CycleReadingModeCommand.Execute(null);
        vm.ReadingMode.Should().Be(ReadingMode.DualPage);

        // Assert: 设置已持久化
        _storage.GetSetting("reader.readingMode").Should().Be("DualPage");
    }
}

/// <summary>
/// 测试用 Fake IImageLoader：生成纯色测试图片。
/// </summary>
internal class FakeImageLoader : IImageLoader
{
    public Task<IImage> LoadAsync(Stream stream)
    {
        return Task.FromResult<IImage>(CreateTestImage(100, 100, SKColors.Red));
    }

    public Task<IImage> LoadAsync(string filePath)
    {
        return Task.FromResult<IImage>(CreateTestImage(200, 150, SKColors.Blue));
    }

    internal static IImage CreateTestImage(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        return new NbReader.Core.Services.SkiaImage(bitmap);
    }
}
