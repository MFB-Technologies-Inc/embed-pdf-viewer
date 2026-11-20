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

class SearchTest {
    private suspend fun opened(): Triple<PdfViewer, DocumentInfo, SearchCapability> {
        val viewer = started(ViewerConfig(search = SearchConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        return Triple(viewer, document, viewer.search(document))
    }

    @Test
    fun aQueryFindsHitsAndNavigatesThem() = viewerTest {
        val (viewer, _, search) = opened()
        val result = search.search(SearchQuery(text = "the"))
        assertEquals(SearchResultStatus.COMPLETE, result.status)
        assertTrue(result.hitCount > 1u)
        assertEquals(SearchStatus.COMPLETE, search.status())
        // The first hit becomes active as the hits arrive; navigating moves on.
        assertEquals(0, search.activeHitIndex())
        val next = search.nextHit()
        assertEquals(1, search.activeHitIndex())
        assertEquals(next, search.activeHit())
        assertTrue(next?.snippet?.matched?.contains("the", ignoreCase = true) == true)
        assertEquals(result.hitCount.toInt(), search.hitCount())
        viewer.destroy()
    }

    @Test
    fun completionIsObserved() = viewerTest {
        val (viewer, _, search) = opened()
        val events = subscribed(search.observeEvents())
        val result = search.search(SearchQuery(text = "the"))
        val completed = events.nextMapped { it as? SearchEventCompleted }
        assertEquals(result.hitCount, completed.event.hitCount)
        viewer.destroy()
    }

    @Test
    fun findAllCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, search) = opened()
        val finding = async(start = CoroutineStart.UNDISPATCHED) { search.findAll(SearchQuery(text = "e")) }
        finding.cancel()
        try {
            finding.await()
            fail("a cancelled findAll finished")
        } catch (e: CancellationException) {
            // The caller sees its own cancellation.
        }
        // The capability still answers after a cancelled call.
        assertTrue(search.findAll(SearchQuery(text = "Report")).isNotEmpty())
        viewer.destroy()
    }

    @Test
    fun searchCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, search) = opened()
        expectCancelledBeforeItStarts { search.search(SearchQuery(text = "the")) }
        viewer.destroy()
    }

    @Test
    fun refreshCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, search) = opened()
        search.search(SearchQuery(text = "the"))
        expectCancelledBeforeItStarts { search.refresh() }
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, search) = opened()
        val collector = collecting(search.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        search.search(SearchQuery(text = "the"))
        viewer.destroy()
    }

    /** May search, may not copy: the scope where `snippets` changes answers. */
    private suspend fun openedWithoutCopy(): Pair<PdfViewer, SearchCapability> {
        val viewer = started(ViewerConfig(scope = listOf("doc.open", "doc.text.search"), search = SearchConfig()))
        return viewer to viewer.search(viewer.open(Fixture.bytes("report")))
    }

    @Test
    fun canSearchLeftOutIsWithoutSnippets() = viewerTest {
        val (viewer, search) = openedWithoutCopy()
        assertEquals(
            listOf(true, true, false),
            listOf(search.canSearch(), search.canSearch(snippets = false), search.canSearch(snippets = true)),
        )
        viewer.destroy()
    }

    @Test
    fun findAllLeftOutCarriesSnippetsWhenTheSessionMayCopy() = viewerTest {
        val (viewer, _, search) = opened()
        val omitted = search.findAll(SearchQuery(text = "Report"))
        val always = search.findAll(SearchQuery(text = "Report"), snippets = ContentInclusion.ALWAYS)
        val never = search.findAll(SearchQuery(text = "Report"), snippets = ContentInclusion.NEVER)
        assertFalse(omitted.isEmpty())
        assertEquals(always, omitted)
        assertTrue(never.all { it.snippet == null })
        assertTrue(omitted.all { it.snippet != null })
        viewer.destroy()
    }

    @Test
    fun findAllLeftOutDropsSnippetsWhenTheSessionMayNotCopy() = viewerTest {
        val (viewer, search) = openedWithoutCopy()
        val omitted = search.findAll(SearchQuery(text = "Report"))
        val never = search.findAll(SearchQuery(text = "Report"), snippets = ContentInclusion.NEVER)
        assertFalse(omitted.isEmpty())
        assertEquals(never, omitted)
        expectCode(PdfException.Code.PERMISSION_DENIED) {
            search.findAll(SearchQuery(text = "Report"), snippets = ContentInclusion.ALWAYS)
        }
        viewer.destroy()
    }

    @Test
    fun aHitIndexBelowZeroIsInvalidInput() = viewerTest {
        val (viewer, _, search) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) { search.goToHit(-1) }
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, search) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { search.search(SearchQuery(text = "the")) }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }
}
