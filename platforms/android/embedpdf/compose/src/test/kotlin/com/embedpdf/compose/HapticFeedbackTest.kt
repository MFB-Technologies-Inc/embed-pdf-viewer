package com.embedpdf.compose

import androidx.compose.ui.test.junit4.createComposeRule
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.FeedbackConfig
import com.embedpdf.FeedbackImpactWeight
import com.embedpdf.FeedbackNotifyKind
import com.embedpdf.HapticFeedback
import com.embedpdf.ViewerConfig
import com.embedpdf.feedback
import com.embedpdf.started
import org.junit.Assert.assertSame
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

/** `HapticFeedbackTests`, with the provider the composition remembers. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class HapticFeedbackTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun acceptsEveryFeedbackRequestWithoutRequiringHapticHardware() {
        val provided = mutableListOf<HapticFeedback>()
        compose.setContent { provided += rememberHapticFeedback() }
        compose.waitForIdle()
        val provider = provided.first()
        engine {
            val viewer = started(ViewerConfig(feedback = FeedbackConfig(provider)))
            try {
                val feedback = viewer.feedback()
                feedback.selection()
                for (weight in listOf(null, FeedbackImpactWeight.LIGHT, FeedbackImpactWeight.MEDIUM, FeedbackImpactWeight.HEAVY)) {
                    feedback.impact(weight)
                }
                for (kind in FeedbackNotifyKind.entries) feedback.notify(kind)
            } finally {
                viewer.destroy()
            }
        }
        // The posted haptics run on the main thread.
        compose.pumpFrame()
        // Recomposition keeps the one provider for the view.
        assertSame(provider, provided.last())
    }
}
