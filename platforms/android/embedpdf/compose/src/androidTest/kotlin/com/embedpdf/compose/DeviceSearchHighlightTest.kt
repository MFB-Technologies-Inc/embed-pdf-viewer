package com.embedpdf.compose

import android.graphics.Bitmap
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.embedpdf.DeviceFixture
import com.embedpdf.PdfViewer
import com.embedpdf.Rect
import com.embedpdf.RenderConfig
import com.embedpdf.SearchConfig
import com.embedpdf.SearchHit
import com.embedpdf.SearchQuery
import com.embedpdf.StageConfig
import com.embedpdf.StageGoToOptions
import com.embedpdf.StageScrollBehavior
import com.embedpdf.StageScrollToOptions
import com.embedpdf.ViewerConfig
import com.embedpdf.models.SearchModel
import com.embedpdf.models.StageReaderModel
import com.embedpdf.onDevice
import com.embedpdf.render
import com.embedpdf.search
import com.embedpdf.stage
import kotlinx.coroutines.test.StandardTestDispatcher
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * `SearchHighlightOverlay` in a real window: the engine paints no hits into
 * the tiles, so the pixels of a hit change only when the overlay draws it.
 */
@RunWith(AndroidJUnit4::class)
class DeviceSearchHighlightTest {
    // A StandardTestDispatcher, for the reason DeviceStageReaderTest gives.
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>(effectContext = StandardTestDispatcher())

    /** The pixels of [rect], a rect in viewport dp, inset by 2 px so antialiased edges don't count. */
    private fun Bitmap.pixelsIn(rect: Rect, density: Float): IntArray {
        val left = (rect.x * density).roundToInt() + 2
        val top = (rect.y * density).roundToInt() + 2
        val right = ((rect.x + rect.width) * density).roundToInt() - 2
        val bottom = ((rect.y + rect.height) * density).roundToInt() - 2
        check(right > left && bottom > top) { "the hit's rect $rect is too small to sample" }
        check(left >= 0 && top >= 0 && right <= width && bottom <= height) { "the hit's rect $rect is off screen ($width x $height px)" }
        val pixels = IntArray((right - left) * (bottom - top))
        getPixels(pixels, 0, right - left, left, top, right - left, bottom - top)
        return pixels
    }

    private fun dark(color: Int): Boolean =
        (color ushr 16 and 0xff) < 128 && (color ushr 8 and 0xff) < 128 && (color and 0xff) < 128

    private fun differs(a: Int, b: Int): Boolean =
        listOf(16, 8, 0).any { shift -> abs((a ushr shift and 0xff) - (b ushr shift and 0xff)) > 24 }

    @Test
    fun searchingTraceColorsTheFirstHit() {
        val viewer = onDevice {
            PdfViewer(ViewerConfig(render = RenderConfig(), stage = StageConfig(), search = SearchConfig())).also { it.start() }
        }
        try {
            val document = onDevice { viewer.open(DeviceFixture.bytes("report")) }
            val pages = onDevice { viewer.pages(document) }
            val stage = onDevice { viewer.stage(document) }
            val capability = onDevice { viewer.search(document) }
            val reader = onDevice { StageReaderModel(stage, viewer.render(document)) }
            val search = SearchModel(capability)
            compose.setContent {
                LaunchedEffect(search) { search.run() }
                StageReader(reader, Modifier.fillMaxSize().testTag("reader")) { page ->
                    pages.firstOrNull { it.page == page.page }?.let { info ->
                        SearchHighlightOverlay(search, page, info, Modifier.fillMaxSize())
                    }
                }
            }
            compose.waitUntil(20_000) { reader.visiblePages.value.isNotEmpty() }

            // Find the first hit without starting the session, and bring it into view.
            val first: SearchHit = onDevice { capability.findAll(SearchQuery(text = "Trace")).first() }
            val box = first.segments.first().quad.let { Rect(it.upperLeft.x, it.upperLeft.y, it.lowerRight.x - it.upperLeft.x, it.lowerRight.y - it.upperLeft.y) }
            val onScreen = onDevice {
                stage.goToPage(first.page, StageGoToOptions(behavior = StageScrollBehavior.INSTANT))
                val viewport = stage.viewportSize()
                val placed = checkNotNull(stage.pageRectToViewport(first.page, box))
                val top = stage.scrollMetrics().scrollTop + placed.y - viewport.height / 3
                stage.scrollTo(StageScrollToOptions(top = top, behavior = StageScrollBehavior.INSTANT))
                checkNotNull(stage.pageRectToViewport(first.page, box))
            }
            val density = compose.density.density
            fun capture() = compose.onNodeWithTag("reader").captureToImage().asAndroidBitmap()
            // The tiles have landed when the hit's word shows dark pixels.
            var before = IntArray(0)
            compose.waitUntil(20_000) {
                before = capture().pixelsIn(onScreen, density)
                before.any(::dark)
            }
            // A moment more for the sharper tiles to replace the first ones.
            Thread.sleep(1_000)
            compose.waitForIdle()
            before = capture().pixelsIn(onScreen, density)

            search.text = "Trace"
            onDevice { search.searchSubmitted() }
            compose.waitUntil(20_000) { search.activeHit.value != null }
            val active = checkNotNull(search.activeHit.value)
            assertEquals(first.page, active.page)
            assertEquals(first.start, active.start, 0.0)
            // The search doesn't move the camera, so the hit is where it was.
            assertEquals(onScreen, onDevice { stage.pageRectToViewport(first.page, box) })

            var changed = 0
            var after = IntArray(0)
            compose.waitUntil(10_000) {
                after = capture().pixelsIn(onScreen, density)
                changed = before.indices.count { differs(before[it], after[it]) }
                changed > before.size / 2
            }
            val inkBefore = before.count(::dark)
            val inkAfter = after.count(::dark)
            Log.i(
                TAG,
                "first hit of 'Trace' at $onScreen dp: $changed of ${before.size} pixels changed color, " +
                    "dark $inkBefore -> $inkAfter; ${search.hitCount.value} hits",
            )
            assertTrue(changed > before.size / 2)
            // Multiplied, not painted over: the word still shows through the fill.
            assertTrue("the fill hid the word", inkAfter > inkBefore / 2)
        } finally {
            onDevice { viewer.destroy() }
        }
    }

    private companion object {
        const val TAG = "EmbedPdfDevice"
    }
}
