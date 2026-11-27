using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class PageRenderResolutionTests
{
    [Fact]
    public void InvisiblePagesIgnoreZoomAndKeepSmallPlaceholders()
    {
        var result = PageRenderResolution.ForPage(viewWidth: 800, displayScale: 3, zoomScale: 8, isOnScreen: false);
        Assert.Equal(256, result.DeviceWidth);
        Assert.False(result.IsFull);
    }

    [Fact]
    public void VisiblePagesRoundUpToPixelBands()
    {
        var result = PageRenderResolution.ForPage(viewWidth: 390, displayScale: 2, zoomScale: 2, isOnScreen: true);
        Assert.Equal(1664, result.DeviceWidth);
        Assert.True(result.IsFull);
    }

    [Fact]
    public void ZoomingOutDoesNotLowerTheNativeDisplayDemand() =>
        Assert.Equal(896, PageRenderResolution.ForPage(viewWidth: 390, displayScale: 2, zoomScale: 0.25, isOnScreen: true).DeviceWidth);

    [Fact]
    public void AnEmptyLayoutKeepsTheMinimumRasterDemand() =>
        Assert.Equal(64, PageRenderResolution.ForPage(viewWidth: 0, displayScale: 2, isOnScreen: true).DeviceWidth);

    [Fact]
    public void ScalesWithinOneBandShareARender()
    {
        var low = PageRenderResolution.BandedScale(2, pageWidth: 612);
        var high = PageRenderResolution.BandedScale(2.05, pageWidth: 612);
        Assert.Equal(low, high);
        Assert.Equal(1280, Math.Round(low * 612));
        Assert.True(low >= 2.05);
        Assert.Equal(1408, Math.Round(PageRenderResolution.BandedScale(2.1, pageWidth: 612) * 612));
        Assert.Equal(1280, Math.Round(PageRenderResolution.BandedScale(1280.0 / 612, pageWidth: 612) * 612));
    }

    [Fact]
    public void ADegeneratePageKeepsItsScale()
    {
        Assert.Equal(2, PageRenderResolution.BandedScale(2, pageWidth: 0));
        Assert.Equal(0, PageRenderResolution.BandedScale(0, pageWidth: 612));
    }

    [Fact]
    public void AQuarterTurnConvertsDisplayedWidthBackToPageWidth() =>
        Assert.Equal(791, new PageRenderResolution(1024, true).UnrotatedDeviceWidth(unrotatedWidth: 612, displayedWidth: 792));

    [Fact]
    public void AHalfPixelRoundsAwayFromZero() =>
        Assert.Equal(3, new PageRenderResolution(5, true).UnrotatedDeviceWidth(unrotatedWidth: 1, displayedWidth: 2));
}
