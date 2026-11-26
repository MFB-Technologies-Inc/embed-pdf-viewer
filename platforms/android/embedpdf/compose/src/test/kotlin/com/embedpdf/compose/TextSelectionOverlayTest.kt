package com.embedpdf.compose

import android.content.ClipboardManager
import android.content.Context
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.platform.LocalTextToolbar
import androidx.compose.ui.platform.TextToolbar
import androidx.compose.ui.platform.TextToolbarStatus
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performSemanticsAction
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipe
import androidx.compose.ui.unit.dp
import androidx.test.core.app.ApplicationProvider
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageGeometry
import com.embedpdf.Point
import com.embedpdf.Rect
import com.embedpdf.RenderRotation
import com.embedpdf.SelectionCapability
import com.embedpdf.Size
import com.embedpdf.ViewerConfig
import com.embedpdf.geometry
import com.embedpdf.models.TextSelectionModel
import com.embedpdf.selection
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import kotlin.math.abs
import androidx.compose.ui.geometry.Rect as LocalRect
import androidx.compose.ui.geometry.Size as LocalSize

/** `TextSelectionOverlayTests`, and the overlay driving a model over a real page. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class TextSelectionOverlayTest {
    @get:Rule
    val compose = createComposeRule()

    private val pageSize = Size(612.0, 792.0)

    /** The frame's top left corner for a rect of no size at [point], which is where `localRect` puts the point itself. */
    private fun localPoint(point: Point, size: LocalSize, geometry: PageGeometry): Offset =
        localRect(Rect(point.x, point.y, 0.0, 0.0), size, geometry).topLeft

    @Test
    fun unturnedFramePointScalesToThePage() {
        val geometry = PageGeometry(RenderRotation.NONE, pageSize)
        val size = LocalSize(306f, 396f)
        assertEquals(Point(306.0, 396.0), pagePoint(Offset(153f, 198f), size, geometry))
        assertEquals(Point(200.0, 100.0), pagePoint(Offset(100f, 50f), size, geometry))
    }

    @Test
    fun quarterTurnedFramePointTurnsBackToThePage() {
        val geometry = PageGeometry(RenderRotation.CLOCKWISE90, pageSize)
        // The page as shown is 792 by 612, drawn at half size.
        assertEquals(Point(306.0, 594.0), pagePoint(Offset(99f, 153f), LocalSize(396f, 306f), geometry))
    }

    @Test
    fun framePointScalesEachAxisByItsOwnLength() {
        val geometry = PageGeometry(RenderRotation.NONE, pageSize)
        // Twice as wide as the page and as tall as it.
        assertEquals(Point(100.0, 100.0), pagePoint(Offset(200f, 100f), LocalSize(1224f, 792f), geometry))
    }

    @Test
    fun localRectInvertsPagePoint() {
        for ((turn, size) in listOf(
            RenderRotation.NONE to LocalSize(306f, 396f),
            RenderRotation.CLOCKWISE90 to LocalSize(396f, 306f),
            RenderRotation.CLOCKWISE180 to LocalSize(306f, 396f),
            RenderRotation.CLOCKWISE270 to LocalSize(396f, 306f),
        )) {
            val geometry = PageGeometry(turn, pageSize)
            val start = Offset(99f, 153f)
            val local = localPoint(pagePoint(start, size, geometry), size, geometry)
            assertTrue("$turn: $local", abs(local.x - start.x) < 0.001f && abs(local.y - start.y) < 0.001f)
        }
    }

    @Test
    fun localRectScalesAPageRectToTheFrame() {
        val geometry = PageGeometry(RenderRotation.CLOCKWISE90, pageSize)
        // At 90 degrees (x, y) shows at (792 - y, x), so the rect shows at
        // x 492...592, y 100...300, drawn at half size.
        assertEquals(
            LocalRect(246f, 50f, 296f, 150f),
            localRect(Rect(100.0, 200.0, 200.0, 100.0), LocalSize(396f, 306f), geometry),
        )
    }

    @Test
    fun aFrameWithNoLengthOnAnAxisMapsThatAxisToZero() {
        val geometry = PageGeometry(RenderRotation.NONE, pageSize)
        val local = Offset(100f, 50f)
        assertEquals(Point(0.0, 0.0), pagePoint(local, LocalSize.Zero, geometry))
        assertEquals(Point(0.0, 100.0), pagePoint(local, LocalSize(0f, 396f), geometry))
        assertEquals(Point(200.0, 0.0), pagePoint(local, LocalSize(306f, 0f), geometry))
    }

    @Test
    fun aPageWithNoLengthOnAnAxisDrawsNothingOnThatAxis() {
        val size = LocalSize(306f, 396f)
        val rect = Rect(10.0, 20.0, 30.0, 40.0)
        assertEquals(LocalRect(0f, 0f, 0f, 0f), localRect(rect, size, PageGeometry(RenderRotation.NONE, Size(0.0, 0.0))))
        assertEquals(LocalRect(0f, 10f, 0f, 30f), localRect(rect, size, PageGeometry(RenderRotation.NONE, Size(0.0, 792.0))))
    }

    /** Records what the overlay asks of the platform toolbar. */
    private class RecordingToolbar : TextToolbar {
        var copy: (() -> Unit)? = null
        var shownAt: LocalRect? = null
        override var status: TextToolbarStatus = TextToolbarStatus.Hidden

        override fun showMenu(
            rect: LocalRect,
            onCopyRequested: (() -> Unit)?,
            onPasteRequested: (() -> Unit)?,
            onCutRequested: (() -> Unit)?,
            onSelectAllRequested: (() -> Unit)?,
        ) {
            shownAt = rect
            copy = onCopyRequested
            status = TextToolbarStatus.Shown
        }

        override fun hide() {
            status = TextToolbarStatus.Hidden
        }
    }

    private class Page(val o: Opened, val selection: SelectionCapability, val model: TextSelectionModel, val text: Point)

    /** The first page of `report` at half size, with a point on its text. */
    private fun page(): Page {
        val o = opened(ViewerConfig(interaction = true, selection = true))
        val page = o.pages[0]
        val selection = engine { o.viewer.selection(o.document) }
        val text = engine {
            selection.ensureLoaded(page.page)
            selection.selectPage(page.page)
            val first = selection.rects(page.page).first()
            selection.clear()
            Point(first.x + first.width / 2, first.y + first.height / 2)
        }
        return Page(o, selection, TextSelectionModel(selection), text)
    }

    private fun Page.show(toolbar: TextToolbar, activation: TextSelectionActivation = TextSelectionActivation.DRAG, shown: () -> Boolean = { true }) {
        val info = o.pages[0]
        compose.setContent {
            LaunchedEffect(model) { model.run() }
            CompositionLocalProvider(LocalTextToolbar provides toolbar) {
                Box(Modifier.size((info.width / 2).dp, (info.height / 2).dp)) {
                    if (shown()) TextSelectionOverlay(model, info.page, info.geometry, Modifier.fillMaxSize(), activation = activation)
                }
            }
        }
        compose.waitForIdle()
    }

    /** Where [point], in page space, is in the overlay's frame, in pixels. */
    private fun Page.local(point: Point): Offset {
        val bounds = compose.onNodeWithContentDescription("Text selection").fetchSemanticsNode().size
        val info = o.pages[0]
        return localPoint(point, LocalSize(bounds.width.toFloat(), bounds.height.toFloat()), info.geometry)
    }

    @Test
    fun aDragAcrossTextSelectsItAndOffersCopy() {
        val p = page()
        try {
            val toolbar = RecordingToolbar()
            p.show(toolbar)
            val from = p.local(p.text)
            val to = p.local(Point(p.text.x + 120, p.text.y))
            compose.onNodeWithContentDescription("Text selection").performTouchInput {
                down(from)
                moveTo(Offset((from.x + to.x) / 2, from.y))
                moveTo(to)
                up()
            }
            compose.pumpUntil { toolbar.copy != null }
            assertFalse(p.model.isSelecting.value)
            assertTrue(p.model.rects.value[p.o.pages[0].page].orEmpty().isNotEmpty())
            assertNotNull(toolbar.shownAt)
            toolbar.copy!!()
            val clipboard = ApplicationProvider.getApplicationContext<Context>().getSystemService(ClipboardManager::class.java)
            val expected = engine { p.selection.readText() }
            assertTrue(expected.isNotBlank())
            compose.pumpUntil { clipboard.primaryClip?.getItemAt(0)?.text?.toString() == expected }
            assertEquals(TextToolbarStatus.Hidden, toolbar.status)
        } finally {
            p.o.destroy()
        }
    }

    /** TalkBack: the overlay is labelled, and copies through its semantics action once text is selected. */
    @Test
    fun theCopyActionPutsTheSelectionOnTheClipboard() {
        val p = page()
        try {
            p.show(RecordingToolbar())
            val from = p.local(p.text)
            compose.onNodeWithContentDescription("Text selection").performTouchInput {
                down(from)
                moveTo(from + Offset(60f, 0f))
                up()
            }
            compose.pumpUntil { p.model.rects.value.isNotEmpty() && !p.model.isSelecting.value }
            compose.onNodeWithContentDescription("Text selection").performSemanticsAction(SemanticsActions.CopyText)
            val clipboard = ApplicationProvider.getApplicationContext<Context>().getSystemService(ClipboardManager::class.java)
            compose.pumpUntil { !clipboard.primaryClip?.getItemAt(0)?.text.isNullOrBlank() }
        } finally {
            p.o.destroy()
        }
    }

    /** A plain drag is not the long-press overlay's; a long press, then a move, selects. */
    @Test
    fun theLongPressOverlayLeavesAPlainDragAndSelectsAfterAPress() {
        val p = page()
        try {
            p.show(RecordingToolbar(), TextSelectionActivation.LONG_PRESS)
            val from = p.local(p.text)
            val to = p.local(Point(p.text.x + 120, p.text.y))
            compose.onNodeWithContentDescription("Text selection").performTouchInput { swipe(from, to, 200) }
            compose.pumpFor(200)
            assertTrue(p.model.rects.value.isEmpty())
            compose.onNodeWithContentDescription("Text selection").performTouchInput {
                down(from)
                advanceEventTime(viewConfiguration.longPressTimeoutMillis + 100)
                moveTo(Offset((from.x + to.x) / 2, from.y))
                moveTo(to)
                up()
            }
            compose.pumpUntil { p.model.rects.value.isNotEmpty() && !p.model.isSelecting.value }
        } finally {
            p.o.destroy()
        }
    }

    /** A drag still open when the overlay leaves the composition is ended. */
    @Test
    fun leavingMidDragEndsTheSelection() {
        val p = page()
        try {
            var shown by mutableStateOf(true)
            p.show(RecordingToolbar(), shown = { shown })
            val from = p.local(p.text)
            compose.onNodeWithContentDescription("Text selection").performTouchInput {
                down(from)
                moveTo(from + Offset(60f, 0f))
            }
            compose.pumpUntil { p.model.isSelecting.value }
            compose.runOnIdle { shown = false }
            compose.pumpUntil { !p.model.isSelecting.value }
            assertTrue(p.model.rects.value.isNotEmpty())
        } finally {
            p.o.destroy()
        }
    }
}
