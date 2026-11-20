package com.embedpdf.models

import com.embedpdf.Fixture
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.PdfViewer
import com.embedpdf.Point
import com.embedpdf.Rect
import com.embedpdf.SelectionCapability
import com.embedpdf.ViewerConfig
import com.embedpdf.selection
import com.embedpdf.started
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Rule
import org.junit.Test

class TextSelectionModelTest {
    @get:Rule
    val main = MainThreadRule()

    private class Opened(val viewer: PdfViewer, val page: PageInfo, val selection: SelectionCapability, val model: TextSelectionModel)

    private val missingPage = PageRef(pageObjectNumber = UInt.MAX_VALUE)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(interaction = true, selection = true))
        val document = viewer.open(Fixture.bytes("report"))
        val selection = viewer.selection(document)
        return Opened(viewer, viewer.pages(document).first(), selection, TextSelectionModel(selection))
    }

    /** The model with its `run()` going, subscribed by the time this returns. */
    private fun CoroutineScope.running(model: TextSelectionModel): Job = launch(start = CoroutineStart.UNDISPATCHED) { model.run() }

    private suspend fun pointOverText(page: PageInfo, selection: SelectionCapability): Point {
        selection.ensureLoaded(page.page)
        selection.selectPage(page.page)
        val first = selection.rects(page.page).first()
        selection.clear()
        return Point(first.x + first.width / 2, first.y + first.height / 2)
    }

    private suspend fun pointOffText(page: PageInfo, selection: SelectionCapability): Point {
        selection.ensureLoaded(page.page)
        selection.selectPage(page.page)
        val rects = selection.rects(page.page)
        selection.clear()
        val candidates = listOf(
            Point(2.0, 2.0), Point(page.width - 2, 2.0), Point(2.0, page.height - 2), Point(page.width - 2, page.height - 2),
        )
        for (point in candidates) {
            val inRect = rects.any { point.x >= it.x && point.x <= it.x + it.width && point.y >= it.y && point.y <= it.y + it.height }
            if (!inRect && !selection.isOverText(point, page.page)) return point
        }
        fail("no point off text on the page")
        throw AssertionError()
    }

    private suspend fun selectionRects(start: Point, end: Point, page: PageInfo, selection: SelectionCapability): List<Rect> {
        selection.clear()
        selection.ensureLoaded(page.page)
        assertTrue(selection.beginGesture(start, page.page))
        selection.extend(end, page.page)
        selection.endGesture()
        return selection.rects(page.page)
    }

    @Test
    fun draggingAcrossTextSelectsIt() = mainTest {
        val o = opened()
        running(o.model)
        val start = pointOverText(o.page, o.selection)
        expectObserved(o.model.isSelecting, true) { o.model.dragBegan(start, o.page.page) }
        o.model.dragChanged(Point(start.x + 120, start.y), o.page.page)
        assertTrue(o.model.dragEnded())
        assertFalse(o.model.isSelecting.value)
        val engineRects = o.selection.rects(o.page.page)
        assertTrue(engineRects.isNotEmpty())
        assertEquals(engineRects, o.model.rects.value[o.page.page])
        o.viewer.destroy()
    }

    /** Review focus: points that arrive while an extension is queued collapse into one. */
    @Test
    fun pointsQueuedBehindOneExtensionCollapseIntoIt() = mainTest {
        val o = opened()
        running(o.model)
        val start = pointOverText(o.page, o.selection)
        expectObserved(o.model.isSelecting, true) { o.model.dragBegan(start, o.page.page) }
        val end = Point(start.x + 160, start.y)
        o.model.dragChanged(Point(start.x + 20, start.y), o.page.page)
        o.model.dragChanged(Point(start.x + 40, start.y), o.page.page)
        o.model.dragChanged(end, o.page.page)
        o.model.dragEnded()
        assertEquals(1, o.model.extensionCount)
        val collapsed = o.model.rects.value[o.page.page]
        assertEquals(selectionRects(start, end, o.page, o.selection), collapsed)
        o.viewer.destroy()
    }

    /** Review focus: `dragEnded()` returns after the last extension lands, with or without `run()`. */
    @Test
    fun changesAndEndsMadeBeforeTheBeginLandsRunAfterIt() = mainTest {
        val o = opened()
        val start = pointOverText(o.page, o.selection)
        // No `run()` and no suspension between the three calls: the end sends them all.
        o.model.dragBegan(start, o.page.page)
        o.model.dragChanged(Point(start.x + 120, start.y), o.page.page)
        assertTrue(o.model.dragEnded())
        assertFalse(o.model.isSelecting.value)
        assertFalse(o.selection.isSelecting())
        assertEquals(1, o.model.extensionCount)
        assertEquals(o.selection.rects(o.page.page), o.model.rects.value[o.page.page])
        o.viewer.destroy()
    }

    /** A point of the second drag never extends the first, and the second drag's own extension still runs. */
    @Test
    fun aSecondDragsPointStaysWithTheSecondDrag() = mainTest {
        val o = opened()
        val start = pointOverText(o.page, o.selection)
        val end = Point(start.x + 120, start.y)
        // No `run()`: every call waits in the queue until the end sends them.
        o.model.dragBegan(start, o.page.page)
        o.model.dragChanged(Point(start.x + 40, start.y), o.page.page)
        o.model.dragBegan(start, o.page.page)
        o.model.dragChanged(end, o.page.page)
        assertTrue(o.model.dragEnded())
        assertEquals(1, o.model.extensionCount)
        assertEquals(selectionRects(start, end, o.page, o.selection), o.model.rects.value[o.page.page])
        o.viewer.destroy()
    }

    @Test
    fun aDragThatStartsOffTextSelectsNothing() = mainTest {
        val o = opened()
        val margin = pointOffText(o.page, o.selection)
        o.model.dragBegan(margin, o.page.page)
        o.model.dragChanged(Point(300.0, 300.0), o.page.page)
        o.model.dragEnded()
        assertEquals(emptyMap<PageRef, List<Rect>>(), o.model.rects.value)
        assertEquals(0, o.model.extensionCount)
        assertFalse(o.model.isSelecting.value)
        o.viewer.destroy()
    }

    @Test
    fun dragEndedReportsWhetherTextWasUnderTheStart() = mainTest {
        val o = opened()
        val margin = pointOffText(o.page, o.selection)
        val text = pointOverText(o.page, o.selection)
        o.model.dragBegan(margin, o.page.page)
        assertFalse(o.model.dragEnded())
        o.model.dragBegan(text, o.page.page)
        assertTrue(o.model.dragEnded())
        // An end with no begin before it closes nothing.
        assertFalse(o.model.dragEnded())
        o.viewer.destroy()
    }

    @Test
    fun rectsAreFinalAfterTheEndEvenWhenAnEventRefreshInterleaves() = mainTest {
        val o = opened()
        running(o.model)
        val start = pointOverText(o.page, o.selection)
        val reached = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        var held = false
        o.model.rectsReadReturned = {
            if (!held) {
                held = true
                reached.complete(Unit)
                release.await()
            }
        }
        o.model.dragBegan(start, o.page.page)
        // The begin's refresh has read the caret's rects and not applied them.
        reached.await()
        o.selection.selectAll()
        release.complete(Unit)
        o.model.dragEnded()
        val pages = o.selection.selectedPages()
        assertTrue(pages.isNotEmpty())
        // The event refresh `selectAll` queued may still be on its way; it is
        // queued before the end only once its event arrived.
        expectObserved(o.model.rects, pages.associateWith { o.selection.rects(it) })
        o.viewer.destroy()
    }

    @Test
    fun aNewDragClearsTheLastError() = mainTest {
        val o = opened()
        val start = pointOverText(o.page, o.selection)
        // No page of the document has this object number, so selecting the word throws.
        o.model.dragBegan(start, missingPage, TextSelectionModel.Start.WORD)
        o.model.dragEnded()
        assertNotNull(o.model.error.value)
        o.model.dragBegan(start, o.page.page)
        assertTrue(o.model.dragEnded())
        assertNull(o.model.error.value)
        o.viewer.destroy()
    }

    @Test
    fun clearingClearsTheLastError() = mainTest {
        val o = opened()
        val start = pointOverText(o.page, o.selection)
        o.model.dragBegan(start, missingPage, TextSelectionModel.Start.WORD)
        o.model.dragEnded()
        assertNotNull(o.model.error.value)
        o.model.clear()
        assertNull(o.model.error.value)
        o.viewer.destroy()
    }

    @Test
    fun selectionsMadeAndClearedElsewhereReachTheModelUntilClose() = mainTest {
        val o = opened()
        val observing = running(o.model)
        o.selection.ensureLoaded(o.page.page)
        expectObserved({ o.model.rects.value[o.page.page]?.isNotEmpty() == true }, true) { o.selection.selectPage(o.page.page) }
        expectObserved({ o.model.rects.value.isEmpty() }, true) { o.selection.clear() }
        o.viewer.closeAll()
        assertTrue(ends(observing))
        o.viewer.destroy()
    }
}
