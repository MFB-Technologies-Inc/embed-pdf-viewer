package com.embedpdf

import android.os.Build
import android.os.Handler
import android.os.Looper
import android.view.HapticFeedbackConstants
import android.view.View
import java.lang.ref.WeakReference

/**
 * Adapts viewer feedback requests to [View.performHapticFeedback] on [view].
 * Pass it as `FeedbackConfig(HapticFeedback(view))`.
 *
 * Safe to call from the viewer's kernel thread: each method posts to the main
 * thread and returns before the view performs the feedback. Whether the
 * device vibrates depends on the device, its settings and
 * [View.isHapticFeedbackEnabled].
 *
 * The constants follow the iOS mapping as closely as Android allows:
 * selection is [HapticFeedbackConstants.CLOCK_TICK]; impact is
 * [HapticFeedbackConstants.KEYBOARD_TAP] when light,
 * [HapticFeedbackConstants.VIRTUAL_KEY] when medium or unweighted and
 * [HapticFeedbackConstants.LONG_PRESS] when heavy; a success notification is
 * [HapticFeedbackConstants.CONFIRM] and an error
 * [HapticFeedbackConstants.REJECT] from API 30, [HapticFeedbackConstants.VIRTUAL_KEY]
 * and [HapticFeedbackConstants.LONG_PRESS] below it, and a warning is
 * [HapticFeedbackConstants.LONG_PRESS].
 *
 * It holds [view] weakly, so a viewer that outlives its screen doesn't keep
 * the screen alive; feedback after the view is gone is dropped.
 */
public class HapticFeedback(view: View) : PlatformFeedback {
    private val view = WeakReference(view)
    private val main = Handler(Looper.getMainLooper())

    /** Posts a selection-change haptic to the main thread. */
    override fun selection() {
        perform(HapticFeedbackConstants.CLOCK_TICK)
    }

    /** Posts an impact haptic to the main thread; `null` [weight] is medium. */
    override fun impact(weight: FeedbackImpactWeight?) {
        perform(impactConstant(weight))
    }

    /** Posts a success, warning or error haptic to the main thread. */
    override fun notify(kind: FeedbackNotifyKind) {
        perform(notifyConstant(kind, Build.VERSION.SDK_INT))
    }

    private fun perform(constant: Int) {
        main.post { view.get()?.performHapticFeedback(constant) }
    }

    internal companion object {
        fun impactConstant(weight: FeedbackImpactWeight?): Int = when (weight) {
            FeedbackImpactWeight.LIGHT -> HapticFeedbackConstants.KEYBOARD_TAP
            FeedbackImpactWeight.HEAVY -> HapticFeedbackConstants.LONG_PRESS
            FeedbackImpactWeight.MEDIUM, null -> HapticFeedbackConstants.VIRTUAL_KEY
        }

        fun notifyConstant(kind: FeedbackNotifyKind, sdk: Int): Int = when (kind) {
            FeedbackNotifyKind.SUCCESS ->
                if (sdk >= Build.VERSION_CODES.R) HapticFeedbackConstants.CONFIRM else HapticFeedbackConstants.VIRTUAL_KEY
            FeedbackNotifyKind.WARNING -> HapticFeedbackConstants.LONG_PRESS
            FeedbackNotifyKind.ERROR ->
                if (sdk >= Build.VERSION_CODES.R) HapticFeedbackConstants.REJECT else HapticFeedbackConstants.LONG_PRESS
        }
    }
}
