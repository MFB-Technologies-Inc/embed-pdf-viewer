package com.embedpdf

import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.floor
import kotlin.math.sign

/**
 * How sharp a view should render a page: full resolution while any part of it
 * is on screen, a small placeholder otherwise.
 *
 * Lazy lists keep neighbour pages composed, and accessibility services reach
 * more of them. A page that renders at full resolution whenever its view
 * exists holds megabytes for pages nobody can see.
 *
 * The constructor makes an explicit demand without rounding or validating its
 * width.
 *
 * @property deviceWidth device pixels across the page's displayed width.
 * @property isFull whether this is the on-screen render rather than the placeholder.
 */
public data class PageRenderResolution(public val deviceWidth: Double, public val isFull: Boolean) {
    /**
     * [deviceWidth] measured across a page whose displayed width differs from
     * its unrotated width, for `RenderPageOptions(width = ...)`, which renders
     * the unrotated page. [displayedWidth] includes any rotation and is
     * clamped to at least one. Rounds half away from zero.
     */
    public fun unrotatedDeviceWidth(unrotatedWidth: Double, displayedWidth: Double): Double =
        roundHalfAwayFromZero(deviceWidth * unrotatedWidth / maxOf(displayedWidth, 1.0))

    public companion object {
        /** Device pixels for a page that is built but not on screen. */
        public const val OFF_SCREEN_DEVICE_WIDTH: Double = 256.0

        /** Full-resolution widths round up to this many pixels, so small layout changes don't each start a new render. */
        public const val DEVICE_WIDTH_STEP: Double = 128.0

        /**
         * The resolution for a page laid out [viewWidth] layout units wide.
         *
         * On-screen widths round upward to multiples of [DEVICE_WIDTH_STEP],
         * with a minimum of 64 pixels. Off-screen pages always use
         * [OFF_SCREEN_DEVICE_WIDTH] and ignore the dimensions.
         *
         * @param displayScale device pixels per layout unit (the display's density).
         * @param isOnScreen whether any portion of the page is visible.
         * @param zoomScale additional magnification; values below one are treated as one.
         */
        public fun forPage(
            viewWidth: Double,
            displayScale: Double,
            isOnScreen: Boolean,
            zoomScale: Double = 1.0,
        ): PageRenderResolution {
            if (!isOnScreen) return PageRenderResolution(OFF_SCREEN_DEVICE_WIDTH, isFull = false)
            val pixels = viewWidth * displayScale * maxOf(zoomScale, 1.0)
            val width = maxOf(DEVICE_WIDTH_STEP / 2, ceil(pixels / DEVICE_WIDTH_STEP) * DEVICE_WIDTH_STEP)
            return PageRenderResolution(width, isFull = true)
        }

        /**
         * [scale] raised so the page's width in device pixels rounds up to a
         * multiple of [DEVICE_WIDTH_STEP]. Scales whose widths fall in one step
         * share a value, so a render keyed on it restarts only when the width
         * crosses into another step. The result is never below [scale]; it is
         * [scale] itself when either value is not positive.
         */
        public fun bandedScale(scale: Double, pageWidth: Double): Double {
            if (scale <= 0 || pageWidth <= 0) return scale
            val pixels = ceil(scale * pageWidth / DEVICE_WIDTH_STEP) * DEVICE_WIDTH_STEP
            return maxOf(scale, pixels / pageWidth)
        }
    }
}

internal fun roundHalfAwayFromZero(value: Double): Double = sign(value) * floor(abs(value) + 0.5)
