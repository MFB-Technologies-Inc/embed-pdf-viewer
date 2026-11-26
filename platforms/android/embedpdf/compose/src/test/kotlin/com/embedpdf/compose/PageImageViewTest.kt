package com.embedpdf.compose

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.test.hasProgressBarRangeInfo
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.RenderConfig
import com.embedpdf.ViewerConfig
import com.embedpdf.render
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class PageImageViewTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    private fun loading(): Boolean =
        compose.onAllNodes(hasProgressBarRangeInfo(ProgressBarRangeInfo.Indeterminate)).fetchSemanticsNodes().isNotEmpty()

    @Test
    fun aPageShowsItsPixelsOnceRendered() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            compose.setContent {
                Box(Modifier.size(153.dp, 198.dp).testTag("page")) {
                    PageImageView(render, o.pages[0].page, 0.5, Modifier.fillMaxSize())
                }
            }
            compose.pumpUntil { !loading() }
            assertTrue(compose.drawnImage("page").hasInk())
        } finally {
            o.destroy()
        }
    }

    @Test
    fun aFailedRenderSaysThePageIsUnavailable() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            val failures = mutableListOf<PdfException>()
            compose.setContent {
                PageImageView(render, PageRef(pageObjectNumber = UInt.MAX_VALUE), 0.5, Modifier.size(100.dp), onError = { failures += it })
            }
            compose.pumpUntil { !loading() }
            compose.onNodeWithText("Page unavailable").assertExists()
            // Review fix 8: the reader sees localized words; the engine's message goes to the host.
            compose.onNodeWithText("The page could not be drawn.").assertExists()
            val failure = failures.single()
            compose.onNodeWithText(failure.detail).assertDoesNotExist()
        } finally {
            o.destroy()
        }
    }
}
