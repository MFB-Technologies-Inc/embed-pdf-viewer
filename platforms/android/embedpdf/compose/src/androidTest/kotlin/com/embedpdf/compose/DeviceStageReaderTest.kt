package com.embedpdf.compose

import android.graphics.Bitmap
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.pinch
import androidx.compose.ui.test.swipe
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.embedpdf.AnnotationSubtype
import com.embedpdf.DeviceFixture
import com.embedpdf.DocumentInfo
import com.embedpdf.PageInfo
import com.embedpdf.PdfViewer
import com.embedpdf.RenderConfig
import com.embedpdf.StageConfig
import com.embedpdf.ToolId
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.models.StageReaderModel
import com.embedpdf.onDevice
import com.embedpdf.render
import com.embedpdf.stage
import kotlinx.coroutines.test.StandardTestDispatcher
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * `StageReader` in a real window with real touch input, where the JVM tests
 * run Robolectric's paused looper.
 */
@RunWith(AndroidJUnit4::class)
class DeviceStageReaderTest {
    /*
     * Effects run on a StandardTestDispatcher. The rule's default,
     * UnconfinedTestDispatcher, resumes an effect's engine call on the native
     * thread that delivers the answer, inside JNA's callback; the call's next
     * poll then answers through a nested callback, and JNA detaching the
     * thread when that returns aborts the process on Android. An app's
     * effects run on the main thread's dispatcher, which never resumes
     * inline. The README's device run records it.
     */
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>(effectContext = StandardTestDispatcher())

    private class Reader(val viewer: PdfViewer, val document: DocumentInfo, val pages: List<PageInfo>, val model: StageReaderModel) {
        fun destroy() = onDevice { viewer.destroy() }
    }

    private fun reader(): Reader {
        val (viewer, document, pages) = onDevice {
            val viewer = PdfViewer(ViewerConfig(render = RenderConfig(), interaction = true, stage = StageConfig(), link = true, annotation = ""))
            viewer.start()
            val document = viewer.open(DeviceFixture.bytes("report"))
            Triple(viewer, document, viewer.pages(document))
        }
        val model = onDevice {
            StageReaderModel(viewer.stage(document), viewer.render(document), viewer.interaction(document), viewer.link(document))
        }
        compose.setContent {
            StageReader(model, Modifier.fillMaxSize().testTag("reader")) { page ->
                Box(Modifier.fillMaxSize().testTag("overlay"))
            }
        }
        compose.waitUntil(20_000) { model.visiblePages.value.isNotEmpty() && model.activeToolId.value != null }
        return Reader(viewer, document, pages, model)
    }

    /** A point a third of the way down the first visible page, in the reader's pixels. */
    private fun Reader.onPage(): Offset {
        val page = model.visiblePages.value.first()
        val scale = compose.density.density
        return Offset(
            ((page.screenX + page.transform.viewWidth / 2) * scale).toFloat(),
            ((page.screenY + page.transform.viewHeight / 3) * scale).toFloat(),
        )
    }

    private fun Bitmap.darkSamples(): Int {
        var dark = 0
        for (y in 0 until height step 4) {
            for (x in 0 until width step 4) {
                val color = getPixel(x, y)
                if ((color ushr 24) > 128 && (color ushr 16 and 0xff) < 128 && (color ushr 8 and 0xff) < 128 && (color and 0xff) < 128) dark++
            }
        }
        return dark
    }

    @Test
    fun theReaderShowsTheDocumentAndNamesItsPages() {
        val r = reader()
        try {
            val count = r.pages.size
            assertEquals(14, count)
            compose.waitUntil(20_000) { compose.onAllNodesWithContentDescription("Page 1 of $count").fetchSemanticsNodes().isNotEmpty() }
            compose.onNodeWithContentDescription("Document pages").assertExists()
            // The page's text is drawn: the window holds dark pixels once the tiles land.
            var dark = 0
            compose.waitUntil(20_000) {
                dark = compose.onNodeWithTag("reader").captureToImage().asAndroidBitmap().darkSamples()
                dark > 50
            }
            Log.i(TAG, "reader shows $dark dark samples; visible pages ${r.model.visiblePages.value.map { it.pageIndex }}")
        } finally {
            r.destroy()
        }
    }

    @Test
    fun aDragPansAndAPinchZooms() {
        val r = reader()
        try {
            val before = r.model.camera.value
            val from = r.onPage()
            compose.onNodeWithTag("reader").performTouchInput { swipe(from, from - Offset(0f, 400f), 300) }
            compose.waitUntil(10_000) { r.model.camera.value != before }
            Log.i(TAG, "pan: $before -> ${r.model.camera.value}")

            compose.waitForIdle()
            val panned = r.model.camera.value
            val center = r.onPage()
            compose.onNodeWithTag("reader").performTouchInput {
                pinch(
                    start0 = center - Offset(40f, 0f),
                    end0 = center - Offset(240f, 0f),
                    start1 = center + Offset(40f, 0f),
                    end1 = center + Offset(240f, 0f),
                    durationMillis = 400,
                )
            }
            compose.waitUntil(10_000) { r.model.camera.value?.zoom != panned?.zoom && !r.model.isZooming.value }
            Log.i(TAG, "pinch: zoom ${panned?.zoom} -> ${r.model.camera.value?.zoom}")
            assertTrue((r.model.camera.value?.zoom ?: 0.0) > (panned?.zoom ?: 0.0))
        } finally {
            r.destroy()
        }
    }

    /**
     * Under Robolectric the first stroke after the tool turns on did not
     * commit, so the JVM test draws until ink lands. On a device the first
     * stroke has to.
     */
    @Test
    fun theFirstInkStrokeCreatesAnInkAnnotation() {
        val r = reader()
        try {
            val annotations = onDevice {
                val annotations = r.viewer.annotations(r.document)
                annotations.refresh()
                annotations
            }
            val inks = { onDevice { annotations.list().count { it.subtype == AnnotationSubtype.INK } } }
            val before = inks()
            onDevice { r.viewer.interaction(r.document).activateTool(ToolId.INK) }
            compose.waitUntil(10_000) { r.model.forwardsDrags.value }
            val camera = r.model.camera.value
            val from = r.onPage()
            compose.onNodeWithTag("reader").performTouchInput {
                down(from)
                for (step in 1..6) moveTo(from + Offset(20f * step, 12f * step))
                up()
            }
            compose.waitForIdle()
            onDevice { annotations.drafts.finishInkDraft() }
            var after = before
            val landed = runCatching {
                compose.waitUntil(5_000) {
                    after = inks()
                    after > before
                }
            }.isSuccess
            Log.i(TAG, "ink annotations after the first stroke: $before -> $after (landed $landed)")
            assertTrue("the first stroke made no ink annotation", landed)
            assertEquals(camera, r.model.camera.value)
            assertEquals(null, r.model.error.value)
        } finally {
            r.destroy()
        }
    }

    private companion object {
        const val TAG = "EmbedPdfDevice"
    }
}
