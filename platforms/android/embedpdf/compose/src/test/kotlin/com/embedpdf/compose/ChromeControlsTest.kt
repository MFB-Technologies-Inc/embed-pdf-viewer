package com.embedpdf.compose

import androidx.compose.foundation.layout.width
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.unit.Density
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertIsNotSelected
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performImeAction
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageEditTurn
import com.embedpdf.SearchConfig
import com.embedpdf.SearchHit
import com.embedpdf.ToolId
import com.embedpdf.ViewerConfig
import com.embedpdf.history
import com.embedpdf.interaction
import com.embedpdf.models.HistoryModel
import com.embedpdf.models.SearchModel
import com.embedpdf.models.ToolModel
import com.embedpdf.pageEdit
import com.embedpdf.search
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

/** `SearchBar`, `HistoryControls` and `ToolPicker` over live models. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ChromeControlsTest {
    @get:Rule
    val compose = createComposeRule()

    private fun searchBar(direction: LayoutDirection = LayoutDirection.Ltr, onActiveHit: (SearchHit) -> Unit = {}): Pair<Opened, SearchModel> {
        val o = opened(ViewerConfig(search = SearchConfig()))
        val model = SearchModel(engine { o.viewer.search(o.document) })
        compose.setContent {
            CompositionLocalProvider(LocalLayoutDirection provides direction) {
                SearchBar(model, Modifier.width(480.dp), onActiveHit)
            }
        }
        compose.waitForIdle()
        return o to model
    }

    @Test
    fun searchingStepsThroughTheHitsAndClears() {
        val reported = mutableListOf<SearchHit>()
        val (o, model) = searchBar { reported += it }
        try {
            compose.onNodeWithTag("searchPrevious").assertIsNotEnabled()
            compose.onNodeWithTag("searchNext").assertIsNotEnabled()
            compose.onNodeWithTag("searchClear").assertIsNotEnabled()
            compose.onNodeWithTag("searchField").performTextInput("the")
            assertEquals("the", model.text)
            compose.onNodeWithTag("searchClear").assertIsEnabled()
            compose.onNodeWithTag("searchField").performImeAction()
            compose.pumpUntil { model.hitCount.value > 1 && reported.isNotEmpty() }
            compose.onNodeWithTag("searchNext").assertIsEnabled()
            compose.onNodeWithTag("searchSummary").assertExists()
            compose.onNodeWithTag("searchNext").performClick()
            compose.pumpUntil { model.activeHitIndex.value == 1 && reported.size >= 2 }
            assertEquals(model.activeHit.value, reported.last())
            // The count can still grow while the scan runs, so only the place is fixed.
            compose.pumpFrame()
            val shown = compose.onNodeWithTag("searchSummary").fetchSemanticsNode().config[SemanticsProperties.Text].single().text
            assertTrue(shown, shown.startsWith("2 of "))
            compose.onNodeWithTag("searchClear").performClick()
            compose.pumpUntil { model.hitCount.value == 0 }
            assertEquals("", model.text)
            compose.pumpFrame()
            compose.onNodeWithTag("searchNext").assertIsNotEnabled()
            compose.onNodeWithTag("searchClear").assertIsNotEnabled()
            // TalkBack: every control has a label and a role.
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
        } finally {
            o.destroy()
        }
    }

    @Test
    fun aHitHeldWhenTheBarAppearsIsNotReported() {
        val o = opened(ViewerConfig(search = SearchConfig()))
        val model = SearchModel(engine { o.viewer.search(o.document) })
        try {
            compose.onMain {
                model.text = "the"
                model.searchSubmitted()
            }
            compose.pumpUntil { model.activeHit.value != null }
            val reported = mutableListOf<SearchHit>()
            compose.setContent { SearchBar(model, onActiveHit = { reported += it }) }
            compose.pumpFor(200)
            assertTrue("$reported", reported.isEmpty())
        } finally {
            o.destroy()
        }
    }

    /** Review focus: under a right-to-left layout the field starts at the right and the buttons follow it leftwards. */
    @Test
    fun aRightToLeftBarMirrors() {
        val (o, _) = searchBar(LayoutDirection.Rtl)
        try {
            val field = compose.onNodeWithTag("searchField").fetchSemanticsNode().boundsInRoot
            val next = compose.onNodeWithContentDescription("Next match").fetchSemanticsNode().boundsInRoot
            val clear = compose.onNodeWithContentDescription("Clear search").fetchSemanticsNode().boundsInRoot
            assertTrue("$field, $next", field.left > next.left)
            assertTrue("$next, $clear", next.left > clear.left)
        } finally {
            o.destroy()
        }
    }

    /** Swift stacks the controls at the accessibility text sizes; the bar does at a font scale of 1.5 and up. */
    @Test
    fun aLargeFontScaleStacksTheButtonsUnderTheField() {
        val o = opened(ViewerConfig(search = SearchConfig()))
        val model = SearchModel(engine { o.viewer.search(o.document) })
        try {
            compose.setContent {
                val density = LocalDensity.current
                CompositionLocalProvider(LocalDensity provides Density(density.density, fontScale = 2f)) { SearchBar(model) }
            }
            val field = compose.onNodeWithTag("searchField").fetchSemanticsNode().boundsInRoot
            val next = compose.onNodeWithTag("searchNext").fetchSemanticsNode().boundsInRoot
            assertTrue("$field, $next", next.top >= field.bottom)
        } finally {
            o.destroy()
        }
    }

    @Test
    fun historyButtonsFollowTheHistory() {
        val o = opened(ViewerConfig(history = true, pageEdit = true))
        val model = HistoryModel(o.viewer, engine { o.viewer.history(o.document) })
        try {
            compose.setContent { HistoryControls(model) }
            compose.pumpFor(100)
            compose.onNodeWithTag("historyUndo").assertIsNotEnabled()
            compose.onNodeWithTag("historyRedo").assertIsNotEnabled()
            engine { o.viewer.pageEdit(o.document).rotate(listOf(o.pages[0].page), PageEditTurn.CLOCKWISE) }
            compose.pumpUntil { model.canUndo.value }
            compose.pumpFrame()
            compose.onNodeWithTag("historyUndo").assertIsEnabled().performClick()
            compose.pumpUntil { model.canRedo.value && !model.submitting.value }
            compose.pumpFrame()
            compose.onNodeWithTag("historyRedo").assertIsEnabled()
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
            compose.onAllNodes(hasClickAction()).assertAll(SemanticsMatcher.keyIsDefined(SemanticsProperties.Role))
        } finally {
            o.destroy()
        }
    }

    @Test
    fun toolChipsToggleTheActiveToolAndExposeTheirState() {
        val o = opened(ViewerConfig(interaction = true, annotation = ""), "annotation_kinds")
        val model = ToolModel(engine { o.viewer.interaction(o.document) })
        val unknown = ToolId("not-registered")
        try {
            compose.setContent { ToolPicker(model, listOf(ToolId.POINTER, ToolId.INK, unknown), label = { toolLabel(it) }) }
            compose.pumpUntil { model.activeToolId.value != null }
            compose.pumpFrame()
            compose.onNodeWithTag("toolPicker-pointer").assertIsSelected()
            compose.onNodeWithTag("toolPicker-ink").assertIsNotSelected().assertIsEnabled()
            compose.onNodeWithTag("toolPicker-not-registered").assertIsNotEnabled()
            compose.onNodeWithTag("toolPicker-ink").performClick()
            compose.pumpUntil { model.activeToolId.value == ToolId.INK && !model.submitting.value }
            compose.pumpFrame()
            compose.onNodeWithTag("toolPicker-ink").assertIsSelected()
            compose.onNodeWithTag("toolPicker-pointer").assertIsNotSelected()
            compose.onNodeWithTag("toolPicker-ink").performClick()
            compose.pumpUntil { model.activeToolId.value == ToolId.POINTER && !model.submitting.value }
            compose.pumpFrame()
            compose.onNodeWithTag("toolPicker-ink").assertIsNotSelected()
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
        } finally {
            o.destroy()
        }
    }
}
