import CustomDump
import EmbedPDF
import Testing

@Suite struct PageRenderResolutionTests {
  @Test func invisiblePagesIgnoreZoomAndKeepSmallPlaceholders() {
    let result = PageRenderResolution.forPage(viewWidth: 800, displayScale: 3, zoomScale: 8, isOnScreen: false)
    #expect(result.deviceWidth == 256)
    #expect(!result.isFull)
  }

  @Test func visiblePagesRoundUpToPixelBands() {
    let result = PageRenderResolution.forPage(viewWidth: 390, displayScale: 2, zoomScale: 2, isOnScreen: true)
    #expect(result.deviceWidth == 1664)
    #expect(result.isFull)
  }

  @Test func zoomingOutDoesNotLowerTheNativeDisplayDemand() {
    #expect(PageRenderResolution.forPage(viewWidth: 390, displayScale: 2, zoomScale: 0.25, isOnScreen: true).deviceWidth == 896)
  }

  @Test func anEmptyLayoutKeepsTheMinimumRasterDemand() {
    #expect(PageRenderResolution.forPage(viewWidth: 0, displayScale: 2, isOnScreen: true).deviceWidth == 64)
  }

  /// A pinch moves the scale every frame. A 612-point page at 2 and at 2.05
  /// is 1224 and 1254.6 pixels wide, both in the 1280 band, so both render
  /// at 1280 pixels: never fewer than the larger scale asks for.
  @Test func scalesWithinOneBandShareARender() {
    let low = PageRenderResolution.bandedScale(2, pageWidth: 612)
    let high = PageRenderResolution.bandedScale(2.05, pageWidth: 612)
    expectNoDifference(low, high)
    expectNoDifference((low * 612).rounded(), 1280)
    #expect(low >= 2.05)
    // 2.1 is 1285.2 pixels, past the band: the next render is 1408 wide.
    let next = PageRenderResolution.bandedScale(2.1, pageWidth: 612)
    expectNoDifference((next * 612).rounded(), 1408)
    // A width already on a band edge stays there.
    expectNoDifference((PageRenderResolution.bandedScale(1280.0 / 612, pageWidth: 612) * 612).rounded(), 1280)
  }

  @Test func aDegeneratePageKeepsItsScale() {
    expectNoDifference(PageRenderResolution.bandedScale(2, pageWidth: 0), 2)
    expectNoDifference(PageRenderResolution.bandedScale(0, pageWidth: 612), 0)
  }

  @Test func aQuarterTurnConvertsDisplayedWidthBackToPageWidth() {
    let result = PageRenderResolution(deviceWidth: 1024, isFull: true)
    #expect(result.unrotatedDeviceWidth(unrotatedWidth: 612, displayedWidth: 792) == 791)
  }
}
