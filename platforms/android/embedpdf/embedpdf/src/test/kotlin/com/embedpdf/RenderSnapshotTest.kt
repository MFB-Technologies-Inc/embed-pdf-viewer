package com.embedpdf

import com.embedpdf.RenderTest.Companion.rgba
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.zip.CRC32

class RenderSnapshotTest {
    @Test
    fun metadataChangesAndOwnedPixelsSurviveClearAndClose() = viewerTest {
        val viewer = started(ViewerConfig(render = RenderConfig(), interaction = true))
        val document = viewer.open(Fixture.bytes("colors"))
        val page = viewer.pages(document)[0].page
        val render = viewer.render(document)
        val interaction = viewer.interaction(document)
        assertTrue(render.canSnapshot())
        assertNull(render.snapshot())
        val changes = subscribed(render.observeSnapshotChanges())
        interaction.activateTool(ToolId.SNAPSHOT)
        val from = Point(20.0, 0.0)
        val to = Point(60.0, 20.0)
        interaction.dispatchPointer(
            PointerSample(
                phase = PointerPhase.DOWN,
                viewport = from,
                page = PointerPageHit(page = page, point = from, scale = 1.0, rotation = 90.0),
                modifiers = Modifiers(),
            ),
        )
        interaction.dispatchPointer(
            PointerSample(
                phase = PointerPhase.UP,
                viewport = to,
                page = PointerPageHit(page = page, point = to, scale = 1.0, rotation = 90.0),
                modifiers = Modifiers(),
            ),
        )
        val snapshot = render.snapshot() ?: error("no snapshot after the gesture")
        assertEquals(page, snapshot.page)
        assertEquals(Rect(20.0, 0.0, 40.0, 20.0), snapshot.rect)
        assertEquals(snapshot.rect, render.snapshotBox(page))
        val captured = withTimeout(5_000) { changes.receive() } ?: error("the first change was a clear")
        assertEquals(snapshot.rect, captured.rect)
        expectCancelledBeforeItStarts { snapshot.image() }
        val pixels = captured.image()
        assertEquals(40, pixels.width)
        assertEquals(80, pixels.height)
        assertEquals(2.0, pixels.scale, 0.0)
        val before = pixels.image.copyPixels()
        render.clearSnapshot()
        assertNull(render.snapshot())
        assertNull(withTimeout(5_000) { changes.receive() })
        viewer.close(document)
        assertEquals(emptyList<RenderSnapshot?>(), changes.drain())
        expectCode(PdfException.Code.INSTANCE_CLOSED) { snapshot.image() }
        viewer.destroy()
        assertArrayEquals(before, pixels.image.copyPixels())
    }

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
