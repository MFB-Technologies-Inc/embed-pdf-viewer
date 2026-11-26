package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class SelectionTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val selection: SelectionCapability, val page: PageRef)

    private suspend fun opened(search: Boolean = false): Opened {
        val viewer = started(
            ViewerConfig(search = if (search) SearchConfig() else null, interaction = true, selection = true),
        )
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        return Opened(viewer, document, viewer.selection(document), page)
    }

    @Test
    fun selectingAPageReadsItsText() = viewerTest {
        val (viewer, _, selection, page) = opened()
        selection.selectPage(page)
        // report.pdf opens with the title "Trace-based Just-in-Time Type
        // Specialization for Dynamic Languages".
        assertTrue(selection.readText().contains("Dynamic"))
        assertFalse(selection.rects(page).isEmpty())
        viewer.destroy()
    }

    @Test
    fun aSearchHitSelectsItsCharacters() = viewerTest {
        val (viewer, document, selection, _) = opened(search = true)
        val hit = viewer.search(document).findAll(SearchQuery(text = "Report")).first()
        selection.select(hit)
        // Search matches case-insensitively by default, so compare that way.
        assertEquals("report", selection.readText().lowercase())
        viewer.destroy()
    }

    @Test
    fun selectAndClearAreObserved() = viewerTest {
        val (viewer, _, selection, page) = opened()
        val events = subscribed(selection.observeEvents())
        selection.selectPage(page)
        selection.clear()
        val kinds = mutableListOf<String>()
        while ("cleared" !in kinds) {
            kinds += events.nextMapped {
                when (it) {
                    is SelectionEventChanged -> "changed"
                    is SelectionEventCommitted -> "committed"
                    is SelectionEventCleared -> "cleared"
                }
            }
        }
        assertEquals("changed", kinds.first())
        viewer.destroy()
    }

    @Test
    fun readTextCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, selection, _) = opened()
        selection.selectAll()
        expectCancelledBeforeItStarts { selection.readText() }
        assertTrue(selection.readText().isNotEmpty())
        viewer.destroy()
    }

    @Test
    fun readTextInRangeCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, selection, page) = opened()
        val range = SelectionTextRange(
            start = SelectionTextPosition(page = page, index = 0),
            end = SelectionTextPosition(page = page, index = 20),
        )
        val reading = async(start = CoroutineStart.UNDISPATCHED) { selection.readText(range) }
        reading.cancel()
        try {
            reading.await()
            fail("a cancelled read finished")
        } catch (e: CancellationException) {
            // The caller sees its own cancellation.
        }
        assertEquals(20, selection.readText(range).length)
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, selection, _) = opened()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { selection.hasSelection() }
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, selection, page) = opened()
        val collector = collecting(selection.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        selection.selectPage(page)
        viewer.destroy()
    }
}
