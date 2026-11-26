package com.embedpdf.compose

import androidx.compose.foundation.layout.Spacer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import com.embedpdf.Rect
import com.embedpdf.Size
import com.embedpdf.models.TiledPageModel
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.roundToInt

/** Notifications for a view-driven raster update of a [TiledPageImage]. */
public enum class TiledPagePhase {
    /** The view is about to submit its current scale and visible region. */
    BEGAN,

    /**
     * The base is current and a plan that is not waiting has all its
     * requested pixels. May come for a base-only plan; it does not confirm
     * that anything is on screen.
     */
    PAINTED,

    /**
     * The update returned while its effect was not cancelled. It also comes
     * after a suppressed error or the model's polling limit, so it does not
     * prove that the tile plan settled.
     */
    SETTLED,
}

/**
 * Draws a page's budgeted base image and its detail tiles from [model].
 *
 * Give it the page's unrotated aspect ratio through [modifier] and bounded
 * dimensions; rotate the whole view if the page is shown turned. An update
 * starts when [deviceScale], [visibleRect] or [revision] changes. The view
 * attaches to the model as its painter and acknowledges each frame it draws
 * from the draw phase, so the model's pool never refills a bitmap a frame
 * may still show; it acknowledges each tile two frames after the tile first
 * appears, so the plan can drop the coarser tiles under it. When it leaves
 * the composition, or when [model] is replaced, it detaches and releases the
 * page's demand. The host keeps the model. The images are decorative: the
 * host gives the page its accessible label and text.
 *
 * @param model the retained model for one page and the surface's shared demand.
 * @param unrotatedSize the page's size in points, without its rotation.
 * @param deviceScale positive device pixels per page point at the current zoom.
 * @param visibleRect the visible region in unrotated page points; `null` is
 *   the whole page and a zero-area rectangle is a page built but off screen.
 * @param revision a host revision, initially zero. Change it after
 *   `RenderCapability.invalidate` to restart the update and check the base
 *   key, even when scale and visibility are unchanged.
 * @param onPhase an optional observer of the update, called on the main
 *   thread. A cancelled update sends no [TiledPagePhase.SETTLED], and
 *   [TiledPagePhase.PAINTED] is not guaranteed for every update.
 */
@Composable
public fun TiledPageImage(
    model: TiledPageModel,
    unrotatedSize: Size,
    deviceScale: Double,
    visibleRect: Rect?,
    modifier: Modifier = Modifier,
    revision: Int = 0,
    onPhase: ((TiledPagePhase) -> Unit)? = null,
) {
    // Replacing the model retires the old one's demand as well as starting new work.
    ModelScope(model) { page ->
        val base by page.base.collectAsState()
        val tiles by page.tiles.collectAsState()
        val phase by rememberUpdatedState(onPhase)
        val scope = rememberCoroutineScope()

        DisposableEffect(page) {
            page.painterAttached()
            onDispose { page.painterDetached() }
        }
        LaunchedEffect(page) {
            try {
                awaitCancellation()
            } finally {
                // The composition has let go, so nothing cancels the release.
                withContext(NonCancellable) { page.release() }
            }
        }
        LaunchedEffect(page, deviceScale, visibleRect, revision) {
            phase?.invoke(TiledPagePhase.BEGAN)
            page.update(deviceScale, unrotatedSize.width, visibleRect) { phase?.invoke(TiledPagePhase.PAINTED) }
            ensureActive()
            phase?.invoke(TiledPagePhase.SETTLED)
        }
        for (tile in tiles) {
            key(tile.id) {
                LaunchedEffect(Unit) {
                    // Two frames after it appears, as the web host waits, so the
                    // tile is on screen before the plan may drop what is under it.
                    withFrameNanos {}
                    withFrameNanos {}
                    // The model polls until the plan settles; that outlives this tile.
                    scope.launch { page.tileDisplayed(tile.id) }
                }
            }
        }

        val baseImage = remember(base) { base?.asImageBitmap() }
        // Stacked by z, as Swift's `zIndex` stacks them; equal z keeps paint order.
        val tileImages = remember(tiles) { tiles.sortedBy { it.z }.map { it to it.image.asImageBitmap() } }
        Spacer(
            modifier.drawBehind {
                val pixels = IntSize(size.width.roundToInt(), size.height.roundToInt())
                if (baseImage != null) {
                    drawImage(baseImage, srcSize = IntSize(baseImage.width, baseImage.height), dstSize = pixels)
                }
                val scale = size.width / maxOf(unrotatedSize.width, 1.0).toFloat()
                for ((tile, image) in tileImages) {
                    // Edges rounded, not lengths, so neighbouring tiles meet without a seam.
                    val left = (tile.rect.x * scale).roundToInt()
                    val top = (tile.rect.y * scale).roundToInt()
                    val right = ((tile.rect.x + tile.rect.width) * scale).roundToInt()
                    val bottom = ((tile.rect.y + tile.rect.height) * scale).roundToInt()
                    drawImage(
                        image,
                        srcSize = IntSize(image.width, image.height),
                        dstOffset = IntOffset(left, top),
                        dstSize = IntSize(right - left, bottom - top),
                    )
                }
                page.frameDrawn()
            },
        )
    }
}
