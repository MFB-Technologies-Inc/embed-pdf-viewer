package com.embedpdf.compose

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.click
import androidx.compose.ui.test.doubleClick
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.LinkActivation
import com.embedpdf.Point
import com.embedpdf.Rect
import com.embedpdf.RenderConfig
import com.embedpdf.StageConfig
import com.embedpdf.ToolId
import com.embedpdf.ViewerConfig
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.models.StageReaderModel
import com.embedpdf.models.TextSelectionModel
import com.embedpdf.render
import com.embedpdf.selection
import com.embedpdf.stage
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

/** How `StageReader` shares a touch with a long-press text selection, links and tools. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class StageReaderGestureTest {
    @get:Rule
    val compose = createComposeRule()

    private class Reader(val o: Opened, val model: StageReaderModel, val selection: TextSelectionModel, val activations: List<LinkActivation>)

    /**
     * A reader over [fixture] with a hub, links and selection, and a
     * long-press selection overlay on each page, as `PdfReader` lays it.
     */
    private fun reader(fixture: String): Reader {
        val o = opened(
            ViewerConfig(render = RenderConfig(), interaction = true, stage = StageConfig(), link = true, selection = true, annotation = ""),
            fixture,
        )
        val model = engine {
            StageReaderModel(o.viewer.stage(o.document), o.viewer.render(o.document), o.viewer.interaction(o.document), o.viewer.link(o.document))
        }
        val selection = TextSelectionModel(engine { o.viewer.selection(o.document) })
        val activations = mutableListOf<LinkActivation>()
        compose.setContent {
            LaunchedEffect(selection) { selection.run() }
            StageReader(model, Modifier.size(390.dp, 844.dp).testTag("reader"), onLinkActivation = { activations += it }) { page ->
                Box(Modifier.fillMaxSize()) {
                    TextSelectionOverlay(selection, page, o.pages[page.pageIndex.toInt()], Modifier.fillMaxSize(), activation = TextSelectionActivation.LONG_PRESS)
                }
            }
        }
        compose.pumpUntil { model.visiblePages.value.isNotEmpty() && model.activeToolId.value != null }
        compose.pumpFor(100)
        return Reader(o, model, selection, activations)
    }

    /** The centre of [rect], a rect on the first page, in the reader's pixels. */
    private fun Reader.pixel(rect: Rect): Offset {
        val page = o.pages[0].page
        val viewport = engine {
            o.viewer.stage(o.document).pageToViewport(page, Point(rect.x + rect.width / 2, rect.y + rect.height / 2))
        }!!
        val scale = compose.density.density
        return Offset((viewport.x * scale).toFloat(), (viewport.y * scale).toFloat())
    }

    /** A point on the first page's first line of text, in the reader's pixels. */
    private fun Reader.text(): Offset {
        val page = o.pages[0].page
        val rect = engine {
            val selection = o.viewer.selection(o.document)
            selection.ensureLoaded(page)
            selection.selectPage(page)
            val first = selection.rects(page).first()
            selection.clear()
            first
        }
        return pixel(rect)
    }

    /** Review fix 1: a long press whose finger jitters within the slop still arms the selection, and does not pan. */
    @Test
    fun aJitteryLongPressSelectsAndDoesNotPan() {
        val r = reader("report")
        try {
            val camera = r.model.camera.value
            val from = r.text()
            compose.onNodeWithTag("reader").performTouchInput {
                down(from)
                moveBy(Offset(2f, 0f))
                moveBy(Offset(-1f, 1f))
                advanceEventTime(viewConfiguration.longPressTimeoutMillis + 100)
                moveTo(from + Offset(60f, 0f))
                moveTo(from + Offset(120f, 0f))
                up()
            }
            compose.pumpUntil(message = { "rects ${r.selection.rects.value}" }) {
                r.selection.rects.value.isNotEmpty() && !r.selection.isSelecting.value
            }
            compose.pumpFor(200)
            assertEquals(camera, r.model.camera.value)
        } finally {
            r.o.destroy()
        }
    }

    /** Review fix 1: a tap that jitters within the slop neither pans nor flings. */
    @Test
    fun aJitteryTapDoesNotMoveTheStage() {
        val r = reader("report")
        try {
            val camera = r.model.camera.value
            val at = r.text()
            compose.onNodeWithTag("reader").performTouchInput {
                down(at)
                moveBy(Offset(0f, -3f))
                up()
            }
            compose.pumpFor(600)
            assertEquals(camera, r.model.camera.value)
        } finally {
            r.o.destroy()
        }
    }

    /** Review fix 2: a tap on a link activates it; a long press that the selection armed does not. */
    @Test
    fun aLongPressOnALinkDoesNotActivateIt() {
        val r = reader("annotation_kinds")
        try {
            val page = r.o.pages[0].page
            val link = engine {
                val links = r.o.viewer.link(r.o.document)
                links.ensureLoaded(page)
                links.links(page).first()
            }
            val at = r.pixel(link.bounds)
            compose.onNodeWithTag("reader").performTouchInput {
                down(at)
                advanceEventTime(viewConfiguration.longPressTimeoutMillis + 100)
                up()
            }
            compose.pumpFor(600)
            assertEquals(emptyList<LinkActivation>(), r.activations)
            compose.onNodeWithTag("reader").performTouchInput { click(at) }
            compose.pumpUntil { r.activations.isNotEmpty() }
        } finally {
            r.o.destroy()
        }
    }

    /** Review fix 5: under a drawing tool a double tap goes to the tool and does not zoom. */
    @Test
    fun aDoubleTapUnderAToolDoesNotZoom() {
        val r = reader("report")
        try {
            engine { r.o.viewer.interaction(r.o.document).activateTool(ToolId.INK) }
            compose.pumpUntil { r.model.forwardsDrags.value }
            val zoom = r.model.camera.value?.zoom
            val at = r.text()
            compose.onNodeWithTag("reader").performTouchInput { doubleClick(at) }
            compose.pumpFor(800)
            assertTrue(!r.model.isZooming.value)
            assertEquals(zoom, r.model.camera.value?.zoom)
        } finally {
            r.o.destroy()
        }
    }
}
