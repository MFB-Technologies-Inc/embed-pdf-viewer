package com.embedpdf

import android.content.Context
import android.os.Looper
import android.view.HapticFeedbackConstants
import android.view.View
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import kotlin.concurrent.thread

/** Records each haptic it performs and whether it ran on the main thread. */
class RecordingView(context: Context) : View(context) {
    val performed = mutableListOf<Int>()
    var offMain = 0

    override fun performHapticFeedback(feedbackConstant: Int): Boolean {
        if (Looper.myLooper() != Looper.getMainLooper()) offMain++
        performed.add(feedbackConstant)
        return true
    }
}

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class HapticFeedbackTest {
    private val view = RecordingView(RuntimeEnvironment.getApplication())

    @Test
    fun callsFromAnotherThreadReachTheViewOnTheMainThread() {
        val feedback = HapticFeedback(view)
        thread {
            feedback.selection()
            feedback.impact(FeedbackImpactWeight.HEAVY)
            feedback.impact(null)
            feedback.notify(FeedbackNotifyKind.SUCCESS)
        }.join()
        // Posted, not performed, until the main thread runs.
        assertEquals(emptyList<Int>(), view.performed)
        shadowOf(Looper.getMainLooper()).idle()
        assertEquals(
            listOf(
                HapticFeedbackConstants.CLOCK_TICK,
                HapticFeedbackConstants.LONG_PRESS,
                HapticFeedbackConstants.VIRTUAL_KEY,
                HapticFeedbackConstants.CONFIRM,
            ),
            view.performed,
        )
        assertEquals(0, view.offMain)
    }

    @Test
    fun theViewersKernelThreadReachesTheViewOnTheMainThread() = viewerTest {
        val viewer = started(ViewerConfig(feedback = FeedbackConfig(HapticFeedback(view))))
        val feedback = viewer.feedback()
        feedback.notify(FeedbackNotifyKind.ERROR)
        feedback.impact(FeedbackImpactWeight.LIGHT)
        viewer.destroy()
        shadowOf(Looper.getMainLooper()).idle()
        assertEquals(listOf(HapticFeedbackConstants.REJECT, HapticFeedbackConstants.KEYBOARD_TAP), view.performed)
        assertEquals(0, view.offMain)
    }

    @Test
    fun belowApi30NotificationsUseTheOlderConstants() {
        assertEquals(HapticFeedbackConstants.VIRTUAL_KEY, HapticFeedback.notifyConstant(FeedbackNotifyKind.SUCCESS, 29))
        assertEquals(HapticFeedbackConstants.LONG_PRESS, HapticFeedback.notifyConstant(FeedbackNotifyKind.ERROR, 29))
        assertEquals(HapticFeedbackConstants.LONG_PRESS, HapticFeedback.notifyConstant(FeedbackNotifyKind.WARNING, 30))
    }
}
