package com.embedpdf.models

import com.embedpdf.AnnotationCapability
import com.embedpdf.AnnotationSubtype
import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.InteractionCapability
import com.embedpdf.LinkCapability
import com.embedpdf.PageRenderResolution
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.PointerPhase
import com.embedpdf.PointerSample
import com.embedpdf.RenderConfig
import com.embedpdf.RenderInvalidateScope
import com.embedpdf.Size
import com.embedpdf.StageCapability
import com.embedpdf.StageConfig
import com.embedpdf.ToolId
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.render
import com.embedpdf.stage
import com.embedpdf.started
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.joinAll
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import kotlin.math.roundToLong

class StageReaderModelTest {
    @get:Rule
    val main = MainThreadRule()

    private class Plain(val viewer: PdfViewer, val document: DocumentInfo, val model: StageReaderModel, val observing: Job)

    /** A reader over `report` with no hub, its `run()` going. */
    private suspend fun CoroutineScope.plain(fixture: String = "report"): Plain {
        val viewer = started(ViewerConfig(render = RenderConfig(), stage = StageConfig()))
        val document = viewer.open(Fixture.bytes(fixture))
        val model = StageReaderModel(viewer.stage(document), viewer.render(document))
        val observing = launch { model.run() }
        return Plain(viewer, document, model, observing)
    }

    private class Reader(
        val viewer: PdfViewer,
        val model: StageReaderModel,
        val stage: StageCapability,
        val interaction: InteractionCapability,
        val annotations: AnnotationCapability,
        val link: LinkCapability,
        val observing: Job,
    )

    /** A sized reader with a hub and links, its `run()` going and the tool read once. */
    private suspend fun CoroutineScope.reader(fixture: String = "annotation_kinds"): Reader {
        val viewer = started(ViewerConfig(render = RenderConfig(), interaction = true, stage = StageConfig(), link = true, annotation = ""))
        val document = viewer.open(Fixture.bytes(fixture))
        val stage = viewer.stage(document)
        val interaction = viewer.interaction(document)
        val link = viewer.link(document)
        val model = StageReaderModel(stage, viewer.render(document), interaction, link)
        val observing = launch { model.run() }
        model.sizeChanged(390f, 844f, 2f)
        expectObserved({ model.activeToolId.value != null }, true)
        val annotations = viewer.annotations(document)
        annotations.refresh()
        return Reader(viewer, model, stage, interaction, annotations, link, observing)
    }

    /** A viewport point inside the first visible page, [fraction] of the way across and down it. */
    private fun point(model: StageReaderModel, fraction: Double): Pair<Float, Float> {
        val page = model.visiblePages.value.first()
        return (page.screenX + page.transform.viewWidth * fraction).toFloat() to
            (page.screenY + page.transform.viewHeight * fraction).toFloat()
    }

    private suspend fun inkCount(annotations: AnnotationCapability): Int =
        annotations.list().count { it.subtype == AnnotationSubtype.INK }

    private suspend fun untilStill(model: StageReaderModel) {
        val deadline = System.nanoTime() + 20_000_000_000
        while (model.stage.isMoving() && System.nanoTime() < deadline) delay(50)
    }

    @Test
    fun anInvalidationRefreshesOnlyItsPagesWithoutMovingTheCamera() = mainTest {
        val plain = plain()
        val viewer = plain.viewer
        val model = plain.model
        val pages = viewer.pages(plain.document)
        model.stage.setViewportSize(Size(800.0, 600.0))
        expectObserved({ model.visiblePages.value.isNotEmpty() }, true)
        val before = model.renderRevision(pages[0].page)
        val other = model.renderRevision(pages[1].page)
        val camera = model.stage.camera()
        expectObserved({ model.renderRevision(pages[0].page) > before }, true) {
            model.render.invalidate(listOf(pages[0].page), RenderInvalidateScope.ANNOTATIONS)
        }
        assertEquals(other, model.renderRevision(pages[1].page))
        assertEquals(camera, model.stage.camera())
        viewer.destroy()
        assertTrue(ends(plain.observing))
    }

