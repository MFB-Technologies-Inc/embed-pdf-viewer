package com.embedpdf.compose

import android.net.Uri
import androidx.activity.compose.LocalActivityResultRegistryOwner
import androidx.activity.result.ActivityResultRegistry
import androidx.activity.result.ActivityResultRegistryOwner
import androidx.activity.result.contract.ActivityResultContract
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertAll
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.dp
import androidx.core.app.ActivityOptionsCompat
import com.embedpdf.AttachmentFileSource
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.ResourceStatus
import com.embedpdf.ViewerConfig
import com.embedpdf.attachments
import com.embedpdf.models.AttachmentModel
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class AttachmentPanelTest {
    @get:Rule
    val compose = createComposeRule()

    @get:Rule
    val folder = TemporaryFolder()

    /** Answers each picker at once with what [answer] returns for its contract and input, and records the inputs. */
    private class Pickers(val answer: (ActivityResultContract<*, *>, Any?) -> Any?) : ActivityResultRegistry() {
        val inputs = mutableListOf<Any?>()

        override fun <I, O> onLaunch(requestCode: Int, contract: ActivityResultContract<I, O>, input: I, options: ActivityOptionsCompat?) {
            inputs += input
            dispatchResult(requestCode, answer(contract, input))
        }
    }

    private val bytes = "Attachment file picker round trip".toByteArray()

    private fun opened(): Opened = opened(ViewerConfig(interaction = true, annotation = "", attachment = true))

    private fun show(o: Opened, pickers: Pickers, seeded: Boolean = false): AttachmentModel {
        if (seeded) {
            engine {
                val attachments = o.viewer.attachments(o.document)
                attachments.ensureLoaded()
                attachments.add(listOf(AttachmentFileSource(data = bytes, name = "note.txt", mimeType = "text/plain")))
            }
        }
        val model = AttachmentModel(o.viewer, engine { o.viewer.attachments(o.document) })
        val owner = object : ActivityResultRegistryOwner {
            override val activityResultRegistry: ActivityResultRegistry = pickers
        }
        compose.setContent {
            CompositionLocalProvider(LocalActivityResultRegistryOwner provides owner) {
                AttachmentPanel(model, Modifier.size(360.dp, 640.dp))
            }
        }
        compose.pumpUntil { model.status.value == ResourceStatus.READY && model.canAdd.value && (!seeded || model.files.value.isNotEmpty()) }
        compose.pumpFrame()
        return model
    }

    @Test
    fun importingAttachesTheFilesThePickerReturns() {
        val file = folder.newFile("hello.txt").apply { writeBytes(bytes) }
        val pickers = Pickers { contract, _ ->
            assertEquals(ActivityResultContracts.OpenMultipleDocuments::class, contract::class)
            listOf(Uri.fromFile(file))
        }
        val o = opened()
        try {
            val model = show(o, pickers)
            compose.onNodeWithText("No attachments").assertExists()
            compose.onNodeWithTag("attachmentImport").assertIsEnabled().performClick()
            compose.pumpUntil { model.files.value.any { it.name == "hello.txt" } }
            compose.pumpFrame()
            compose.onNodeWithText("hello.txt").assertExists()
            // TalkBack: every control has a label and a role.
            compose.onAllNodes(hasClickAction()).assertAll(hasLabel())
            compose.onAllNodes(hasClickAction()).assertAll(SemanticsMatcher.keyIsDefined(SemanticsProperties.Role))
        } finally {
            o.destroy()
        }
    }

    @Test
    fun savingACopyWritesTheSelectedFileWhereThePickerSays() {
        val out = folder.newFile("copy.bin")
        val pickers = Pickers { _, _ -> Uri.fromFile(out) }
        val o = opened()
        try {
            val model = show(o, pickers, seeded = true)
            compose.onNodeWithTag("attachmentExport").assertIsNotEnabled()
            compose.onNode(hasText("note.txt", substring = true) and hasClickAction()).performClick()
            compose.pumpUntil { model.selectedFiles.value.size == 1 && !model.performing.value }
            compose.pumpFrame()
            compose.onNode(hasText("note.txt", substring = true) and hasClickAction()).assertIsSelected()
            compose.onNodeWithTag("attachmentExport").assertIsEnabled().performClick()
            compose.pumpUntil { out.length() == bytes.size.toLong() }
            assertArrayEquals(bytes, out.readBytes())
            assertEquals(listOf<Any?>("note.txt"), pickers.inputs)
            assertEquals(null, model.error.value)
        } finally {
            o.destroy()
        }
    }

    /** `AttachmentExportDocument.contentType(for:)`: every copy is offered as plain data. */
    @Test
    fun aCopyIsOfferedAsDataWhateverItsType() {
        assertEquals("application/octet-stream", AttachmentExportDocument.mimeType("text/plain"))
        assertEquals(AttachmentExportDocument.MIME_TYPE, AttachmentExportDocument.mimeType(null))
    }

    @Test
    fun removingAsksFirst() {
        val o = opened()
        try {
            val model = show(o, Pickers { _, _ -> null }, seeded = true)
            compose.onNode(hasText("note.txt", substring = true) and hasClickAction()).performClick()
            compose.pumpUntil { model.canRemoveSelection.value && !model.performing.value }
            compose.pumpFrame()
            compose.onNodeWithTag("attachmentRemove").assertIsEnabled().performClick()
            compose.onNodeWithText("Remove attachment?").assertExists()
            compose.onNode(hasText("Remove") and hasClickAction()).performClick()
            compose.pumpUntil { model.files.value.isEmpty() }
            compose.pumpFrame()
            compose.onNodeWithText("No attachments").assertExists()
        } finally {
            o.destroy()
        }
    }
}
