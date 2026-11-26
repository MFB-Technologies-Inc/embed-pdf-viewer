package com.embedpdf.compose

import android.app.Activity
import android.app.Application
import android.content.Intent
import android.content.pm.ActivityInfo
import android.net.Uri
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.test.core.app.ApplicationProvider
import com.embedpdf.AttachmentFileSource
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.Fixture
import com.embedpdf.attachments
import com.embedpdf.models.PdfReaderModel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.rules.ExternalResource
import org.junit.rules.RuleChain
import org.junit.rules.TemporaryFolder
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

/** Shows [model] in a reader that a recreation composes again, as an app's reader screen would. */
class ReaderHostActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val reader = checkNotNull(model)
        setContent { PdfReader(reader, Modifier.fillMaxSize()) }
    }

    companion object {
        var model: PdfReaderModel? = null
    }
}

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35], qualifiers = "w360dp-h740dp")
class ReaderRecreationTest {
    private val host = object : ExternalResource() {
        override fun before() {
            val application = ApplicationProvider.getApplicationContext<Application>()
            shadowOf(application.packageManager).addOrUpdateActivity(
                ActivityInfo().apply {
                    name = ReaderHostActivity::class.java.name
                    packageName = application.packageName
                },
            )
            ReaderHostActivity.model = PdfReaderModel(configure = { it.attachment() })
        }

        override fun after() {
            ReaderHostActivity.model = null
        }
    }

    val compose = createAndroidComposeRule<ReaderHostActivity>()

    @get:Rule
    val rules: RuleChain = RuleChain.outerRule(host).around(compose)

    @get:Rule
    val folder = TemporaryFolder()

    private val bytes = "Attachment file picker round trip".toByteArray()

    private fun onMain(block: suspend () -> Unit) {
        var done = false
        CoroutineScope(Dispatchers.Main).launch {
            block()
            done = true
        }
        compose.pumpUntil { done }
    }

    /**
     * A save picker opened in a narrow window answers after a rotation to a
     * wide one: the attachment panel moves from the bottom sheet to the side
     * sheet, and the copy still reaches the document the picker created.
     */
    @Test
    fun aSaveCopyAnsweredAfterARecreationIntoAWideWindowIsWritten() {
        val model = checkNotNull(ReaderHostActivity.model)
        try {
            onMain { model.open(Fixture.bytes("report"), name = "report.pdf") }
            val session = (model.phase.value as PdfReaderModel.Phase.Ready).session
            engine {
                val attachments = session.viewer.attachments(session.document)
                attachments.ensureLoaded()
                attachments.add(listOf(AttachmentFileSource(data = bytes, name = "note.txt", mimeType = "text/plain")))
            }
            compose.pumpUntil { runCatching { compose.onNodeWithTag("readerPanel-attachments").assertExists() }.isSuccess }
            compose.onNodeWithTag("readerPanel-attachments").performClick()
            compose.pumpUntil { runCatching { compose.onNode(hasText("note.txt", substring = true) and hasClickAction()).assertExists() }.isSuccess }
            compose.onNodeWithTag("readerBottomSheet").assertExists()
            compose.onNode(hasText("note.txt", substring = true) and hasClickAction()).performClick()
            compose.pumpUntil { runCatching { compose.onNodeWithTag("attachmentExport").assertIsEnabled() }.isSuccess }
            compose.onNodeWithTag("attachmentExport").performClick()
            compose.pumpUntil { shadowOf(compose.activity).peekNextStartedActivityForResult() != null }
            val request = shadowOf(compose.activity).nextStartedActivityForResult
            assertEquals(Intent.ACTION_CREATE_DOCUMENT, request.intent.action)

            RuntimeEnvironment.setQualifiers("w840dp-h900dp")
            compose.activityRule.scenario.recreate()
            compose.pumpFrame()
            val out = folder.newFile("copy.bin")
            compose.activity.activityResultRegistry.dispatchResult(request.requestCode, Activity.RESULT_OK, Intent().setData(Uri.fromFile(out)))
            compose.pumpUntil { out.length() == bytes.size.toLong() }
            assertArrayEquals(bytes, out.readBytes())
            compose.pumpUntil { runCatching { compose.onNodeWithTag("readerSideSheet").assertExists() }.isSuccess }
            // The waiting copy is gone once written.
            val copies = attachmentCopyDirectory(compose.activity)
            compose.pumpUntil { copies.listFiles().isNullOrEmpty() }
        } finally {
            onMain { model.close() }
        }
    }
}
