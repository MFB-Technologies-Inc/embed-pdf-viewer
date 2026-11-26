package com.embedpdf.models

import com.embedpdf.Fixture
import com.embedpdf.RenderConfig
import com.embedpdf.RenderInvalidateScope
import com.embedpdf.ViewerConfig
import com.embedpdf.render
import com.embedpdf.started
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class RenderRevisionsTest {
    @get:Rule
    val main = MainThreadRule()

    @Test
    fun invalidatingAPageBumpsOnlyThatPage() = mainTest {
        val viewer = started(ViewerConfig(render = RenderConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val pages = viewer.pages(document)
        val render = viewer.render(document)
        val revisions = RenderRevisions(render)
        launch(start = CoroutineStart.UNDISPATCHED) { revisions.run() }
        // Subscribed by now, so an invalidation right after counts.
        render.invalidate(listOf(pages[0].page), RenderInvalidateScope.ANNOTATIONS)
        expectObserved({ revisions.revision(pages[0].page) }, 1L)
        assertEquals(0L, revisions.revision(pages[1].page))
        viewer.destroy()
    }

    @Test
    fun aCancelledRunStopsCountingWhileTheDocumentStaysOpen() = mainTest {
        val viewer = started(ViewerConfig(render = RenderConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val render = viewer.render(document)
        val revisions = RenderRevisions(render)
        val running = launch(start = CoroutineStart.UNDISPATCHED) { revisions.run() }
        expectObserved({ revisions.revision(page) }, 1L) { render.invalidate(listOf(page), RenderInvalidateScope.ANNOTATIONS) }
        running.cancel()
        running.join()
        render.invalidate(listOf(page), RenderInvalidateScope.ANNOTATIONS)
        delay(200)
        assertEquals(1L, revisions.revision(page))
        viewer.destroy()
    }

    @Test
    fun closingTheDocumentEndsTheRunAndKeepsTheCounts() = mainTest {
        val viewer = started(ViewerConfig(render = RenderConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val render = viewer.render(document)
        val revisions = RenderRevisions(render)
        val running = launch(start = CoroutineStart.UNDISPATCHED) { revisions.run() }
        expectObserved({ revisions.revision(page) }, 1L) { render.invalidate(listOf(page), RenderInvalidateScope.ANNOTATIONS) }
        viewer.close(document)
        assertTrue(ends(running))
        assertEquals(1L, revisions.revision(page))
        viewer.destroy()
    }
}
