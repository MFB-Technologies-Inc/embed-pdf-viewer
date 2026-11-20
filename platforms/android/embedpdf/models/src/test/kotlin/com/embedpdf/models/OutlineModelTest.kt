package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.LinkActivationRevealed
import com.embedpdf.OutlineCapability
import com.embedpdf.OutlineItemInput
import com.embedpdf.OutlineItemPatch
import com.embedpdf.OutlinePatchKey
import com.embedpdf.OutlinePositionStart
import com.embedpdf.PageDestinationFit
import com.embedpdf.PageLinkTargetGoto
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ResourceStatus
import com.embedpdf.Size
import com.embedpdf.StageConfig
import com.embedpdf.StageEventPageChanged
import com.embedpdf.ViewerConfig
import com.embedpdf.outline
import com.embedpdf.stage
import com.embedpdf.started
import com.embedpdf.subscribed
import com.embedpdf.nextMapped
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class OutlineModelTest {
    @get:Rule
    val main = MainThreadRule()

    private data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val outline: OutlineCapability, val model: OutlineModel)

    private suspend fun opened(scope: List<String>? = null, stage: Boolean = false): Opened {
        val viewer = started(ViewerConfig(scope = scope, stage = if (stage) StageConfig() else null, outline = true))
        val document = viewer.open(Fixture.bytes("report"))
        val outline = viewer.outline(document)
        return Opened(viewer, document, outline, OutlineModel(viewer, outline))
    }

    @Test
    fun cancellingInitialObservationDoesNotPoisonTheRetainedModel() = mainTest {
        val (viewer, _, _, model) = opened()
        val cancelled = launch(start = CoroutineStart.UNDISPATCHED) {
            coroutineContext[Job]!!.cancel()
            model.run()
        }
        cancelled.join()
        assertNull(model.error.value)
        val reopened = launch { model.run() }
        expectObserved(model.status, ResourceStatus.READY)
        assertNull(model.error.value)
        viewer.destroy()
        assertTrue(ends(reopened))
    }

    @Test
    fun startingObservationPreservesAnActionFailure() = mainTest {
        val (viewer, _, _, model) = opened()
        model.reload()
        val item = checkNotNull(model.create(OutlineItemInput(title = "Parent", clearTarget = true)))
        model.move(listOf(item.itemRef), item.itemRef)
        val failure = checkNotNull(model.error.value)
        val observing = launch { model.run() }
        expectObserved(model.status, ResourceStatus.READY)
        assertEquals(failure, model.error.value)
        viewer.destroy()
        assertTrue(ends(observing))
    }

    @Test
    fun rowsExpansionSelectionAndEditsComeFromTheCapability() = mainTest {
        val (viewer, _, _, model) = opened()
        model.reload()
        assertEquals(ResourceStatus.READY, model.status.value)
        val parent = checkNotNull(model.create(OutlineItemInput(title = "Parent", clearTarget = true)))
        val child = checkNotNull(
            model.create(OutlineItemInput(title = "Child", parent = parent.itemRef, clearTarget = true, color = "#ff0000")),
        )
        model.collapse(parent.itemRef)
        assertTrue(model.rows.value.none { it.item.itemRef == child.itemRef })
        model.expand(parent.itemRef)
        assertEquals(1u, model.rows.value.first { it.item.itemRef == child.itemRef }.depth)
        model.select(listOf(child.itemRef))
        assertEquals(listOf(child.itemRef), model.selectedItems.value)
        val updated = model.update(
            child.itemRef,
            OutlineItemPatch(title = "Renamed", clear = listOf(OutlinePatchKey.COLOR, OutlinePatchKey.TARGET)),
        )
        assertNull(updated?.color)
        model.move(listOf(child.itemRef), null, OutlinePositionStart)
        assertNull(model.items.value.first { it.itemRef == child.itemRef }.parent)
        model.delete(listOf(child.itemRef, parent.itemRef))
        assertTrue(model.items.value.none { it.itemRef == parent.itemRef })
        assertNull(model.error.value)
        viewer.destroy()
    }

    /** A revision that leaves the outline as it was must not notify; a real change still must. */
    @Test
    fun unchangedRevisionDoesNotNotify() = mainTest {
        val (viewer, document, outline, model) = opened()
        model.reload()
        val page = viewer.pages(document)[1].page
        val parent = checkNotNull(
            model.create(
                OutlineItemInput(title = "Parent", target = PageLinkTargetGoto(PageDestinationFit(page)), color = "#ff0000", bold = true),
            ),
        )
        val child = checkNotNull(model.create(OutlineItemInput(title = "Child", parent = parent.itemRef, clearTarget = true)))
        model.select(listOf(parent.itemRef))
        val shown = listOf(model.items, model.rows, model.selectedItems, model.currentItem, model.status, model.incomplete, model.canEdit)
        // Each read lifts the item's action as a new handle.
        assertTrue(model.items.value.any { it.action != null })
        assertFalse(anyChanges(shown) { model.refresh() })
        assertTrue(
            changes(model.items) {
                outline.update(parent.itemRef, OutlineItemPatch(title = "Renamed"))
                model.refresh()
            },
        )
        assertTrue(
            changes(model.rows) {
                outline.collapse(parent.itemRef)
                model.refresh()
            },
        )
        assertFalse(changes(model.selectedItems) { model.select(listOf(parent.itemRef)) })
        assertTrue(changes(model.selectedItems) { model.select(listOf(child.itemRef)) })
        assertEquals(listOf(child.itemRef), model.selectedItems.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun twoModelsObserveTheSameViewerIndependently() = mainTest {
        val (viewer, _, outline, first) = opened()
        val second = OutlineModel(viewer, outline)
        val firstRun = launch { first.run() }
        val secondRun = launch { second.run() }
        expectObserved(first.status, ResourceStatus.READY)
        expectObserved(second.status, ResourceStatus.READY)
        outline.create(OutlineItemInput(title = "External", clearTarget = true))
        expectObserved({ first.rows.value.any { it.item.title == "External" } }, true)
        expectObserved({ second.rows.value.any { it.item.title == "External" } }, true)
        viewer.destroy()
        assertTrue(ends(firstRun))
        assertTrue(ends(secondRun))
    }

    @Test
    fun closedDocumentEndsObservationAndCannotEditItsReplacement() = mainTest {
        val (viewer, document, _, model) = opened()
        val observing = launch { model.run() }
        expectObserved(model.status, ResourceStatus.READY)
        viewer.close(document)
        assertTrue(ends(observing))
        assertEquals(0, model.rows.value.size)
        assertFalse(model.canEdit.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        val next = viewer.open(Fixture.bytes("report"))
        val nextModel = OutlineModel(viewer, viewer.outline(next))
        nextModel.reload()
        assertNull(nextModel.error.value)
        assertNull(model.create(OutlineItemInput(title = "Old document")))
        assertTrue(nextModel.items.value.none { it.title == "Old document" })
        viewer.destroy()
    }

    @Test
    fun activationUsesTheRustStageAndDeniedEditsKeepTheirError() = mainTest {
        val (viewer, document, _, model) = opened(stage = true)
        val stage = viewer.stage(document)
        stage.setViewportSize(Size(800.0, 600.0))
        model.reload()
        val page = viewer.pages(document)[2].page
        val item = checkNotNull(model.create(OutlineItemInput(title = "Third page", target = PageLinkTargetGoto(PageDestinationFit(page)))))
        val events = subscribed(stage.observeEvents())
        model.activate(item.itemRef)
        events.nextMapped { (it as? StageEventPageChanged)?.takeIf { change -> change.event.pageIndex == 2u } }
        assertTrue(model.lastActivation.value is LinkActivationRevealed)
        assertEquals(2, stage.currentPageIndex())
        viewer.destroy()
        val (restrictedViewer, _, _, restricted) = opened(scope = listOf("doc.open"))
        restricted.refresh()
        assertNull(restricted.error.value)
        assertFalse(restricted.canEdit.value)
        assertNull(restricted.create(OutlineItemInput(title = "Denied")))
        assertEquals(PdfException.Code.PERMISSION_DENIED, restricted.error.value?.code)
        restrictedViewer.destroy()
    }

    @Test
    fun aCancelledActionRecordsTheCancellationAndEndsPerforming() = mainTest {
        val (viewer, _, _, model) = opened()
        model.reload()
        var thrown: Throwable? = null
        val job = launch(start = CoroutineStart.UNDISPATCHED) {
            try {
                model.create(OutlineItemInput(title = "Cancelled", clearTarget = true))
            } catch (e: CancellationException) {
                thrown = e
                throw e
            }
        }
        job.cancel()
        job.join()
        assertTrue(thrown is CancellationException)
        assertFalse(model.performing.value)
        assertEquals(PdfException.Code.OPERATION_CANCELLED, model.error.value?.code)
        viewer.destroy()
    }
}
