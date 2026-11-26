package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class RenderTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val page: PageRef, val render: RenderCapability)

    companion object {
        suspend fun opened(): Opened {
            val viewer = started(ViewerConfig(render = RenderConfig()))
            val document = viewer.open(Fixture.bytes("colors"))
            val page = viewer.pages(document)[0].page
            return Opened(viewer, document, page, viewer.render(document))
        }

        /** The pixel at (x, y) as `[r, g, b, a]`. */
        fun PageImage.rgba(x: Int, y: Int): List<Int> {
            val color = pixel(x, y)
            return listOf(color shr 16 and 0xff, color shr 8 and 0xff, color and 0xff, color ushr 24)
        }
    }

    @Test
    fun aPageRendersToPixelsThatOutliveTheViewer() = viewerTest {
        val (viewer, _, page, render) = opened()
        val rendered = render.renderPage(page, RenderPageOptions(scale = 1.0))
        assertTrue(rendered.width > 0 && rendered.height > 0)
        assertEquals(1.0, rendered.scale, 0.0)
        assertEquals(rendered.width, rendered.image.width)
        viewer.destroy()
        val survivor = rendered.image
        assertTrue(survivor.width > 0)
        assertEquals(survivor.stride * survivor.height, survivor.copyPixels().size)
    }

    /**
     * The `colors` fixture is an 80 by 20 point page of four 20-point squares:
     * red, green, blue, then (0.2, 0.4, 0.6). A red and blue swap anywhere
     * between the engine's bytes and the color ints would turn the first
     * square blue.
     */
    @Test
    fun redStaysRed() = viewerTest {
        val (viewer, _, page, render) = opened()
        val rendered = render.renderPage(page, RenderPageOptions(scale = 0.5))
        assertEquals(40, rendered.width)
        assertEquals(10, rendered.height)
        assertEquals(listOf(255, 0, 0, 255), rendered.image.rgba(5, 5))
        assertEquals(listOf(0, 255, 0, 255), rendered.image.rgba(15, 5))
        assertEquals(listOf(0, 0, 255, 255), rendered.image.rgba(25, 5))
        assertEquals(listOf(51, 102, 153, 255), rendered.image.rgba(35, 5))
        viewer.destroy()
    }

    @Test
    fun renderPageCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, page, render) = opened()
        expectCancelledBeforeItStarts { render.renderPage(page, RenderPageOptions(scale = 1.0)) }
        viewer.destroy()
    }

    @Test
    fun renderThumbnailCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, page, render) = opened()
        expectCancelledBeforeItStarts { render.renderThumbnail(page) }
        viewer.destroy()
    }

    @Test
    fun renderPagesCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, page, render) = opened()
        expectCancelledBeforeItStarts { render.renderPages(listOf(page), RenderPageOptions(scale = 1.0)) }
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, page, render) = opened()
        val collector = collecting(render.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        // A render after the drop still answers (and emits to no one).
        render.renderPage(page, RenderPageOptions(scale = 0.5))
        viewer.destroy()
    }

    @Test
    fun aBadScaleIsRefused() = viewerTest {
        val (viewer, _, page, render) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) { render.renderPage(page, RenderPageOptions(scale = 0.0)) }
        viewer.destroy()
    }

    @Test
    fun aNegativeConcurrencyIsInvalidInput() = viewerTest {
        val (viewer, _, page, render) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) { render.renderPages(listOf(page), concurrency = -1) }
        viewer.destroy()
    }

    @Test
    fun aThumbnailAndABatchRender() = viewerTest {
        val (viewer, document, page, render) = opened()
        assertTrue(render.canRenderThumbnail())
        assertTrue(render.canRender())
        val thumbnail = render.renderThumbnail(page)
        // The whole page, 512 pixels on its long side.
        assertEquals(512, maxOf(thumbnail.width, thumbnail.height))
        val again = render.renderSource(page, scale = 3.0, thumbnail = true)
        assertEquals(thumbnail.width, again.width)
        val pages = viewer.pages(document).map { it.page }
        val batch = render.renderPages(pages, RenderPageOptions(width = 30.0))
        assertEquals(pages, batch.applied.map { it.page })
        assertEquals(pages.map { 30 }, batch.applied.map { it.rendered.width })
        assertEquals(emptyList<RenderBatchFailure>(), batch.failed)
        viewer.destroy()
    }

    @Test
    fun invalidatingIsObservedAndBumpsTheEpoch() = viewerTest {
        val (viewer, _, page, render) = opened()
        val before = render.renderEpoch(page)
        val events = subscribed(render.observeEvents())
        render.invalidate(listOf(page))
        val invalidated = events.nextMapped { it as? RenderEventInvalidated }
        assertEquals(listOf(page), invalidated.pages)
        assertEquals(RenderInvalidateScope.CONTENT, invalidated.scope)
        assertEquals(before + 1, render.renderEpoch(page))
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, page, render) = opened()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { render.renderPage(page) }
        viewer.destroy()
    }
}
