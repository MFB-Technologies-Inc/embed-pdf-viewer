package com.embedpdf.compose

import android.content.Context
import android.view.accessibility.AccessibilityManager
import androidx.compose.foundation.layout.Box
import androidx.test.core.app.ApplicationProvider
import org.robolectric.Shadows.shadowOf
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.performSemanticsAction
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipe
import androidx.compose.ui.test.doubleClick
import androidx.compose.ui.test.filter
import androidx.compose.ui.unit.dp
import com.embedpdf.AnnotationSubtype
import com.embedpdf.Camera
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.RenderConfig
import com.embedpdf.StageConfig
import com.embedpdf.ToolId
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.models.StageReaderModel
import com.embedpdf.models.TextSelectionModel
import com.embedpdf.selection
import androidx.compose.runtime.LaunchedEffect
import com.embedpdf.render
import com.embedpdf.stage
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import kotlin.math.abs

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class StageReaderTest {
    @get:Rule
    val compose = createComposeRule()

    private class Reader(val o: Opened, val model: StageReaderModel)

    /** A reader with a hub and links over [fixture], composed 390 by 844 dp with a tagged overlay per page. */
    private fun reader(fixture: String = "annotation_kinds"): Reader {
        val o = opened(
            ViewerConfig(render = RenderConfig(), interaction = true, stage = StageConfig(), link = true, annotation = ""),
            fixture,
        )
        val model = engine {
            StageReaderModel(o.viewer.stage(o.document), o.viewer.render(o.document), o.viewer.interaction(o.document), o.viewer.link(o.document))
        }
        compose.setContent {
            StageReader(model, Modifier.size(390.dp, 844.dp).testTag("reader")) { page ->
                Box(Modifier.fillMaxSize().testTag("overlay")) { Text("${page.pageIndex}") }
            }
        }
        compose.pumpUntil { model.visiblePages.value.isNotEmpty() && model.activeToolId.value != null }
        return Reader(o, model)
    }

    private fun Reader.center(): Offset {
        val page = model.visiblePages.value.first()
        // Viewport dp to pixels.
        val scale = compose.density.density
        return Offset(
            ((page.screenX + page.transform.viewWidth / 2) * scale).toFloat(),
            ((page.screenY + page.transform.viewHeight / 3) * scale).toFloat(),
        )
    }

    private fun Reader.camera(): Camera? = model.camera.value

    @Test
    fun eachVisiblePageHasItsOverlayInThePagesFrame() {
        val r = reader()
        try {
            compose.pumpUntil { compose.onAllNodesWithTag("overlay").fetchSemanticsNodes().size == r.model.visiblePages.value.size }
            val page = r.model.visiblePages.value.first()
            val overlay = compose.onAllNodesWithTag("overlay").fetchSemanticsNodes().first()
            val scale = compose.density.density
            assertTrue(abs(overlay.size.width - page.transform.viewWidth * scale) <= 1.0)
            assertTrue(abs(overlay.boundsInRoot.left - page.screenX * scale) <= 1.0)
            // TalkBack: the reader is labelled, and nothing in it acts without a label.
            compose.onNodeWithContentDescription("Document pages").assertExists()
            compose.onAllNodesWithTag("reader").assertAll(hasLabel())
            assertEquals(0, compose.onAllNodesWithTag("reader").filter(hasClickAction()).fetchSemanticsNodes().size)
        } finally {
            r.o.destroy()
        }
    }

    @Test
    fun aDragUnderTheDefaultToolPansTheStage() {
        // Several pages, so a drag has somewhere to go.
        val r = reader("report")
        try {
            val before = r.camera()
            val from = r.center()
            compose.onNodeWithTag("reader").performTouchInput { swipe(from, from - Offset(0f, 300f), 300) }
            compose.pumpUntil { r.camera() != before }
            assertEquals(false, r.model.forwardsDrags.value)
        } finally {
            r.o.destroy()
        }
    }

    /**
     * The pan follows the finger from where it crossed the touch slop: a
     * short move just past the slop pans by its overshoot, not by the slop
     * as well. Compared with a long move, whose pan the slop barely changes.
     */
    @Test
    fun aPanStartsWhereTheFingerCrossedTheSlop() {
        val r = reader("report")
        try {
            val from = r.center()
            var slop = 0f
            fun panBy(extra: Float): Double {
                val before = checkNotNull(r.camera()).y
                compose.onNodeWithTag("reader").performTouchInput {
                    slop = viewConfiguration.touchSlop
                    down(from)
                    moveBy(Offset(0f, -(slop + 1f)))
                    moveBy(Offset(0f, -extra))
                }
                compose.pumpFor(400)
                val moved = checkNotNull(r.camera()).y - before
                // Lifted only once measured: the lift may fling.
                compose.onNodeWithTag("reader").performTouchInput { cancel() }
                compose.pumpFor(400)
                return moved
            }
            val short = panBy(4f)
            val long = panBy(200f)
            assertTrue("short $short, long $long", long > 0.0)
            // With the slop counted the ratio would be (slop + 5) / (slop + 201).
            val ratio = short / long
            assertTrue("short $short, long $long, slop $slop", abs(ratio - 5.0 / 201.0) < abs(ratio - (slop + 5.0) / (slop + 201.0)))
        } finally {
            r.o.destroy()
        }
    }

    /** An active drawing tool takes the drag, so the stage does not pan and the stroke reaches the tool. */
    @Test
    fun aDrawingToolConsumesTheDragAndTheStageDoesNotPan() {
        val r = reader()
        try {
            val annotations = engine { r.o.viewer.annotations(r.o.document) }
            engine { annotations.refresh() }
            val inks = { engine { annotations.list().count { it.subtype == AnnotationSubtype.INK } } }
            val before = inks()
            engine { r.o.viewer.interaction(r.o.document).activateTool(ToolId.INK) }
            compose.pumpUntil { r.model.forwardsDrags.value }
            val camera = r.camera()
            val from = r.center()
            val reader = compose.onNodeWithTag("reader")
            var strokes = 0
            // Under Robolectric the first stroke after the tool turns on does
            // not commit; a plain JVM model test commits it. The reader's part
            // is the same for every stroke, so the test draws until ink lands.
            compose.pumpUntil(message = { "after $strokes strokes" }) {
                // A frame between events, as a finger's samples arrive.
                reader.performTouchInput { down(from) }
                compose.pumpFrame()
                for (step in 1..6) {
                    reader.performTouchInput { moveTo(from + Offset(20f * step, 12f * step)) }
                    compose.pumpFrame()
                }
                reader.performTouchInput { up() }
                strokes += 1
                compose.pumpFor(200)
                engine { annotations.drafts.finishInkDraft() }
                compose.pumpFor(100)
                inks() > before
            }
            compose.pumpFor(200)
            assertEquals(camera, r.camera())
            assertEquals(null, r.model.error.value)
        } finally {
            r.o.destroy()
        }
    }

    @Test
    fun disablingPanLeavesTheCameraAlone() {
        val r = reader()
        try {
            compose.runOnIdle { r.model.isPanEnabled = false }
            val before = r.camera()
            val from = r.center()
            compose.onNodeWithTag("reader").performTouchInput { swipe(from, from - Offset(0f, 300f), 300) }
            compose.pumpFor(400)
            assertEquals(before, r.camera())
        } finally {
            r.o.destroy()
        }
    }

    /** An overlay that takes the touch's down keeps the reader from panning. */
    @Test
    fun anOverlayThatTakesTheTouchKeepsItFromThePan() {
        val o = opened(ViewerConfig(render = RenderConfig(), stage = StageConfig(), interaction = true, selection = true))
        try {
            val model = engine { StageReaderModel(o.viewer.stage(o.document), o.viewer.render(o.document)) }
            val selection = TextSelectionModel(engine { o.viewer.selection(o.document) })
            compose.setContent {
                LaunchedEffect(selection) { selection.run() }
                StageReader(model, Modifier.size(390.dp, 844.dp).testTag("reader")) { page ->
                    TextSelectionOverlay(selection, page, o.pages[page.pageIndex.toInt()], Modifier.fillMaxSize())
                }
            }
            compose.pumpUntil { model.visiblePages.value.isNotEmpty() }
            val r = Reader(o, model)
            val before = r.camera()
            val from = r.center()
            compose.onNodeWithTag("reader").performTouchInput { swipe(from, from - Offset(0f, 300f), 300) }
            compose.pumpFor(400)
            assertEquals(before, r.camera())
        } finally {
            o.destroy()
        }
    }

    private fun accessibility(enabled: Boolean) {
        val context = ApplicationProvider.getApplicationContext<Context>()
        shadowOf(context.getSystemService(AccessibilityManager::class.java)).setEnabled(enabled)
    }

    /** TalkBack names each visible page and scrolls the reader through its scroll actions. */
    @Test
    fun theReaderExposesItsPagesAndScrollToAccessibility() {
        accessibility(true)
        val r = reader("report")
        try {
            val count = r.o.pages.size
            compose.pumpUntil { runCatching { compose.onNodeWithContentDescription("Page 1 of $count").assertExists() }.isSuccess }
            compose.pumpUntil {
                compose.onNodeWithTag("reader").fetchSemanticsNode().config.contains(SemanticsProperties.VerticalScrollAxisRange)
            }
            val range = compose.onNodeWithTag("reader").fetchSemanticsNode().config[SemanticsProperties.VerticalScrollAxisRange]
            assertEquals(0f, range.value(), 1f)
            assertTrue(range.maxValue() > 0f)
            val before = r.camera()
            // At the top edge a scroll up goes nowhere.
            val up = compose.runOnIdle {
                compose.onNodeWithTag("reader").fetchSemanticsNode().config[SemanticsActions.ScrollBy].action!!(0f, -400f)
            }
            assertEquals(false, up)
            compose.onNodeWithTag("reader").performSemanticsAction(SemanticsActions.ScrollBy) { it(0f, 400f) }
            compose.pumpUntil { r.camera() != before }
            compose.pumpUntil {
                compose.onNodeWithTag("reader").fetchSemanticsNode().config[SemanticsProperties.VerticalScrollAxisRange].value() > 0f
            }
        } finally {
            r.o.destroy()
        }
    }

    /** Without an accessibility service the reader reads no scroll metrics. */
    @Test
    fun theReaderReadsNoScrollMetricsWithoutAccessibility() {
        accessibility(false)
        val r = reader("report")
        try {
            compose.pumpFor(300)
            assertEquals(false, compose.onNodeWithTag("reader").fetchSemanticsNode().config.contains(SemanticsProperties.VerticalScrollAxisRange))
        } finally {
            r.o.destroy()
        }
    }

    @Test
    fun aDoubleTapZooms() {
        val r = reader("report")
        try {
            val before = r.camera()
            val at = r.center()
            compose.onNodeWithTag("reader").performTouchInput { doubleClick(at) }
            compose.pumpUntil { r.camera() != before && !r.model.isZooming.value }
            assertNotEquals(before?.zoom, r.camera()?.zoom)
        } finally {
            r.o.destroy()
        }
    }
}
