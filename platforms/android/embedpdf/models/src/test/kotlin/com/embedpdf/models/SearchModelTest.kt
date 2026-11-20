package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.PageEditPlacementEnd
import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.RenderConfig
import com.embedpdf.SearchCapability
import com.embedpdf.SearchConfig
import com.embedpdf.SearchHit
import com.embedpdf.SearchQuery
import com.embedpdf.SearchStatus
import com.embedpdf.Size
import com.embedpdf.StageConfig
import com.embedpdf.StageGoToOptions
import com.embedpdf.StageScrollBehavior
import com.embedpdf.ViewerConfig
import com.embedpdf.pageEdit
import com.embedpdf.search
import com.embedpdf.stage
import com.embedpdf.started
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Rule
import org.junit.Test
import java.text.NumberFormat

class SearchModelTest {
    @get:Rule
    val main = MainThreadRule()

    private data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val search: SearchCapability, val model: SearchModel)

    private suspend fun opened(scope: List<String>? = null, pageEdit: Boolean = true): Opened {
        val viewer = started(ViewerConfig(scope = scope, search = SearchConfig(), pageEdit = pageEdit))
        val document = viewer.open(Fixture.bytes("report"))
        val search = viewer.search(document)
        return Opened(viewer, document, search, SearchModel(search))
    }

    private fun summary(position: Int, count: Int): String {
        val numbers = NumberFormat.getIntegerInstance()
        return "${numbers.format(position)} of ${numbers.format(count)}"
    }

    /**
     * Moves [page] to the end and waits for the search plugin's own rerun of
     * the query to finish. The plugin starts it 250 ms after the edit
     * (`RERUN_DELAY_MS` in `crates/plugins/search/src/controller.rs`).
     */
    private suspend fun moveToEndAndAwaitRerun(page: PageRef, opened: Opened, before: List<SearchHit>) {
        opened.viewer.pageEdit(opened.document).movePages(listOf(page), PageEditPlacementEnd)
        val deadline = System.nanoTime() + 10_000_000_000
        while (System.nanoTime() < deadline) {
            if (opened.search.status() == SearchStatus.COMPLETE && opened.search.hits() != before) return
            delay(20)
        }
        fail("the plugin's rerun after the move did not finish")
    }

    @Test
    fun revealingTheActiveHitLeavesItActive() = mainTest {
        val viewer = started(ViewerConfig(search = SearchConfig(), render = RenderConfig(), stage = StageConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val stage = viewer.stage(document)
        stage.setViewportSize(Size(400.0, 600.0))
        val model = SearchModel(viewer.search(document))
        model.text = "the"
        model.searchSubmitted()
        model.nextButtonTapped()
        assertEquals(1, model.activeHitIndex.value)
        val active = checkNotNull(model.activeHit.value)
        // The step already revealed the hit, so scroll away from it first.
        val last = viewer.pages(document).last()
        assertNotEquals(active.page, last.page)
        stage.goToPage(last.page, StageGoToOptions(behavior = StageScrollBehavior.INSTANT))
        assertFalse(stage.isPageVisible(active.page))
        model.revealActiveHit()
        // The search settings reveal with a smooth scroll.
        expectObserved({ stage.isPageVisible(active.page) }, true)
        // Safe to repeat.
        model.revealActiveHit()
        assertNull(model.error.value)
        assertEquals(1, model.activeHitIndex.value)
        assertEquals(1, viewer.search(document).activeHitIndex())
        viewer.destroy()
    }

    @Test
    fun revealingWithNoActiveHitOrNoStageIsNotAnError() = mainTest {
        val (viewer, _, _, model) = opened()
        model.revealActiveHit()
        assertNull(model.error.value)
        model.text = "the"
        model.searchSubmitted()
        model.revealActiveHit()
        assertNull(model.error.value)
        assertEquals(0, model.activeHitIndex.value)
        viewer.destroy()
    }

    @Test
    fun revealingOnAClosedDocumentSetsTheError() = mainTest {
        val (viewer, document, _, model) = opened()
        viewer.close(document)
        model.revealActiveHit()
        assertNotNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun resubmittingAfterAnEditRereadsHitsWhoseCountDidNotChange() = mainTest {
        val opened = opened()
        val model = opened.model
        model.text = "the"
        model.searchSubmitted()
        val before = model.hits.value
        moveToEndAndAwaitRerun(before.first().page, opened, before)
        model.searchSubmitted()
        val kernel = opened.search.hits()
        // The rerun finds as many hits, at other positions.
        assertEquals(before.size, kernel.size)
        assertNotEquals(before, kernel)
        assertEquals(kernel, model.hits.value)
        opened.viewer.destroy()
    }

    @Test
    fun aRefreshThatOvertakesTheSubmittedOneStillRereadsHits() = mainTest {
        val opened = opened()
        val model = opened.model
        model.text = "the"
        model.searchSubmitted()
        val before = model.hits.value
        moveToEndAndAwaitRerun(before.first().page, opened, before)
        // A revision lands while the submitted refresh is reading: the newer
        // refresh supersedes it and must still reread the hit list.
        var overtaken = false
        model.snapshots.readFinished = {
            if (!overtaken) {
                overtaken = true
                model.refresh()
            }
        }
        model.searchSubmitted()
        model.snapshots.readFinished = null
        assertTrue(overtaken)
        val kernel = opened.search.hits()
        assertEquals(before.size, kernel.size)
        assertNotEquals(before, kernel)
        assertEquals(kernel, model.hits.value)
        opened.viewer.destroy()
    }

    @Test
    fun closingTheDocumentEndsTheRunAndEmptiesTheModel() = mainTest {
        val (viewer, document, _, model) = opened()
        val observing = launch { model.run() }
        model.text = "the"
        model.searchSubmitted()
        assertTrue(model.hitCount.value > 1)
        viewer.close(document)
        assertTrue(ends(observing))
        assertEquals(emptyList<SearchHit>(), model.hits.value)
        assertEquals(0, model.hitCount.value)
        assertNull(model.activeHitIndex.value)
        assertEquals(SearchStatus.IDLE, model.status.value)
        assertEquals(SearchModel.EMPTY_PROGRESS, model.progress.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        viewer.destroy()
    }

    @Test
    fun aRefreshNotifiesCollectorsOfEachProperty() = mainTest {
        val (viewer, _, search, model) = opened()
        search.search(SearchQuery(text = "the"))
        search.nextHit()
        val page = search.hits().first().page
        val flows = listOf(model.status, model.activeHitIndex, model.hitCount, model.progress)
        val before = flows.map { it.value }
        model.refresh()
        assertEquals(listOf(true, true, true, true), flows.indices.map { flows[it].value !== before[it] })
        assertTrue(model.hitCount(page) > 0)
        viewer.destroy()
    }

    @Test
    fun aBackgroundRefreshKeepsAnActionsError() = mainTest {
        // Without doc.text.search the search itself is refused, while the session reads still answer.
        val (viewer, _, _, model) = opened(scope = listOf("doc.open", "doc.render"), pageEdit = false)
        model.text = "the"
        model.searchSubmitted()
        val refused = checkNotNull(model.error.value)
        model.refresh()
        assertEquals(refused, model.error.value)
        assertEquals(SearchStatus.ERROR, model.status.value)
        viewer.destroy()
    }

    @Test
    fun clearingAfterARefusedSearchLeavesTheIdleStatusAndNoError() = mainTest {
        val (viewer, _, _, model) = opened(scope = listOf("doc.open", "doc.render"), pageEdit = false)
        model.text = "the"
        model.searchSubmitted()
        assertEquals(SearchStatus.ERROR, model.status.value)
        assertNotNull(model.error.value)
        model.clearButtonTapped()
        assertEquals(SearchStatus.IDLE, model.status.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun submittingASearchFillsHitsAndNavigationMovesTheActiveHit() = mainTest {
        val (viewer, _, _, model) = opened()
        model.text = "the"
        model.searchSubmitted()
        assertEquals(SearchStatus.COMPLETE, model.status.value)
        assertTrue(model.hits.value.size > 1)
        assertEquals(0, model.activeHitIndex.value)
        model.nextButtonTapped()
        assertEquals(1, model.activeHitIndex.value)
        model.previousButtonTapped()
        assertEquals(0, model.activeHitIndex.value)
        viewer.destroy()
    }

    @Test
    fun theSummaryGroupsALargeCountAsTheLocaleDoes() {
        assertEquals(summary(1234, 12345), SearchModel.summary(1233, 12345, SearchStatus.COMPLETE))
    }

    @Test
    fun givenStringsWordTheSummary() {
        val strings = object : SearchStrings {
            override fun searchSummary(index: Int, count: Int, searching: Boolean): String =
                "$index / $count" + if (searching) " …" else ""
        }
        assertEquals("3 / 12 …", SearchModel.summary(2, 12, SearchStatus.SEARCHING, strings))
        assertEquals("3 / 12", SearchModel.summary(2, 12, SearchStatus.COMPLETE, strings))
        assertNull(SearchModel.summary(null, 12, SearchStatus.COMPLETE, strings))
    }

    @Test
    fun theSummaryMarksACountThatIsStillGrowing() {
        assertEquals(summary(3, 12) + "+", SearchModel.summary(2, 12, SearchStatus.SEARCHING))
        assertEquals(summary(3, 12), SearchModel.summary(2, 12, SearchStatus.COMPLETE))
        assertEquals(summary(3, 12), SearchModel.summary(2, 12, SearchStatus.CANCELLED))
        assertNull(SearchModel.summary(null, 12, SearchStatus.COMPLETE))
        assertNull(SearchModel.summary(null, 0, SearchStatus.SEARCHING))
        // The index and the count come from separate reads and can disagree for one refresh.
        assertNull(SearchModel.summary(12, 12, SearchStatus.COMPLETE))
    }

    @Test
    fun clearIsAvailableForTextHitsOrASessionThatIsNotIdle() = mainTest {
        val (viewer, _, _, model) = opened()
        assertFalse(model.canClear.value)
        model.text = "trace"
        assertTrue(model.canClear.value)
        model.searchSubmitted()
        // The field emptied by hand leaves the hits to clear.
        model.text = ""
        assertTrue(model.hitCount.value > 0)
        assertTrue(model.canClear.value)
        model.clearButtonTapped()
        assertFalse(model.canClear.value)
        viewer.destroy()
    }

    @Test
    fun clearIsAvailableAfterARefusedSearchWithAnEmptyField() = mainTest {
        val (viewer, _, _, model) = opened(scope = listOf("doc.open", "doc.render"), pageEdit = false)
        model.text = "the"
        model.searchSubmitted()
        model.text = ""
        assertEquals(SearchStatus.ERROR, model.status.value)
        assertEquals(0, model.hitCount.value)
        assertTrue(model.canClear.value)
        model.clearButtonTapped()
        assertFalse(model.canClear.value)
        viewer.destroy()
    }

    @Test
    fun theActiveHitAndSummaryFollowNavigation() = mainTest {
        val (viewer, _, _, model) = opened()
        assertNull(model.activeHit.value)
        assertNull(model.summary.value)
        model.text = "trace"
        model.searchSubmitted()
        assertEquals(SearchStatus.COMPLETE, model.status.value)
        assertTrue(model.hitCount.value > 1)
        assertEquals(model.hits.value[0], model.activeHit.value)
        assertEquals(summary(1, model.hitCount.value), model.summary.value)
        model.nextButtonTapped()
        assertEquals(model.hits.value[1], model.activeHit.value)
        assertEquals(summary(2, model.hitCount.value), model.summary.value)
        model.clearButtonTapped()
        assertNull(model.activeHit.value)
        assertNull(model.summary.value)
        viewer.destroy()
    }

    @Test
    fun activeHitChangesFromTheCapabilityReachTheModel() = mainTest {
        val (viewer, _, search, model) = opened()
        model.text = "the"
        model.searchSubmitted()
        val observing = launch { model.run() }
        search.goToHit(2)
        expectObserved(model.activeHitIndex, 2)
        observing.cancel()
        viewer.destroy()
    }

    @Test
    fun aNewModelHasNoSearch() = mainTest {
        val (viewer, _, _, model) = opened()
        assertEquals(SearchStatus.IDLE, model.status.value)
        assertNull(model.activeHitIndex.value)
        assertEquals(0, model.hitCount.value)
        assertEquals(SearchModel.EMPTY_PROGRESS, model.progress.value)
        assertEquals(emptyList<SearchHit>(), model.hits.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun aSearchRunElsewhereReachesTheObservingModel() = mainTest {
        val (viewer, _, search, model) = opened()
        val observing = launch { model.run() }
        search.search(SearchQuery(text = "the"))
        val hitCount = search.hitCount()
        assertTrue(hitCount > 1)
        expectObserved(model.hitCount, hitCount)
        expectObserved(model.status, search.status())
        assertEquals(search.progress(), model.progress.value)
        assertEquals(search.activeHitIndex(), model.activeHitIndex.value)
        assertEquals(hitCount, model.hits.value.size)
        val page = model.hits.value.first().page
        assertEquals(search.hitCount(page), model.hitCount(page))
        expectObserved(model.hitCount, 0) { search.clear() }
        assertEquals(0, model.hitCount(page))
        assertEquals(emptyList<SearchHit>(), model.hits.value)
        observing.cancel()
        viewer.destroy()
    }

    @Test
    fun anOlderReadCannotOverwriteANewerOne() = mainTest {
        val (viewer, _, _, model) = opened()
        val (reached, release) = holdingFirstRead(model.snapshots)
        val stale = async { model.refresh() }
        reached.await()
        model.text = "the"
        model.searchSubmitted()
        val hitCount = model.hitCount.value
        assertTrue(hitCount > 1)
        release.complete(Unit)
        stale.await()
        assertEquals(hitCount, model.hitCount.value)
        assertEquals(SearchStatus.COMPLETE, model.status.value)
        assertEquals(0, model.activeHitIndex.value)
        viewer.destroy()
    }
}
