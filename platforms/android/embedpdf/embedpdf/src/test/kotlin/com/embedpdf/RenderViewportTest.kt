package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class RenderViewportTest {
    @Test
    fun renderSourceMatchesItsSourceKey() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        val a = render.sourceKey(page, scale = 2.0)
        val b = render.sourceKey(page, scale = 2.0, includeAnnotations = ContentInclusion.ALWAYS)
        assertEquals(a, b)
        assertTrue(render.renderSource(page, scale = 2.0).width > 0)
        viewer.destroy()
    }

    @Test
    fun renderSourceCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        val rendering = async(start = CoroutineStart.UNDISPATCHED) { render.renderSource(page, scale = 8.0) }
        rendering.cancel()
        try {
            rendering.await()
        } catch (e: CancellationException) {
            // Cancelled, or finished first.
        }
        render.renderSource(page, scale = 1.0)
        viewer.destroy()
    }

    @Test
    fun aViewDemandPlansAndDisposes() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        render.paintSettings()
        val view = render.viewDemand("main")
        // The page is 80 points wide: the full-page raster meets an 80-pixel
        // demand, so the tile plane stays disengaged whether or not tiles are on.
        view.setDemand(page, RenderPageViewDemand(desiredDeviceWidth = 80.0))
        val plan = view.plan(page)
        assertFalse(plan.engaged)
        assertEquals(emptyList<RenderTileSource>(), plan.paint)
        assertNull(view.tileImage(page, key = "no-such-key"))
        view.release(page)
        view.dispose()
        viewer.destroy()
    }

    @Test
    fun layersLeftOutDrawWhatTheSessionMayRead() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        val omitted = render.sourceKey(page, scale = 2.0)
        val always = render.sourceKey(
            page, scale = 2.0, includeAnnotations = ContentInclusion.ALWAYS, includeFormFields = ContentInclusion.ALWAYS,
        )
        val never = render.sourceKey(page, scale = 2.0, includeAnnotations = ContentInclusion.NEVER)
        assertEquals(always, omitted)
        assertNotEquals(never, omitted)
        // Form fields left out follow annotations turned off.
        val fieldsNever = render.sourceKey(
            page, scale = 2.0, includeAnnotations = ContentInclusion.NEVER, includeFormFields = ContentInclusion.NEVER,
        )
        assertEquals(fieldsNever, never)
        viewer.destroy()
    }

    @Test
    fun layersLeftOutAreOmittedWhenTheSessionMayNotReadThem() = viewerTest {
        val viewer = started(ViewerConfig(scope = listOf("doc.open", "doc.render"), render = RenderConfig()))
        val document = viewer.open(Fixture.bytes("colors"))
        val page = viewer.pages(document)[0].page
        val render = viewer.render(document)
        val omitted = render.sourceKey(page, scale = 2.0)
        val never = render.sourceKey(
            page, scale = 2.0, includeAnnotations = ContentInclusion.NEVER, includeFormFields = ContentInclusion.NEVER,
        )
        assertEquals(never, omitted)
        assertTrue(render.renderSource(page, scale = 2.0).width > 0)
        expectCode(PdfException.Code.PERMISSION_DENIED) {
            render.renderSource(page, scale = 2.0, includeAnnotations = ContentInclusion.ALWAYS)
        }
        expectCode(PdfException.Code.PERMISSION_DENIED) {
            render.renderThumbnail(page, includeAnnotations = ContentInclusion.ALWAYS)
        }
        viewer.destroy()
    }

    @Test
    fun thumbnailLeftOutIsTheRaster() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        val omitted = render.sourceKey(page, scale = 2.0)
        assertEquals(render.sourceKey(page, scale = 2.0, thumbnail = false), omitted)
        assertNotEquals(render.sourceKey(page, scale = 2.0, thumbnail = true), omitted)
        viewer.destroy()
    }

    @Test
    fun theViewportAndPolicyAnswer() = viewerTest {
        val (viewer, _, page, render) = RenderTest.opened()
        render.renderPolicy()
        render.conformViewport(page, scale = 2.0)
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsViewDemandThrows() = viewerTest {
        val (viewer, document, page, render) = RenderTest.opened()
        val view = render.viewDemand("main")
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { view.plan(page) }
        viewer.destroy()
    }
}
