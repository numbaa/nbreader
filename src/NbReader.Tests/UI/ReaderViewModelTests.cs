using NbReader.Core.Abstractions;
using NbReader.ViewModels;
using SkiaSharp;

namespace NbReader.Tests.UI;

/// <summary>
/// ReaderViewModel 的单元测试。
/// </summary>
public class ReaderViewModelTests
{
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
