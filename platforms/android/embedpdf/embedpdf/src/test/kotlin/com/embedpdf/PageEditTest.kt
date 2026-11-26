package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class PageEditTest {
    private suspend fun opened(): Triple<PdfViewer, DocumentInfo, PageEditCapability> {
        val viewer = started(ViewerConfig(pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        return Triple(viewer, document, viewer.pageEdit(document))
    }

    @Test
    fun rotatingTwiceMakesAHalfTurn() = viewerTest {
        val (viewer, document, edit) = opened()
        val first = viewer.pages(document)[0].page
        edit.rotate(listOf(first), turn = PageEditTurn.CLOCKWISE)
        val layout = edit.rotate(listOf(first), turn = PageEditTurn.CLOCKWISE)
        assertEquals(180u, layout.pages[0].rotation)
        assertEquals(180u, viewer.pages(document)[0].rotation)
        viewer.destroy()
    }

    @Test
    fun movingAPageAfterTheLastReordersTheList() = viewerTest {
        val (viewer, document, edit) = opened()
        val pages = viewer.pages(document).map { it.page }
        val first = pages[0]
        // Placement names a neighbour page, not an index.
        val layout = edit.movePages(listOf(first), placement = PageEditPlacementAfter(pages.last()))
        assertEquals(first, layout.pages.last().page)
        assertEquals((0u until 14u).toList(), layout.pages.map { it.index })
        viewer.destroy()
    }

    @Test
    fun endMovesTheBlockLast() = viewerTest {
        val (viewer, document, edit) = opened()
        val pages = viewer.pages(document).map { it.page }
        val layout = edit.movePages(listOf(pages[0], pages[1]), placement = PageEditPlacementEnd)
        assertEquals(listOf(pages[0], pages[1]), layout.pages.map { it.page }.takeLast(2))
        assertEquals(14, layout.pages.size)
        viewer.destroy()
    }

    @Test
    fun deletingEveryPageThrowsAndKeepsThePages() = viewerTest {
        val (viewer, document, edit) = opened()
        val all = viewer.pages(document).map { it.page }
        try {
            edit.deletePages(all)
            throw AssertionError("every page was deleted")
        } catch (e: PdfException) {
            // Refused.
        }
        assertEquals(14, viewer.pages(document).size)
        viewer.destroy()
    }

    @Test
    fun anInsertedBlankPageTakesItsNeighboursSize() = viewerTest {
        val (viewer, document, edit) = opened()
        val first = viewer.pages(document)[0]
        val insertion = edit.insertBlank(PageEditBlankOptions(count = 2u, placement = PageEditPlacementAfter(first.page)))
        assertEquals(2, insertion.inserted.size)
        assertEquals(16, insertion.pages.size)
        assertEquals(first.width, insertion.pages[1].width, 0.0)
        assertEquals(insertion.inserted[0], insertion.pages[1].page)
        viewer.destroy()
    }

    @Test
    fun anExtractOpensAsItsOwnDocument() = viewerTest {
        val (viewer, document, edit) = opened()
        val pages = viewer.pages(document).take(3).map { it.page }
        val bytes = edit.extract(pages)
        assertArrayEquals("%PDF".toByteArray(), bytes.copyOf(4))
        assertEquals(3u, viewer.open(bytes).pageCount)
        viewer.destroy()
    }

    @Test
    fun pagesFromAnotherOpenDocumentInsert() = viewerTest {
        val (viewer, _, edit) = opened()
        val other = viewer.open(Fixture.bytes("colors"))
        val copied = viewer.pages(other).map { it.page }
        val insertion = edit.insert(copied, source = other, placement = PageEditPlacementStart)
        assertEquals(copied.size, insertion.inserted.size)
        assertEquals(14 + copied.size, insertion.pages.size)
        viewer.destroy()
    }

    @Test
    fun pagesFromBytesInsertAndDuplicatesFollowTheirOriginal() = viewerTest {
        val (viewer, document, edit) = opened()
        val inserted = edit.insert(Fixture.bytes("colors"))
        assertEquals(15, inserted.pages.size)
        val first = viewer.pages(document)[0].page
        val duplicated = edit.duplicate(listOf(first))
        assertEquals(16, duplicated.pages.size)
        assertEquals(duplicated.inserted[0], duplicated.pages[1].page)
        edit.select(listOf(first))
        assertEquals(listOf(first), edit.selectedPages())
        assertTrue(edit.canExtract())
        viewer.destroy()
    }

    @Test
    fun anEditIsObservedAsPagesChanged() = viewerTest {
        val (viewer, document, edit) = opened()
        val events = subscribed(viewer.observeDocumentEvents())
        val first = viewer.pages(document)[0].page
        edit.setRotation(listOf(first), PageEditRotation.CLOCKWISE90)
        val changed = events.nextMapped { it as? DocumentEventPagesChanged }
        assertEquals(document.id, changed.documentId)
        viewer.destroy()
    }

    @Test
    fun rotateCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, document, edit) = opened()
        val first = viewer.pages(document)[0].page
        val rotating = async(start = CoroutineStart.UNDISPATCHED) { edit.rotate(listOf(first), turn = PageEditTurn.HALF) }
        rotating.cancel()
        try {
            rotating.await()
        } catch (e: CancellationException) {
            // Cancelled before it started. Once started, the edit lands.
        }
        // The capability still answers after a cancelled call.
        assertTrue(edit.canEdit())
        viewer.destroy()
    }

    // One per remaining member that takes a cancel token, each called in an
    // already-cancelled coroutine.

    private suspend fun firstPage(): Triple<PdfViewer, PageEditCapability, PageRef> {
        val (viewer, document, edit) = opened()
        return Triple(viewer, edit, viewer.pages(document)[0].page)
    }

    @Test
    fun setRotationCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, page) = firstPage()
        expectCancelledBeforeItStarts { edit.setRotation(listOf(page), PageEditRotation.CLOCKWISE90) }
        viewer.destroy()
    }

    @Test
    fun movePagesCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, page) = firstPage()
        expectCancelledBeforeItStarts { edit.movePages(listOf(page), placement = PageEditPlacementEnd) }
        viewer.destroy()
    }

    @Test
    fun deletePagesCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, page) = firstPage()
        expectCancelledBeforeItStarts { edit.deletePages(listOf(page)) }
        assertEquals(14, viewer.pages(viewer.documents().first()).size)
        viewer.destroy()
    }

    @Test
    fun insertBlankCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, _) = firstPage()
        expectCancelledBeforeItStarts { edit.insertBlank() }
        viewer.destroy()
    }

    @Test
    fun insertFromBytesCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, _) = firstPage()
        val colors = Fixture.bytes("colors")
        expectCancelledBeforeItStarts { edit.insert(colors) }
        viewer.destroy()
    }

    @Test
    fun insertFromDocumentCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, _) = firstPage()
        val other = viewer.open(Fixture.bytes("colors"))
        val copied = viewer.pages(other).map { it.page }
        expectCancelledBeforeItStarts { edit.insert(copied, source = other) }
        viewer.destroy()
    }

    @Test
    fun duplicateCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, page) = firstPage()
        expectCancelledBeforeItStarts { edit.duplicate(listOf(page)) }
        viewer.destroy()
    }

    @Test
    fun extractCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, edit, page) = firstPage()
        expectCancelledBeforeItStarts { edit.extract(listOf(page)) }
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, edit) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { edit.canEdit() }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }
}
