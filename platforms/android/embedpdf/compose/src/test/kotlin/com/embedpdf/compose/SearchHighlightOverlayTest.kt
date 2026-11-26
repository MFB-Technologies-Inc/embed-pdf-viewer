package com.embedpdf.compose

import android.graphics.Bitmap
import androidx.activity.ComponentActivity
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.isFocusable
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onChildren
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageGeometry
import com.embedpdf.PageInfo
import com.embedpdf.RenderRotation
import com.embedpdf.SearchBlendMode
import com.embedpdf.SearchCapability
import com.embedpdf.SearchConfig
import com.embedpdf.SearchHighlightPatch
import com.embedpdf.SearchHit
import com.embedpdf.SearchSettingsPatch
import com.embedpdf.Size
import com.embedpdf.ViewerConfig
import com.embedpdf.geometry
import com.embedpdf.models.SearchModel
import com.embedpdf.search
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import kotlin.math.abs
import androidx.compose.ui.geometry.Offset as LocalOffset
import androidx.compose.ui.geometry.Size as LocalSize

/** The search highlights of one page, drawn over white so a multiplied fill shows its own color. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class SearchHighlightOverlayTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    private class Search(
        val o: Opened,
        val capability: SearchCapability,
        val model: SearchModel,
        val info: PageInfo,
        val active: SearchHit,
        val other: SearchHit,
    )

    /** A search for "the" in `report`, on the page of its active hit, with another hit on that page. */
    private fun search(): Search {
        val o = opened(ViewerConfig(search = SearchConfig()))
        val capability = engine { o.viewer.search(o.document) }
        val model = SearchModel(capability)
        model.text = "the"
        compose.onMain { model.searchSubmitted() }
        val active = checkNotNull(model.activeHit.value)
        val other = model.hits.value.first { it != active && it.page == active.page && it.segments.isNotEmpty() }
        val info = o.pages.first { it.page == active.page }
        return Search(o, capability, model, info, active, other)
    }

    private fun Search.show(geometry: PageGeometry, color: Color? = null) {
        val shown = geometry.displaySize
        compose.setContent {
            LaunchedEffect(model) { model.run() }
            Box(Modifier.size((shown.width / 2).dp, (shown.height / 2).dp).background(Color.White).testTag("page")) {
                SearchHighlightOverlay(model, info.page, geometry, Modifier.fillMaxSize(), color = color)
            }
        }
        compose.pumpFor(100)
    }

    /** The pixel at the middle of [hit]'s first line in the drawn page. */
    private fun Bitmap.middleOf(hit: SearchHit, geometry: PageGeometry): Int {
        val quad = hit.segments.first().quad
        val box = com.embedpdf.Rect(quad.upperLeft.x, quad.upperLeft.y, quad.lowerRight.x - quad.upperLeft.x, quad.lowerRight.y - quad.upperLeft.y)
        val local = localRect(box, LocalSize(width.toFloat(), height.toFloat()), geometry)
        val at: LocalOffset = local.center
        return getPixel(at.x.toInt(), at.y.toInt())
    }

    private fun assertColor(expected: Color, actual: Int) {
        val want = expected.toArgb()
        for (shift in listOf(16, 8, 0)) {
            val a = want ushr shift and 0xff
            val b = actual ushr shift and 0xff
            assertTrue("expected #%06x, drew #%06x".format(want and 0xffffff, actual and 0xffffff), abs(a - b) <= 3)
        }
    }

    @Test
    fun hitsTakeTheHighlightColorAndTheActiveHitTheActiveColor() {
        val s = search()
        try {
            val geometry = s.info.geometry
            s.show(geometry)
            val image = compose.drawnImage("page")
            // Multiplied over white, each fill is its own color.
            assertColor(DEFAULT_ACTIVE_COLOR, image.middleOf(s.active, geometry))
            assertColor(DEFAULT_COLOR, image.middleOf(s.other, geometry))
            // A corner of the page holds no hit.
            assertColor(Color.White, image.getPixel(1, 1))
        } finally {
            s.o.destroy()
        }
    }

    @Test
    fun aTurnedPagePlacesTheHitsWhereThePageShowsThem() {
        val s = search()
        try {
            val turned = PageGeometry(RenderRotation.CLOCKWISE90, Size(s.info.width, s.info.height))
            s.show(turned)
            val image = compose.drawnImage("page")
            assertTrue(image.width > image.height)
            assertColor(DEFAULT_ACTIVE_COLOR, image.middleOf(s.active, turned))
            assertColor(DEFAULT_COLOR, image.middleOf(s.other, turned))
        } finally {
            s.o.destroy()
        }
    }

    @Test
    fun aSettingsChangeRedrawsInTheNewColors() {
        val s = search()
        try {
            val geometry = s.info.geometry
            s.show(geometry)
            engine {
                s.capability.updateSettings(
                    SearchSettingsPatch(highlight = SearchHighlightPatch(color = "#00ff00", activeColor = "rgb(0 0 255)", blendMode = SearchBlendMode.NORMAL)),
                )
            }
            compose.pumpUntil { s.model.highlight.value.color == "#00ff00" }
            compose.pumpFor(50)
            val image = compose.drawnImage("page")
            assertColor(Color(0xFF0000FF), image.middleOf(s.active, geometry))
            assertColor(Color(0xFF00FF00), image.middleOf(s.other, geometry))
        } finally {
            s.o.destroy()
        }
    }

    @Test
    fun aColorGivenToTheOverlayWinsOverTheSetting() {
        val s = search()
        try {
            val geometry = s.info.geometry
            s.show(geometry, color = Color(0xFFFF0000))
            val image = compose.drawnImage("page")
            assertColor(Color(0xFFFF0000), image.middleOf(s.other, geometry))
            assertColor(DEFAULT_ACTIVE_COLOR, image.middleOf(s.active, geometry))
        } finally {
            s.o.destroy()
        }
    }

    /** Decoration only: the overlay adds no semantics node, so nothing in it can take focus. */
    @Test
    fun theOverlayHasNoSemantics() {
        val s = search()
        try {
            s.show(s.info.geometry)
            compose.onNodeWithTag("page").onChildren().assertCountEquals(0)
            compose.onAllNodes(isFocusable()).assertCountEquals(0)
        } finally {
            s.o.destroy()
        }
    }

    @Test
    fun cssColorsReadAsTheWebReadsThem() {
        assertEquals(Color(0xFFFFD500), cssColor("#ffd500"))
        assertEquals(Color(0xFFFFDD55), cssColor("#fd5"))
        assertEquals(Color(0x80FF0000), cssColor("#ff000080"))
        assertEquals(Color(0xFF0000FF), cssColor(" Blue "))
        assertEquals(Color(0xFF008000), cssColor("green"))
        assertEquals(Color(1f, 0f, 0f, 0.5f), cssColor("rgba(255, 0, 0, 0.5)"))
        assertEquals(Color(1f, 0f, 0f, 0.25f), cssColor("rgb(100% 0 0 / 25%)"))
        assertNull(cssColor("var(--accent)"))
        assertNull(cssColor("#ggg"))
        assertNull(cssColor("rgb(1, 2)"))
    }

    @Test
    fun eachBlendModeHasItsComposeMode() {
        assertEquals(BlendMode.Multiply, drawableBlendMode(SearchBlendMode.MULTIPLY))
        assertEquals(BlendMode.SrcOver, drawableBlendMode(SearchBlendMode.NORMAL))
        assertEquals(BlendMode.Hardlight, drawableBlendMode(SearchBlendMode.HARD_LIGHT))
        assertEquals(BlendMode.Luminosity, drawableBlendMode(SearchBlendMode.LUMINOSITY))
        assertEquals(SearchBlendMode.entries.size, SearchBlendMode.entries.map(::drawableBlendMode).toSet().size)
    }
}