    @Test
    fun sizingShowsPages() = mainTest {
        val reader = plain()
        reader.model.sizeChanged(390f, 844f, 3f)
        assertTrue(reader.model.visiblePages.value.isNotEmpty())
        assertEquals(0, reader.model.pageIndex.value)
        reader.viewer.destroy()
    }

    @Test
    fun draggingScrollsAndResizingKeepsTheCurrentPage() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        val before = model.camera.value
        repeat(12) {
            model.dragChanged(0f, -800f)
            model.dragEnded(0f, 0f)
        }
        assertNotEquals(before, model.camera.value)
        val page = model.pageIndex.value
        assertTrue(page > 0)
        model.sizeChanged(844f, 390f, 3f)
        assertEquals(page, model.pageIndex.value)
        assertTrue(model.visiblePages.value.isNotEmpty())
    }

    /** A drag sample does not read the camera back itself: the stage's camera event brings it. */
    @Test
    fun aDragSampleReachesTheModelThroughTheStageEvents() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        val before = model.visiblePages.value
        model.dragChanged(0f, -400f)
        val camera = model.stage.camera()
        val pages = model.stage.visiblePages()
        assertNotEquals(before, pages)
        expectObserved(model.camera, camera)
        expectObserved(model.visiblePages, pages)
        model.dragEnded(0f, 0f)
    }

    /** A gesture's change and end made back to back keep the bracket ordered, so the stage springs back. */
    @Test
    fun backToBackGestureCallsKeepTheBracketOrdered() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        val resting = model.stage.camera()
        repeat(20) {
            val change = launch { model.dragChanged(0f, 300f) }
            val end = launch { model.dragEnded(0f, 0f) }
            change.join()
            end.join()
        }
        untilStill(model)
        assertEquals(resting, model.stage.camera())
    }

    /** Drag changes made while one is on its way collapse into the newest, which the stage gets. */
    @Test
    fun dragChangesBehindOneInFlightCollapse() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        val start = model.stage.camera()
        model.dragChanged(0f, 0f)
        val before = model.dragChangesApplied
        // None of the three has reached the stage when the next is made.
        val changes = (1..3).map { step -> launch { model.dragChanged(0f, -100f * step) } }
        changes.joinAll()
        val applied = model.dragChangesApplied - before
        assertTrue("$applied applied", applied < 3)
        assertEquals(0.0 to -300.0, model.lastDragChangeApplied)
        model.dragEnded(0f, 0f)
        assertNotEquals(start, model.stage.camera())
    }

    @Test
    fun theRenderScaleIsCappedBandedWhileZoomingAndTheStagesAtRest() {
        val capped = StageReaderModel.renderScale(12.0, 600.0, 800.0)
        assertTrue(capped < 12)
        assertTrue(600 * capped * 800 * capped <= StageReaderModel.MAX_PAGE_PIXELS * 1.000001)
        val banded = StageReaderModel.renderScale(2.0, 600.0, 800.0, zooming = true)
        assertEquals(PageRenderResolution.bandedScale(2.0, 600.0), banded, 0.0)
        assertEquals(banded, StageReaderModel.renderScale(2.1, 600.0, 800.0, zooming = true), 0.0)
        assertEquals(2.0, StageReaderModel.renderScale(2.0, 612.0, 792.0), 0.0)
        assertEquals(1280L, (StageReaderModel.renderScale(2.0, 612.0, 792.0, zooming = true) * 612).roundToLong())
        assertEquals(capped, StageReaderModel.renderScale(12.0, 600.0, 800.0, zooming = true), 0.0)
    }

    @Test
    fun aPinchZoomsUntilItSettles() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        assertFalse(model.isZooming.value)
        model.magnifyChanged(1.2f, 195f, 422f)
        assertTrue(model.isZooming.value)
        expectObserved(model.isZooming, false) { model.magnifyEnded() }
    }

    @Test
    fun aDoubleTapZoomsUntilItSettles() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        expectObserved(model.isZooming, false) { model.doubleTapped(195f, 422f) }
        assertFalse(model.stage.isMoving())
    }

    /** A refresh while a double tap's stage call is on its way reads the still stage; it must not end the zoom. */
    @Test
    fun aRefreshAheadOfTheDoubleTapDoesNotEndItsZoom() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        val reached = CompletableDeferred<Unit>()
        val gate = CompletableDeferred<Unit>()
        model.zoomStartReached = {
            reached.complete(Unit)
            gate.await()
        }
        val tap = launch { model.doubleTapped(195f, 422f) }
        reached.await()
        assertFalse(model.stage.isMoving())
        // Ends in a refresh, which reads the still stage.
        model.sizeChanged(390f, 844f, 3f)
        gate.complete(Unit)
        tap.join()
        assertTrue(model.stage.isMoving())
        assertTrue(model.isZooming.value)
        expectObserved(model.isZooming, false)
        assertFalse(model.stage.isMoving())
    }

    @Test
    fun aDoubleTapDuringAPinchKeepsZoomingUntilThePinchEnds() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        model.magnifyChanged(1.2f, 195f, 422f)
        model.doubleTapped(195f, 422f)
        assertTrue(model.isZooming.value)
        untilStill(model)
        // Ends in a refresh, which reads the still stage with no start in flight.
        model.sizeChanged(390f, 844f, 3f)
        assertTrue(model.isZooming.value)
        expectObserved(model.isZooming, false) { model.magnifyEnded() }
    }

    @Test
    fun disablingPanLeavesTheCameraAloneAndClosesAnOpenGesture() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        model.dragChanged(0f, -50f)
        assertTrue(model.gestureOpen)
        // The setter queues the end; a no-op drag call waits for it.
        model.isPanEnabled = false
        model.dragChanged(0f, 0f)
        assertFalse(model.gestureOpen)
        untilStill(model)
        val before = model.stage.camera()
        model.dragChanged(0f, -200f)
        model.dragEnded(0f, 0f)
        assertEquals(before, model.stage.camera())
        assertFalse(model.isPanEnabledFlow.value)
    }

    @Test
    fun withPanBeginsOnMovementAPanBeginsPastTheThreshold() = mainTest {
        val model = plain().model
        model.sizeChanged(390f, 844f, 3f)
        model.panBeginsOnMovement = true
        model.dragChanged(0f, -6f)
        assertFalse(model.gestureOpen)
        model.dragChanged(0f, -20f)
        assertTrue(model.gestureOpen)
        model.dragEnded(0f, 0f)
        assertFalse(model.gestureOpen)
    }

    @Test
    fun aGoodCallOfTheKindThatFailedClearsTheErrorAndAnotherKindKeepsIt() = mainTest {
        val model = plain().model
        val failure = PdfException(PdfException.Code.OPERATION_FAILED, "a failed link load")
        model.failed(StageReaderModel.Call.LINK_LOAD, failure)
        model.sizeChanged(390f, 844f, 3f)
        // A good resize and read are other kinds.
        assertEquals(failure, model.error.value)
        model.failed(StageReaderModel.Call.RESIZE, failure)
        model.sizeChanged(390f, 844f, 3f)
        assertNull(model.error.value)
    }

    @Test
    fun aForwardedDragUnderTheInkToolCreatesInk() = mainTest {
        val reader = reader()
        val model = reader.model
        val before = inkCount(reader.annotations)
        expectObserved(model.forwardsDrags, true) { reader.interaction.activateTool(ToolId.INK) }
        assertEquals(ToolId.INK, model.activeToolId.value)
        point(model, 0.30).let { (x, y) -> model.pointerDown(x, y) }
        for (fraction in listOf(0.35, 0.40, 0.45)) {
            point(model, fraction).let { (x, y) -> model.pointerMoved(x, y) }
            model.pointerCallsFinished()
        }
        point(model, 0.45).let { (x, y) -> model.pointerUp(x, y) }
        model.pointerCallsFinished()
        reader.annotations.drafts.finishInkDraft()
        assertEquals(before + 1, inkCount(reader.annotations))
        assertNull(model.error.value)
        reader.viewer.destroy()
        assertTrue(ends(reader.observing))
    }

    @Test
    fun aDragUnderTheDefaultToolIsNotForwarded() = mainTest {
        val reader = reader()
        assertEquals(reader.interaction.defaultToolId(), reader.model.activeToolId.value)
        assertFalse(reader.model.forwardsDrags.value)
        reader.viewer.destroy()
    }

    @Test
    fun aReaderWithoutAHubForwardsNothing() = mainTest {
        val model = plain("annotation_kinds").model
        model.sizeChanged(390f, 844f, 2f)
        var sent = 0
        model.pointerSampleDispatched = { sent += 1 }
        model.pointerDown(100f, 100f)
        model.pointerMoved(120f, 120f)
        model.pointerUp(120f, 120f)
        model.tapped(120f, 120f)
        model.pointerCancelled()
        model.pointerCallsFinished()
        assertEquals(0, sent)
        assertNull(model.activeToolId.value)
        assertFalse(model.forwardsDrags.value)
        assertNull(model.error.value)
    }

    @Test
    fun aCancelledDragLeavesNoGestureOpen() = mainTest {
        val reader = reader()
        val model = reader.model
        val before = inkCount(reader.annotations)
        expectObserved(model.forwardsDrags, true) { reader.interaction.activateTool(ToolId.INK) }
        val phases = mutableListOf<PointerPhase>()
        model.pointerSampleDispatched = { phases += it.phase }
        point(model, 0.30).let { (x, y) -> model.pointerDown(x, y) }
        point(model, 0.40).let { (x, y) -> model.pointerMoved(x, y) }
        model.pointerCallsFinished()
        model.pointerCancelled()
        model.pointerCallsFinished()
        assertEquals(listOf(PointerPhase.DOWN, PointerPhase.MOVE, PointerPhase.CANCEL), phases)
        reader.annotations.drafts.finishInkDraft()
        assertEquals(before, inkCount(reader.annotations))
        reader.viewer.destroy()
    }

    /** Moves made while a sample is on its way collapse into the newest. */
    @Test
    fun movesBehindOneInFlightCollapse() = mainTest {
        val reader = reader()
        val model = reader.model
        reader.interaction.activateTool(ToolId.INK)
        val samples = mutableListOf<PointerSample>()
        model.pointerSampleDispatched = { samples += it }
        val (x, y) = point(model, 0.2)
        model.pointerDown(x, y)
        for (step in 1..50) model.pointerMoved(x + step, y + step)
        model.pointerCallsFinished()
        val moves = samples.filter { it.phase == PointerPhase.MOVE }
        assertTrue("${moves.size} moves", moves.size < 50)
        assertEquals((x + 50).toDouble(), moves.last().viewport.x, 0.001)
        model.pointerUp(x + 50, y + 50)
        model.pointerCallsFinished()
        assertEquals(PointerPhase.UP, samples.last().phase)
        reader.viewer.destroy()
    }

    @Test
    fun aPinchCancelsTheOpenToolGesture() = mainTest {
        val reader = reader()
        val model = reader.model
        expectObserved(model.forwardsDrags, true) { reader.interaction.activateTool(ToolId.INK) }
        val phases = mutableListOf<PointerPhase>()
        model.pointerSampleDispatched = { phases += it.phase }
        point(model, 0.3).let { (x, y) -> model.pointerDown(x, y) }
        model.magnifyChanged(1.1f, 195f, 422f)
        // The rest of the touch sends nothing.
        point(model, 0.4).let { (x, y) -> model.pointerMoved(x, y) }
        point(model, 0.4).let { (x, y) -> model.pointerUp(x, y) }
        model.pointerCallsFinished()
        assertEquals(listOf(PointerPhase.DOWN, PointerPhase.CANCEL), phases)
        model.magnifyEnded()
        reader.viewer.destroy()
    }

    @Test
    fun aPageScrolledIntoViewHasItsLinksLoaded() = mainTest {
        val reader = reader("report")
        val model = reader.model
        model.linkLoadsFinished()
        for (page in model.visiblePages.value) assertTrue(reader.link.isLoaded(page.page))
        reader.viewer.destroy()
    }
}
