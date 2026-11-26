package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RedactionTest {
    data class Opened(
        val viewer: PdfViewer,
        val document: DocumentInfo,
        val redaction: RedactionCapability,
        val search: SearchCapability,
    )

    private suspend fun opened(): Opened {
        val viewer = started(
            ViewerConfig(search = SearchConfig(), interaction = true, annotation = "", redaction = RedactionConfig()),
        )
        val document = viewer.open(Fixture.bytes("report"))
        viewer.annotations(document).refresh()
        return Opened(viewer, document, viewer.redaction(document), viewer.search(document))
    }

    @Test
    fun markingAnAreaIsPendingAndObserved() = viewerTest {
        val (viewer, document, redaction, _) = opened()
        val page = viewer.pages(document)[0].page
        val events = subscribed(redaction.observeEvents())
        val mark = redaction.markArea(Rect(x = 72.0, y = 72.0, width = 200.0, height = 20.0), page)
        assertEquals(listOf(mark), redaction.pending(page))
        events.nextMapped { it as? RedactionEventPendingChanged }
        redaction.unmark(listOf(mark.annotationRef))
        assertEquals(0, redaction.pendingCount(page))
        viewer.destroy()
    }

    @Test
    fun applyingMatchesRemovesTheText() = viewerTest {
        val (viewer, _, redaction, search) = opened()
        assertTrue(redaction.canApply())
        val before = search.findAll(SearchQuery(text = "Report"))
        val page = before.first().page
        val marks = redaction.markMatches(SearchQuery(text = "Report"), listOf(page))
        assertFalse(marks.isEmpty())
        val result = redaction.applyPages(listOf(page))
        assertEquals(RedactionPageStatus.APPLIED, result.results.first().status)
        val after = search.findAll(SearchQuery(text = "Report"))
        assertTrue(after.all { it.page != page })
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, redaction, _) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { redaction.pending() }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheStreamUnsubscribes() = viewerTest {
        val (viewer, _, redaction, _) = opened()
        val job = launch(start = CoroutineStart.UNDISPATCHED) { redaction.observeEvents().collect {} }
        delay(20)
        job.cancel()
        job.join()
        redaction.pending()
        viewer.destroy()
    }

    @Test
    fun applyAllCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, redaction, _) = opened()
        expectCancelledBeforeItStarts { redaction.applyAll() }
        redaction.pending()
        viewer.destroy()
    }

    @Test
    fun markMatchesCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, redaction, _) = opened()
        expectCancelledBeforeItStarts { redaction.markMatches(SearchQuery(text = "e")) }
        redaction.pending()
        viewer.destroy()
    }

    // One per member that takes a `CancelToken`, beyond `applyAll` and
    // `markMatches` above. Each is called in an already-cancelled coroutine.

    data class Marked(val viewer: PdfViewer, val redaction: RedactionCapability, val mark: AnnotationRef, val page: PageRef)

    /** A pending mark on the first page. */
    private suspend fun marked(): Marked {
        val (viewer, document, redaction, _) = opened()
        val page = viewer.pages(document)[0].page
        val mark = redaction.markArea(Rect(x = 72.0, y = 72.0, width = 200.0, height = 20.0), page)
        return Marked(viewer, redaction, mark.annotationRef, page)
    }

    @Test
    fun markSelectionCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, _, _) = marked()
        expectCancelledBeforeItStarts { redaction.markSelection() }
        viewer.destroy()
    }

    @Test
    fun markAreaCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, _, page) = marked()
        expectCancelledBeforeItStarts { redaction.markArea(Rect(x = 72.0, y = 200.0, width = 50.0, height = 20.0), page) }
        assertEquals(1, redaction.pendingCount(page))
        viewer.destroy()
    }

    @Test
    fun markPageCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, _, page) = marked()
        expectCancelledBeforeItStarts { redaction.markPage(page) }
        viewer.destroy()
    }

    @Test
    fun unmarkCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, mark, page) = marked()
        expectCancelledBeforeItStarts { redaction.unmark(listOf(mark)) }
        assertEquals(1, redaction.pendingCount(page))
        viewer.destroy()
    }

    @Test
    fun clearPendingCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, _, _) = marked()
        expectCancelledBeforeItStarts { redaction.clearPending() }
        viewer.destroy()
    }

    @Test
    fun updateLabelCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, mark, _) = marked()
        expectCancelledBeforeItStarts { redaction.updateLabel(mark, RedactionLabelPatch(overlayText = "X")) }
        viewer.destroy()
    }

    @Test
    fun applyCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, mark, page) = marked()
        expectCancelledBeforeItStarts { redaction.apply(listOf(mark)) }
        assertEquals(1, redaction.pendingCount(page))
        viewer.destroy()
    }

    @Test
    fun applyPagesCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, redaction, _, page) = marked()
        expectCancelledBeforeItStarts { redaction.applyPages(listOf(page)) }
        viewer.destroy()
    }

    @Test
    fun markingTheSelectionCreatesOneMarkPerPage() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, selection = true, annotation = "", redaction = RedactionConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        viewer.annotations(document).refresh()
        val page = viewer.pages(document)[0].page
        val selection = viewer.selection(document)
        selection.ensureLoaded(page)
        selection.selectPage(page)
        assertEquals(1, viewer.redaction(document).markSelection().size)
        viewer.destroy()
    }

    /** The companion refusals: the selection and search plugins left out. */
    @Test
    fun markSelectionAndMarkMatchesNeedTheirCompanionPlugins() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", redaction = RedactionConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val redaction = viewer.redaction(document)
        expectCode(PdfException.Code.UNSUPPORTED) { redaction.markSelection() }
        expectCode(PdfException.Code.UNSUPPORTED) { redaction.markMatches(SearchQuery(text = "Report")) }
        viewer.destroy()
    }
}
