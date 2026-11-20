package com.embedpdf

import com.embedpdf.RenderTest.Companion.rgba
import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.zip.CRC32

class RenderSnapshotTest {
    // The capture half of the Swift suite drives the snapshot tool through the
    // interaction capability, which arrives with wave B.

    @Test
    fun aCroppedAndRotatedRenderKeepsScaleAndColors() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        val image = render.renderPage(
            page,
            RenderPageOptions(rect = Rect(20.0, 0.0, 40.0, 20.0), rotation = RenderRotation.CLOCKWISE90, scale = 2.0),
        )
        assertEquals(40, image.width)
        assertEquals(80, image.height)
        assertEquals(2.0, image.scale, 0.0)
        viewer.destroy()
        assertEquals(listOf(0, 255, 0, 255), image.image.rgba(20, 20))
        assertEquals(listOf(0, 0, 255, 255), image.image.rgba(20, 60))
        // The Linux render's bytes, pinned: not Apple's, which draws through
        // Core Graphics, but a change here is a change in what the engine draws.
        val crc = CRC32().apply { update(image.image.copyPixels()) }.value
        assertEquals(RENDER_CHECKSUM, crc)
    }

    private companion object {
        const val RENDER_CHECKSUM = 995_213_479L
    }
}
