package com.embedpdf.compose

import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertIsOff
import androidx.compose.ui.test.assertIsOn
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.Fixture
import com.embedpdf.ToolId
import com.embedpdf.models.PdfReaderModel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import com.embedpdf.PdfException
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class PdfReaderTest {
    @get:Rule
    val compose = createComposeRule()

    private var model: PdfReaderModel? = null

    /** Starts [block] on the main dispatcher, as an app's effect would, without waiting for it. */
    private fun launchMain(block: suspend () -> Unit): Job = CoroutineScope(Dispatchers.Main).launch { block() }

    private fun reader(model: PdfReaderModel, tools: List<ToolId> = emptyList()): PdfReaderModel {
        this.model = model
        compose.setContent { PdfReader(model, tools = tools) }
        return model
    }

    private fun ready(model: PdfReaderModel) {
        compose.pumpUntil(message = { "${model.phase.value}" }) { model.phase.value is PdfReaderModel.Phase.Ready }
        compose.pumpFor(200)
    }

    @After
    fun closeModel() {
        val model = model ?: return
        val closing = launchMain { model.close() }
        compose.pumpUntil { closing.isCompleted }
    }

    @Test
    fun aLockedDocumentAsksForItsPasswordUntilItOpens() {
        val model = reader(PdfReaderModel())
        launchMain { model.open(Fixture.bytes("encrypted-hello"), name = "secret.pdf") }
        compose.pumpUntil { model.phase.value is PdfReaderModel.Phase.Locked }
        compose.pumpFrame()
        compose.onNodeWithText("secret.pdf is password protected").assertExists()
        compose.onNodeWithTag("readerUnlock").assertIsNotEnabled()
        compose.onNodeWithTag("readerPassword").performTextInput("wrong")
        compose.onNodeWithTag("readerUnlock").assertIsEnabled().performClick()
        compose.pumpUntil { model.unlockError.value != null }
        compose.pumpFrame()
        compose.onNodeWithTag("readerUnlockError").assertExists()
        compose.onNodeWithContentDescription("Document password").assertExists()
        compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
        // The right password, through the model, as the button sends it.
        launchMain { model.unlock("hôtel") }
        ready(model)
        compose.onNodeWithContentDescription("Document pages").assertExists()
    }

    /** A typed password is not kept in saved state: a restored prompt starts empty. */
    @Test
    fun aRestoredPasswordPromptStartsEmpty() {
        val model = PdfReaderModel()
        this.model = model
        val restoration = StateRestorationTester(compose)
        restoration.setContent { PdfReader(model) }
        launchMain { model.open(Fixture.bytes("encrypted-hello"), name = "secret.pdf") }
        compose.pumpUntil { model.phase.value is PdfReaderModel.Phase.Locked }
        compose.pumpFrame()
        compose.onNodeWithTag("readerPassword").performTextInput("typed")
        compose.onNodeWithTag("readerUnlock").assertIsEnabled()
        restoration.emulateSavedInstanceStateRestore()
        compose.pumpFrame()
        compose.onNodeWithTag("readerUnlock").assertIsNotEnabled()
    }

    /** A failure shows a localized message and hands the engine's exception to the app. */
    @Test
    fun aFailureShowsALocalizedMessageAndReportsTheError() {
        val model = PdfReaderModel()
        this.model = model
        val reported = mutableListOf<PdfException>()
        compose.setContent { PdfReader(model, onError = { reported += it }) }
        launchMain { model.open("not a pdf".toByteArray()) }
        compose.pumpUntil { model.phase.value is PdfReaderModel.Phase.Failed }
        compose.pumpUntil { reported.isNotEmpty() }
        compose.onNodeWithText("The document could not be opened", substring = true).assertExists()
        val failure = (model.phase.value as PdfReaderModel.Phase.Failed).error
        assertEquals(listOf(failure), reported)
        compose.onNodeWithText(failure.detail).assertDoesNotExist()
    }

    /** Review focus: TalkBack reads a label on every control a full reader shows. */
    @Test
    fun everyControlOfAFullReaderHasALabel() {
        val model = reader(
            PdfReaderModel(configure = { it.outline().attachment().history() }),
            tools = listOf(ToolId.POINTER, ToolId.INK),
        )
        launchMain { model.open(Fixture.bytes("report"), name = "report.pdf") }
        ready(model)
        compose.pumpUntil { runCatching { compose.onNodeWithTag("historyUndo").assertExists() }.isSuccess }
        compose.pumpUntil { runCatching { compose.onNodeWithTag("toolPicker-ink").assertExists() }.isSuccess }
        compose.onNodeWithTag("searchField").assertExists()
        compose.onNodeWithContentDescription("Bookmarks").assertIsOff()
        compose.onNodeWithContentDescription("Attachments").assertExists()
        compose.onNodeWithContentDescription("Thumbnails").assertExists()
        compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
    }

    @Test
    fun aViewerWithoutThePanelsPluginsOffersOnlyThumbnails() {
        val model = reader(PdfReaderModel())
        launchMain { model.open(Fixture.bytes("report")) }
        ready(model)
        compose.onNodeWithContentDescription("Thumbnails").assertExists()
        compose.onNodeWithContentDescription("Bookmarks").assertDoesNotExist()
        compose.onNodeWithContentDescription("Attachments").assertDoesNotExist()
        compose.onNodeWithTag("historyUndo").assertDoesNotExist()
        // No tools asked for: no picker, as Swift's reader shows none.
        compose.onNodeWithTag("toolPicker-pointer").assertDoesNotExist()
    }

    /** On a phone the search field has a row of its own, wide enough to type in. */
    @Config(qualifiers = "w360dp-h740dp")
    @Test
    fun aPhoneWidthGivesTheSearchFieldARowOfItsOwn() {
        val model = reader(PdfReaderModel(configure = { it.outline().attachment() }))
        launchMain { model.open(Fixture.bytes("report")) }
        ready(model)
        compose.pumpUntil { runCatching { compose.onNodeWithContentDescription("Attachments").assertExists() }.isSuccess }
        val field = compose.onNodeWithTag("searchField").fetchSemanticsNode()
        val width = field.size.width / compose.density.density
        assertTrue("search field is $width dp wide", width >= 160f)
        // Under the panel toggles, not beside them.
        val toggle = compose.onNodeWithTag("readerPanel-thumbnails").fetchSemanticsNode()
        assertTrue(field.boundsInRoot.top >= toggle.boundsInRoot.bottom)
    }

    /** A compact window shows a panel as a bottom sheet. */
    @Test
    fun aCompactWindowShowsAPanelAsABottomSheet() {
        val model = reader(PdfReaderModel())
        launchMain { model.open(Fixture.bytes("report")) }
        ready(model)
        compose.onNodeWithTag("readerPanel-thumbnails").assertIsOff().performClick()
        compose.pumpFor(500)
        compose.onNodeWithTag("readerPanel-thumbnails").assertIsOn()
        compose.onNodeWithTag("readerBottomSheet").assertExists()
        compose.onNodeWithTag("readerSideSheet").assertDoesNotExist()
        compose.onNodeWithContentDescription("Page 1").assertExists()
        // Closing slides the sheet away before the panel goes.
        compose.onNodeWithTag("readerPanel-thumbnails").performClick()
        compose.pumpUntil { runCatching { compose.onNodeWithTag("readerBottomSheet").assertDoesNotExist() }.isSuccess }
        compose.onNodeWithTag("readerPanel-thumbnails").assertIsOff()
    }

    /** A window 600 dp wide or more shows a panel beside the pages. */
    @Config(qualifiers = "w840dp-h900dp")
    @Test
    fun aWideWindowShowsAPanelBesideThePages() {
        val model = reader(PdfReaderModel(configure = { it.outline() }))
        launchMain { model.open(Fixture.bytes("report")) }
        ready(model)
        compose.pumpUntil { runCatching { compose.onNodeWithContentDescription("Bookmarks").assertExists() }.isSuccess }
        compose.onNodeWithContentDescription("Bookmarks").performClick()
        compose.pumpFrame()
        compose.onNodeWithTag("readerSideSheet").assertExists()
        compose.onNodeWithTag("readerBottomSheet").assertDoesNotExist()
        compose.pumpUntil { runCatching { compose.onNodeWithText("No bookmarks").assertExists() }.isSuccess }
        compose.onNodeWithContentDescription("Close panel").performClick()
        compose.pumpFrame()
        compose.onNodeWithTag("readerSideSheet").assertDoesNotExist()
    }
}
