package com.embedpdf.compose

import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.onVisibilityChanged

/**
 * Reports whether any of this element is on screen, for choosing a page's
 * raster resolution: [onChange] receives `true` once at least 1% of it is
 * visible in the window and `false` when it no longer is. Pass the value to
 * [PageImageView]'s `isOnScreen`; choose its initial value in the host.
 *
 * Lazy lists compose a few neighbours beyond the viewport; they report
 * `false` until they scroll in.
 */
public fun Modifier.trackingScrollVisibility(onChange: (Boolean) -> Unit): Modifier =
    onVisibilityChanged(minFractionVisible = VISIBLE_FRACTION, callback = onChange)

private const val VISIBLE_FRACTION = 0.01f
