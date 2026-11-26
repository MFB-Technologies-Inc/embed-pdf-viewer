package com.embedpdf.compose

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalView
import com.embedpdf.HapticFeedback

/**
 * The viewer feedback provider for the view this composition draws in: a
 * [HapticFeedback] over `LocalView`, for `FeedbackConfig(provider)` when the
 * viewer is made in composition. It holds the view weakly, so a viewer that
 * outlives the screen drops its feedback rather than keep the screen alive.
 * A viewer made outside composition, as `PdfViewerViewModel` makes it, takes
 * `HapticFeedback(view)` directly.
 */
@Composable
public fun rememberHapticFeedback(): HapticFeedback {
    val view = LocalView.current
    return remember(view) { HapticFeedback(view) }
}
