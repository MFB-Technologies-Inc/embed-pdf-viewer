package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.round

class PageRenderResolutionTest {
    @Test
    fun invisiblePagesIgnoreZoomAndKeepSmallPlaceholders() {
        val result = PageRenderResolution.forPage(viewWidth = 800.0, displayScale = 3.0, isOnScreen = false, zoomScale = 8.0)
        assertEquals(256.0, result.deviceWidth, 0.0)
        assertFalse(result.isFull)
    }

    @Test
    fun visiblePagesRoundUpToPixelBands() {
        val result = PageRenderResolution.forPage(viewWidth = 390.0, displayScale = 2.0, isOnScreen = true, zoomScale = 2.0)
        assertEquals(1664.0, result.deviceWidth, 0.0)
        assertTrue(result.isFull)
    }

    @Test
    fun zoomingOutDoesNotLowerTheNativeDisplayDemand() {
        assertEquals(
            896.0,
            PageRenderResolution.forPage(viewWidth = 390.0, displayScale = 2.0, isOnScreen = true, zoomScale = 0.25).deviceWidth,
            0.0,
        )
    }

    @Test
    fun anEmptyLayoutKeepsTheMinimumRasterDemand() {
        assertEquals(64.0, PageRenderResolution.forPage(viewWidth = 0.0, displayScale = 2.0, isOnScreen = true).deviceWidth, 0.0)
    }

    /**
     * A pinch moves the scale every frame. A 612-point page at 2 and at 2.05
     * is 1224 and 1254.6 pixels wide, both in the 1280 band, so both render at
     * 1280 pixels: never fewer than the larger scale asks for.
     */
    @Test
    fun scalesWithinOneBandShareARender() {
        val low = PageRenderResolution.bandedScale(2.0, pageWidth = 612.0)
        val high = PageRenderResolution.bandedScale(2.05, pageWidth = 612.0)
        assertEquals(low, high, 0.0)
        assertEquals(1280.0, round(low * 612), 0.0)
        assertTrue(low >= 2.05)
        // 2.1 is 1285.2 pixels, past the band: the next render is 1408 wide.
        assertEquals(1408.0, round(PageRenderResolution.bandedScale(2.1, pageWidth = 612.0) * 612), 0.0)
        // A width already on a band edge stays there.
        assertEquals(1280.0, round(PageRenderResolution.bandedScale(1280.0 / 612, pageWidth = 612.0) * 612), 0.0)
    }

    @Test
    fun aDegeneratePageKeepsItsScale() {
        assertEquals(2.0, PageRenderResolution.bandedScale(2.0, pageWidth = 0.0), 0.0)
        assertEquals(0.0, PageRenderResolution.bandedScale(0.0, pageWidth = 612.0), 0.0)
    }

    @Test
    fun aQuarterTurnConvertsDisplayedWidthBackToPageWidth() {
        val result = PageRenderResolution(deviceWidth = 1024.0, isFull = true)
        assertEquals(791.0, result.unrotatedDeviceWidth(unrotatedWidth = 612.0, displayedWidth = 792.0), 0.0)
    }

    @Test
    fun halvesRoundAwayFromZero() {
        assertEquals(3.0, roundHalfAwayFromZero(2.5), 0.0)
        assertEquals(-3.0, roundHalfAwayFromZero(-2.5), 0.0)
    }
}
