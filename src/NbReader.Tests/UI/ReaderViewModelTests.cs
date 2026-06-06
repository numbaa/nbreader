namespace NbReader.Tests.UI;

/// <summary>
/// ReaderViewModel 的单元测试。
/// </summary>
public class ReaderViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var vm = new ViewModels.ReaderViewModel();

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
        vm.TotalPages.Should().Be(0);
        vm.ZoomLevel.Should().Be(1.0);
        vm.IsLoading.Should().BeFalse();
        vm.StatusText.Should().Be("就绪");
        vm.ComicName.Should().BeEmpty();
        vm.CurrentImage.Should().BeNull();
    }

    [Fact]
    public void GoToPrevPage_WhenAtFirstPage_ShouldNotChangePage()
    {
        // Arrange
        var vm = new ViewModels.ReaderViewModel();

        // Act
        vm.GoToPrevPageCommand.Execute(null);

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void GoToNextPage_WhenNoPages_ShouldNotChangePage()
    {
        // Arrange
        var vm = new ViewModels.ReaderViewModel();

        // Act
        vm.GoToNextPageCommand.Execute(null);

        // Assert
        vm.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void ZoomIn_ShouldIncreaseZoomLevel()
    {
        // Arrange
        var vm = new ViewModels.ReaderViewModel();
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
        var vm = new ViewModels.ReaderViewModel();

        // Act
        vm.ZoomOutCommand.Execute(null);

        // Assert
        vm.ZoomLevel.Should().BeLessThan(1.0);
    }

    [Fact]
    public void ZoomReset_ShouldResetToOriginalZoom()
    {
        // Arrange
        var vm = new ViewModels.ReaderViewModel();
        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);

        // Act
        vm.ZoomResetCommand.Execute(null);

        // Assert
        vm.ZoomLevel.Should().Be(1.0);
    }
}
