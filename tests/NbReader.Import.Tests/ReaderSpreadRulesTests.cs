using NbReader.Reader;

namespace NbReader.Import.Tests;

public sealed class ReaderSpreadRulesTests
{
    [Fact]
    public void NormalizeAnchorIndex_DualPageWithCoverSingle_ShouldGroupIntoPairsAfterCover()
    {
        var cover = ReaderSpreadRules.NormalizeAnchorIndex(0, totalPages: 8, ReaderDisplayMode.DualPage, coverSinglePage: true);
        var pairA = ReaderSpreadRules.NormalizeAnchorIndex(2, totalPages: 8, ReaderDisplayMode.DualPage, coverSinglePage: true);
        var pairB = ReaderSpreadRules.NormalizeAnchorIndex(4, totalPages: 8, ReaderDisplayMode.DualPage, coverSinglePage: true);

        Assert.Equal(0, cover);
        Assert.Equal(1, pairA);
        Assert.Equal(3, pairB);
    }

    [Fact]
    public void BuildSpread_DualPageLtr_ShouldUseAscendingLeftRightPair()
    {
        var spread = ReaderSpreadRules.BuildSpread(
            anchorPageIndex: 1,
            totalPages: 8,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.LeftToRight,
            coverSinglePage: true);

        Assert.Equal(1, spread.AnchorPageIndex);
        Assert.Equal(1, spread.LeftPageIndex);
        Assert.Equal(2, spread.RightPageIndex);
    }

    [Fact]
    public void BuildSpread_DualPageRtl_ShouldSwapPairToRightToLeft()
    {
        var spread = ReaderSpreadRules.BuildSpread(
            anchorPageIndex: 1,
            totalPages: 8,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.RightToLeft,
            coverSinglePage: true);

        Assert.Equal(2, spread.LeftPageIndex);
        Assert.Equal(1, spread.RightPageIndex);
    }

    [Fact]
    public void BuildSpread_CoverShouldBeSingleRightPageInDualMode()
    {
        var spread = ReaderSpreadRules.BuildSpread(
            anchorPageIndex: 0,
            totalPages: 8,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.LeftToRight,
            coverSinglePage: true);

        Assert.Null(spread.LeftPageIndex);
        Assert.Equal(0, spread.RightPageIndex);
    }

    [Fact]
    public void GetInitialAnchorIndex_RtlDualPage_ShouldStartFromTailSpread()
    {
        var anchor = ReaderSpreadRules.GetInitialAnchorIndex(
            totalPages: 9,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.RightToLeft,
            coverSinglePage: true);

        Assert.Equal(7, anchor);
    }
}
