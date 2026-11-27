package com.embedpdf.compose

import androidx.compose.foundation.layout.size
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.referentialEqualityPolicy
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.performTextReplacement
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.OutlineCapability
import com.embedpdf.OutlineCreateOptions
import com.embedpdf.OutlineItemInput
import com.embedpdf.ResourceStatus
import com.embedpdf.ViewerConfig
import com.embedpdf.models.OutlineModel
import com.embedpdf.outline
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class OutlinePanelTest {
    @get:Rule
    val compose = createComposeRule()

    private fun model(o: Opened): OutlineModel = OutlineModel(o.viewer, engine { o.viewer.outline(o.document) })

    /** Edits the outline through the capability on the engine's side, before the panel shows it. */
    private fun seed(o: Opened, edit: suspend OutlineCapability.() -> Unit) = engine {
        val outline = o.viewer.outline(o.document)
        outline.ensureLoaded()
        outline.edit()
    }

    private fun show(model: OutlineModel) {
        compose.setContent { OutlinePanel(model, Modifier.size(360.dp, 640.dp)) }
        compose.pumpUntil { model.status.value == ResourceStatus.READY && model.canEdit.value }
        compose.pumpFrame()
    }

    @Test
    fun addingABookmarkThroughTheFormShowsItsRow() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            show(model)
            compose.onNodeWithText("No bookmarks").assertExists()
            compose.onNodeWithTag("outlineAdd").assertIsEnabled().performClick()
            compose.onNodeWithText("New bookmark").assertExists()
            compose.onNodeWithTag("outlineTitle").performTextInput("First")
            compose.onNodeWithText("None").performClick()
            compose.onNodeWithTag("outlineSave").performClick()
            compose.pumpUntil { model.items.value.any { it.title == "First" } }
            compose.pumpUntilGone(hasText("New bookmark"))
            compose.onNodeWithText("First").assertExists()
            // TalkBack: every control has a label and a role.
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
            compose.onAllNodes(hasClickAction()).assertAll(SemanticsMatcher.keyIsDefined(SemanticsProperties.Role))
        } finally {
            o.destroy()
        }
    }

    @Test
    fun aParentRowReadsItsExpansionAndCollapses() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            seed(o) {
                val parent = create(OutlineItemInput(title = "Parent", clearTarget = true), OutlineCreateOptions())
                create(OutlineItemInput(title = "Child", parent = parent.itemRef, clearTarget = true), OutlineCreateOptions())
                expand(parent.itemRef)
            }
            show(model)
            compose.onNodeWithText("Child").assertExists()
            compose.onNodeWithContentDescription("Collapse")
                .assert(SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Expanded"))
                .performClick()
            compose.pumpUntil { model.rows.value.none { it.item.title == "Child" } }
            compose.pumpFrame()
            compose.onNodeWithText("Child").assertDoesNotExist()
            compose.onNodeWithContentDescription("Expand")
                .assert(SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Collapsed"))
        } finally {
            o.destroy()
        }
    }

    /** An open edit form comes back after a configuration change with what was typed. */
    @Test
    fun anEditFormOutlivesARestore() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            seed(o) { create(OutlineItemInput(title = "Kept", clearTarget = true), OutlineCreateOptions()) }
            val restoration = StateRestorationTester(compose)
            restoration.setContent { OutlinePanel(model, Modifier.size(360.dp, 640.dp)) }
            compose.pumpUntil { model.status.value == ResourceStatus.READY && model.canEdit.value && model.items.value.isNotEmpty() }
            compose.pumpFrame()
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Edit…").performClick()
            compose.onNodeWithTag("outlineTitle").performTextReplacement("Kept renamed")
            restoration.emulateSavedInstanceStateRestore()
            compose.pumpFor(100)
            compose.onNodeWithText("Edit bookmark").assertExists()
            compose.onNodeWithTag("outlineTitle").assert(hasText("Kept renamed"))
            compose.onNodeWithTag("outlineSave").performClick()
            compose.pumpUntil { model.items.value.any { it.title == "Kept renamed" } }
        } finally {
            o.destroy()
        }
    }

    /** A move form and a delete confirmation come back too. */
    @Test
    fun aMoveFormAndADeleteConfirmationOutliveARestore() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            seed(o) { create(OutlineItemInput(title = "Kept", clearTarget = true), OutlineCreateOptions()) }
            val restoration = StateRestorationTester(compose)
            restoration.setContent { OutlinePanel(model, Modifier.size(360.dp, 640.dp)) }
            compose.pumpUntil { model.status.value == ResourceStatus.READY && model.canEdit.value && model.items.value.isNotEmpty() }
            compose.pumpFrame()
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Move…").performClick()
            restoration.emulateSavedInstanceStateRestore()
            compose.pumpFor(100)
            compose.onNodeWithText("Move bookmark").assertExists()
            compose.onNodeWithText("Cancel").performClick()
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Delete…").performClick()
            restoration.emulateSavedInstanceStateRestore()
            compose.pumpFor(100)
            compose.onNodeWithText("Delete bookmark and its children?").assertExists()
        } finally {
            o.destroy()
        }
    }

    @Test
    fun deletingFromTheRowMenuAsksFirst() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            seed(o) { create(OutlineItemInput(title = "Doomed", clearTarget = true), OutlineCreateOptions()) }
            show(model)
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Delete…").performClick()
            compose.onNodeWithText("Delete bookmark and its children?").assertExists()
            compose.onNode(hasText("Delete") and hasClickAction()).performClick()
            compose.pumpUntil { model.items.value.isEmpty() }
            compose.pumpFrame()
            compose.onNodeWithText("Doomed").assertDoesNotExist()
        } finally {
            o.destroy()
        }
    }

    @Test
    fun selectingFromTheMenuMarksTheRow() {
        val o = opened(ViewerConfig(outline = true))
        val model = model(o)
        try {
            seed(o) { create(OutlineItemInput(title = "Pick me", clearTarget = true), OutlineCreateOptions(select = false)) }
            show(model)
            assertEquals(0, compose.onAllNodesWithContentDescription("Selected").fetchSemanticsNodes().size)
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Select").performClick()
            compose.pumpUntil { model.selectedItems.value.isNotEmpty() }
            compose.pumpFrame()
            compose.onNode(hasContentDescription("Selected")).assertExists()
        } finally {
            o.destroy()
        }
    }

    /**
     * `PanelPresentationTests`: replacing the model closes the form the old
     * model's panel had open, so its save never reaches either model.
     */
    @Test
    fun modelReplacementClosesTheOpenFormAndLeavesBothModelsAlone() {
        val o = opened(ViewerConfig(outline = true))
        val other = opened(ViewerConfig(outline = true))
        val firstModel = model(o)
        val secondModel = model(other)
        try {
            seed(o) { create(OutlineItemInput(title = "First", clearTarget = true), OutlineCreateOptions()) }
            seed(other) { create(OutlineItemInput(title = "Second", clearTarget = true), OutlineCreateOptions()) }
            var shown by mutableStateOf(firstModel, referentialEqualityPolicy())
            compose.setContent { OutlinePanel(shown, Modifier.size(360.dp, 640.dp)) }
            compose.pumpUntil { firstModel.canEdit.value && firstModel.items.value.isNotEmpty() }
            compose.pumpFrame()
            compose.onNodeWithContentDescription("Bookmark actions").performClick()
            compose.onNodeWithText("Edit…").performClick()
            compose.onNodeWithText("Edit bookmark").assertExists()
            compose.onNodeWithTag("outlineTitle").performTextInput(" renamed")
            shown = secondModel
            compose.pumpUntil { secondModel.canEdit.value && secondModel.items.value.isNotEmpty() }
            compose.pumpFor(100)
            compose.onNodeWithText("Edit bookmark").assertDoesNotExist()
            compose.onNodeWithText("Second").assertExists()
            assertEquals(listOf("First"), firstModel.items.value.map { it.title })
            assertEquals(listOf("Second"), secondModel.items.value.map { it.title })
        } finally {
            o.destroy()
            other.destroy()
        }
    }
}
