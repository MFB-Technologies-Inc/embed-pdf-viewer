package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs

class SavedView(private val view: StageViewState) : StageInitialViewProvider {
    override fun initialView(): StageViewState = view
}

class StageTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val stage: StageCapability)

    // The native host's clock always has frames, so motion tweens; the tests
    // that need a jump ask for `INSTANT`.
    private val instant = StageGoToOptions(behavior = StageScrollBehavior.INSTANT)

    private suspend fun opened(config: ViewerConfig = ViewerConfig(stage = StageConfig())): Opened {
        val viewer = started(config)
        val document = viewer.open(Fixture.bytes("report"))
        val stage = viewer.stage(document)
        stage.setViewportSize(Size(390.0, 844.0))
        return Opened(viewer, document, stage)
    }

    @Test
    fun nativeZoomBoxTracksViewportPointsAndCancels() = viewerTest {
        val (viewer, _, stage) = opened()
        val before = stage.camera()
        stage.beginZoomDrag(Point(70.0, 90.0))
        assertNull(stage.zoomBox())
        stage.moveZoomDrag(Point(170.0, 190.0))
        val box = stage.zoomBox()!!
        assertTrue(abs(box.x - 70) < 0.000001 && abs(box.y - 90) < 0.000001)
        assertTrue(abs(box.width - 100) < 0.000001 && abs(box.height - 100) < 0.000001)
        stage.cancelZoomDrag()
        assertNull(stage.zoomBox())
        assertEquals(before, stage.camera())
        assertTrue(stage.currentPages().isNotEmpty())
        viewer.destroy()
    }

    @Test
    fun aSizedStageShowsTheFirstPage() = viewerTest {
        val (viewer, _, stage) = opened()
        val first = stage.visiblePages().first()
        assertEquals(0u, first.pageIndex)
        assertTrue(first.transform.renderScale > 0)
        viewer.destroy()
    }

    @Test
    fun navigationIsObserved() = viewerTest {
        val (viewer, _, stage) = opened()
        val events = subscribed(stage.observeEvents())
        stage.goToPageIndex(5, instant)
        val changed = events.nextMapped { it as? StageEventPageChanged }
        assertEquals(5u, changed.event.pageIndex)
        assertEquals(5, stage.currentPageIndex())
        viewer.destroy()
    }

    @Test
    fun fitWidthThenZoomIn() = viewerTest {
        val (viewer, _, stage) = opened()
        stage.fitWidth()
        assertEquals(StageZoomModeMode(ZoomMode.FIT_WIDTH), stage.zoomMode())
        val fitted = stage.zoomLevel()
        stage.zoomIn()
        assertTrue(stage.zoomLevel() > fitted)
        viewer.destroy()
    }

    @Test
    fun aPanGestureMovesTheCamera() = viewerTest {
        val (viewer, _, stage) = opened()
        val before = stage.camera()
        stage.beginGesture()
        repeat(20) { stage.panBy(0.0, -10.0) }
        stage.endGesture()
        assertNotEquals(before.y, stage.camera().y)
        viewer.destroy()
    }

    @Test
    fun aSavedViewRestoresOnTheNextOpen() = viewerTest {
        val (viewer, _, stage) = opened()
        stage.goToPageIndex(7, instant)
        val saved = stage.viewState()
        val second = viewer.open(Fixture.bytes("report"))
        val next = viewer.stage(second)
        next.provideInitialView(SavedView(saved), priority = 10.0)
        next.setViewportSize(Size(390.0, 844.0))
        assertEquals(7, next.currentPageIndex())
        viewer.destroy()
    }

    @Test
    fun aFlingMovesThenSettles() = viewerTest {
        val (viewer, _, stage) = opened()
        val events = subscribed(stage.observeEvents())
        stage.fling(velocityX = 0.0, velocityY = -3000.0)
        assertTrue(stage.isMoving())
        events.nextMapped { it as? StageEventMotionEnded }
        assertFalse(stage.isMoving())
        viewer.destroy()
    }

    /** A click with the zoom tool: a box that never reaches its threshold. */
    private suspend fun clickZoom(end: suspend (StageCapability) -> Unit): Pair<Double, Double> {
        val (viewer, _, stage) = opened()
        val before = stage.zoomLevel()
        stage.beginZoomDrag(Point(195.0, 400.0))
        end(stage)
        val after = stage.zoomLevel()
        viewer.destroy()
        return before to after
    }

    @Test
    fun endZoomDragLeftOutZoomsIn() = viewerTest {
        val omitted = clickZoom { it.endZoomDrag() }
        val zoomIn = clickZoom { it.endZoomDrag(zoomOut = false) }
        val zoomOut = clickZoom { it.endZoomDrag(zoomOut = true) }
        assertTrue(omitted.second > omitted.first)
        assertEquals(zoomIn.second, omitted.second, 0.0)
        assertTrue(zoomOut.second < zoomOut.first)
    }

    /**
     * The camera inside a gesture after dragging past the top of the
     * document, where only an elastic gesture leaves the clamp.
     */
    private suspend fun overscrolled(begin: suspend (StageCapability) -> Unit): Camera {
        val (viewer, _, stage) = opened()
        begin(stage)
        repeat(20) { stage.panBy(0.0, 30.0) }
        val camera = stage.camera()
        stage.endGesture()
        viewer.destroy()
        return camera
    }

    @Test
    fun beginGestureLeftOutIsNotElastic() = viewerTest {
        val omitted = overscrolled { it.beginGesture() }
        val clamped = overscrolled { it.beginGesture(elastic = false) }
        val elastic = overscrolled { it.beginGesture(elastic = true) }
        assertEquals(clamped, omitted)
        assertNotEquals(clamped, elastic)
    }

    /** The cursor over the gap beside the first page, with the pointer tool. */
    private suspend fun gapCursor(register: suspend (StageCapability) -> Registration): String {
        val (viewer, document, stage) = opened(ViewerConfig(interaction = true, stage = StageConfig()))
        val interaction = viewer.interaction(document)
        val registration = register(stage)
        val lens = stage.lensId()
        // A move nobody captured hovers; `page = null` is a gap.
        interaction.dispatchPointer(
            PointerSample(
                phase = PointerPhase.MOVE,
                viewport = Point(2.0, 2.0),
                modifiers = Modifiers(),
                pointerType = PointerKind.MOUSE,
                source = lens,
            ),
        )
        val cursor = interaction.cursor()
        registration.unregister()
        viewer.destroy()
        return cursor
    }

    @Test
    fun registerScrollHandlerLeftOutPansOverGaps() = viewerTest {
        val omitted = gapCursor { it.registerScrollHandler() }
        val fallback = gapCursor { it.registerScrollHandler(panFallback = true) }
        val without = gapCursor { it.registerScrollHandler(panFallback = false) }
        assertEquals("grab", omitted)
        assertEquals("grab", fallback)
        assertNotEquals("grab", without)
    }

    /** The companion refusal: the scroll handler lives on the pointer hub. */
    @Test
    fun registerScrollHandlerWithoutInteractionIsUnsupported() = viewerTest {
        val (viewer, _, stage) = opened()
        expectCode(PdfException.Code.UNSUPPORTED) { stage.registerScrollHandler() }
        viewer.destroy()
    }

    @Test
    fun aPageIndexBelowZeroIsInvalidInput() = viewerTest {
        val (viewer, _, stage) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) { stage.goToPageIndex(-1) }
        expectCode(PdfException.Code.INVALID_INPUT) { stage.revealIndex(-1) }
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, stage) = opened()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { stage.zoomLevel() }
        viewer.destroy()
    }

    @Test
    fun theSynchronousCallsAreDroppedAfterTheViewerCloses() = viewerTest {
        val (viewer, _, stage) = opened()
        viewer.close()
        stage.panBy(0.0, 10.0)
        stage.setCamera(Camera(0.0, 0.0, 1.0))
        stage.scrollBy(StageScrollToOptions())
        stage.zoomAround(Point(0.0, 0.0), 2.0)
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, stage) = opened()
        val collector = collecting(stage.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        stage.zoomIn()
        viewer.destroy()
    }
}
