package com.embedpdf.compose

import androidx.compose.foundation.layout.size
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.assertIsNotSelected
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageInfo
import com.embedpdf.RenderConfig
import com.embedpdf.ViewerConfig
import com.embedpdf.models.ThumbnailModel
import com.embedpdf.render
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ThumbnailStripTest {
    @get:Rule
    val compose = createComposeRule()

    private fun strip(direction: LayoutDirection = LayoutDirection.Ltr, onSelect: (PageInfo) -> Unit = {}): Pair<Opened, ThumbnailModel> {
        val o = opened(ViewerConfig(render = RenderConfig()))
        val model = ThumbnailModel(engine { o.viewer.render(o.document) }, o.pages)
        compose.setContent {
            CompositionLocalProvider(LocalLayoutDirection provides direction) {
                ThumbnailStrip(model, o.pages, o.pages[1].page, onSelect, Modifier.size(400.dp, 120.dp))
            }
        }
        return o to model
    }

    @Test
    fun cellsAreLabelledButtonsWithTheCurrentPageSelected() {
        val selected = mutableListOf<PageInfo>()
        val (o, _) = strip { selected += it }
        try {
            compose.onNodeWithContentDescription("Page 1").assertIsNotSelected()
            compose.onNodeWithContentDescription("Page 2").assertIsSelected()
            compose.onNodeWithContentDescription("Page 1").performClick()
            assertEquals(listOf(o.pages[0]), selected)
            // TalkBack: every control has a label and a role.
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
            compose.onAllNodes(hasClickAction()).assertAll(SemanticsMatcher.keyIsDefined(SemanticsProperties.Role))
        } finally {
            o.destroy()
        }
    }

    @Test
    fun visibleCellsLoadTheirThumbnails() {
        val (o, model) = strip()
        try {
            compose.pumpUntil { model.images.value.containsKey(o.pages[0].page) }
            assertTrue(model.error.value == null)
        } finally {
            o.destroy()
        }
    }

    /** Review focus: under a right-to-left layout the first page sits at the right. */
    @Test
    fun aRightToLeftStripStartsAtTheRight() {
        val (o, _) = strip(LayoutDirection.Rtl)
        try {
            val first = compose.onNodeWithContentDescription("Page 1").fetchSemanticsNode().boundsInRoot
            val second = compose.onNodeWithContentDescription("Page 2").fetchSemanticsNode().boundsInRoot
            assertTrue("$first, $second", first.left > second.left)
            assertTrue(compose.onAllNodesWithContentDescription("Page", substring = true).fetchSemanticsNodes().size >= 2)
        } finally {
            o.destroy()
        }
    }
}
