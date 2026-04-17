using NbReader.Reader;

namespace NbReader.Import.Tests;

public sealed class ReaderStateMachineTests
{
    [Fact]
    public void OpenVolume_ShouldInitializeAtFirstPage()
    {
        var state = ReaderStateMachine.OpenVolume(
            volumeId: 12,
            volumeTitle: "Vol-1",
            sourcePath: "/tmp/source",
            pageLocators: new[] { "1.jpg", "2.jpg", "3.jpg" });

        Assert.Equal(ReaderLifecycle.VolumeReady, state.Lifecycle);
        Assert.Equal(0, state.CurrentPageIndex);
        Assert.Equal(1, state.CurrentPageNumber);
        Assert.Equal(3, state.TotalPages);
        Assert.False(state.CanMovePrevious);
        Assert.True(state.CanMoveNext);
    }

    [Fact]
    public void NavigateTo_ShouldClampIntoValidRange()
    {
        var opened = ReaderStateMachine.OpenVolume(
            volumeId: 5,
            volumeTitle: "Vol-2",
            sourcePath: "/tmp/source",
            pageLocators: new[] { "1.jpg", "2.jpg", "3.jpg" });

        var toBelow = ReaderStateMachine.NavigateTo(opened, -3);
        var toAbove = ReaderStateMachine.NavigateTo(opened, 99);

        Assert.Equal(0, toBelow.CurrentPageIndex);
        Assert.Equal(2, toAbove.CurrentPageIndex);
        Assert.Equal(ReaderLifecycle.PageLoading, toAbove.Lifecycle);
    }

    [Fact]
    public void MarkState_ShouldTransitionToReadyOrError()
    {
        var opened = ReaderStateMachine.OpenVolume(
            volumeId: 7,
            volumeTitle: "Vol-3",
            sourcePath: "/tmp/source",
            pageLocators: new[] { "1.jpg" });
        var loading = ReaderStateMachine.NavigateTo(opened, 0);

        var ready = ReaderStateMachine.MarkPageReady(loading);
        var error = ReaderStateMachine.MarkError(loading, "failed");

        Assert.Equal(ReaderLifecycle.PageReady, ready.Lifecycle);
        Assert.Null(ready.ErrorMessage);
        Assert.Equal(ReaderLifecycle.Error, error.Lifecycle);
        Assert.Equal("failed", error.ErrorMessage);
    }

    [Fact]
    public void NearbyPageWindowPolicy_ShouldReturnCurrentAndNeighbors()
    {
        var policy = new NearbyPageWindowPolicy(radius: 1);

        var middle = policy.GetWindowIndices(currentPageIndex: 3, totalPages: 8);
        var edge = policy.GetWindowIndices(currentPageIndex: 0, totalPages: 8);

        Assert.Equal(new HashSet<int> { 2, 3, 4 }, middle);
        Assert.Equal(new HashSet<int> { 0, 1 }, edge);
    }
}