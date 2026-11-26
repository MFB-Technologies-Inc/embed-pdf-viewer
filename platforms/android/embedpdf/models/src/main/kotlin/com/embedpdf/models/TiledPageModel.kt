package com.embedpdf.models

import android.graphics.Bitmap
import com.embedpdf.ContentInclusion
import com.embedpdf.PageRef
import com.embedpdf.Rect
import com.embedpdf.RenderCapability
import com.embedpdf.RenderPageViewDemand
import com.embedpdf.RenderTilePlan
import com.embedpdf.RenderViewDemand
import com.embedpdf.RenderedPage
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import kotlin.time.Duration
import kotlin.time.Duration.Companion.milliseconds
import kotlin.time.Duration.Companion.seconds
import kotlin.time.TimeMark
import kotlin.time.TimeSource

/**
 * Raster state for one page with a budgeted base and detail tiles.
 *
 * Retain one model per page, sharing a [RenderViewDemand] across pages in
 * the same reader surface. Demand, visibility, and tile rectangles all use
 * the page's unrotated point space, with x rightward and y downward. Rotate
 * the composed view as a whole when displaying a rotated page.
 *
 * [update] polls for tiles rather than subscribing to render events. The
 * host must request an update after pixel changes. Errors are suppressed; a
 * missing base or incomplete tiles do not provide a diagnostic error.
 *
 * With a [pool], tile and base bitmaps come from it, and a bitmap that left
 * [tiles] or [base] goes back to it only once no frame can still show it.
 * A painter says when: it calls [painterAttached] when it starts showing the
 * page, [frameDrawn] from its draw phase each time it draws the page, and
 * [painterDetached] when it stops. While exactly one painter is attached, a
 * bitmap goes back after the second [frameDrawn] that follows its leaving,
 * since the frame of the first was drawn without it and the frame that drew
 * it has finished by the second. Frames count per attachment: a bitmap that
 * left under an earlier attachment, or while none or several painters were
 * attached, waits on time alone. Time is the backstop either way: a bitmap
 * goes back once it left at least 250 milliseconds ago, so a painter that
 * stops drawing, or a View host that never calls any of these, strands
 * nothing. [update], [tileDisplayed] and [release] keep polling until every
 * bitmap they hold that way has gone back. A painter draws only from the
 * current [tiles] and [base], read on the main thread, and never keeps a
 * bitmap past the next value. Without a pool every bitmap is new and left to
 * the garbage collector.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself; bitmaps are filled on
 * `Dispatchers.Default`.
 */
public class TiledPageModel(
    private val render: RenderCapability,
    private val demand: RenderViewDemand,
    /** Stable page reference served by this model for its entire lifetime. */
    public val page: PageRef,
    /** Whether both raster planes include annotations; fixed at construction. */
    public val includeAnnotations: Boolean = true,
    private val pool: BitmapPool? = null,
) {
    /**
     * An owned tile image positioned in unrotated page points. Equality
     * compares identity, rectangle and stacking order, not pixels.
     */
    public class Tile(
        /** Opaque raster-source key used for display acknowledgments. */
        public val id: String,
        /** Placement rectangle in unrotated page points with a top-left origin. */
        public val rect: Rect,
        /** Stacking order; larger values draw above coarser tiles. */
        public val z: Double,
        public val image: Bitmap,
    ) {
        override fun equals(other: Any?): Boolean = other is Tile && other.id == id && other.rect == rect && other.z == z

        override fun hashCode(): Int = (id.hashCode() * 31 + rect.hashCode()) * 31 + z.hashCode()
    }

    private val _base = MutableStateFlow<Bitmap?>(null)
    private val _tiles = MutableStateFlow<List<Tile>>(emptyList())

    /** Budgeted whole-page raster, initially `null`; kept by [release]. */
    public val base: StateFlow<Bitmap?> = _base.asStateFlow()

    /** The current plan's available tile images in paint order; initially empty. */
    public val tiles: StateFlow<List<Tile>> = _tiles.asStateFlow()

    private var baseKey: String? = null
    private val images = HashMap<String, Bitmap>()
    private val painted = HashSet<String>()
    private var lastStamp: String? = null
    private val renderWork = PageRenderTask()
    private var demandGeneration = 0

    /** A bitmap that left [tiles] or [base]: the attachment and drawn frame it left in, and when. */
    private class Retired(val bitmap: Bitmap, val attachment: Long, val frame: Long, val left: TimeMark)

    /** Bitmaps that left the model and may still be on screen; empty without a pool. */
    private val retiring = ArrayList<Retired>()

    /** Painters attached now. */
    private var painters = 0

    /** Rises with each attach and detach, so frames never count across attachments. */
    private var attachment = 0L

    /** [frameDrawn] calls in the current attachment. */
    private var frames = 0L

    /** Runs after each reconcile. Tests read the pool there. */
    internal var reconciled: (() -> Unit)? = null

    private val annotations: ContentInclusion
        get() = if (includeAnnotations) ContentInclusion.ALWAYS else ContentInclusion.NEVER

    /**
     * Declares raster demand, refreshes the base, and polls for detail tiles.
     *
     * Polls every 32 milliseconds until the plan stays quiet for 250
     * milliseconds, the calling coroutine is cancelled, a plan request fails,
     * or five seconds elapse. Returns without throwing a capability failure;
     * returning does not guarantee that every requested pixel exists. Keeps
     * an earlier image if refreshing fails. A new update cancels and waits for
     * the previous work before declaring its demand.
     *
     * @param deviceScale positive device pixels per page point at the current zoom.
     * @param unrotatedWidth positive page width in points, without its rotation.
     * @param visibleRect the visible region in unrotated page points, y-down;
     *   `null` is the whole page and a zero-area rectangle is a page built but off screen.
     * @param onPainted called on the main thread at most once, after the base
     *   is current and every tile in a plan that is not waiting has pixels. It
     *   acknowledges available pixels, not display, and may never run on
     *   error, cancellation, or timeout.
     */
    public suspend fun update(
        deviceScale: Double,
        unrotatedWidth: Double,
        visibleRect: Rect?,
        onPainted: (() -> Unit)? = null,
    ): Unit = onMain {
        demandGeneration += 1
        renderWork.run { updateDemand(deviceScale, unrotatedWidth, visibleRect, onPainted) }
    }

    /**
     * Acknowledges that a tile has appeared so coarser covering tiles can
     * retire. Ignores unknown and already acknowledged keys and suppresses
     * capability errors. When no update owns the page it then polls until the
     * plan settles, in the calling coroutine: Swift starts that polling as a
     * task of its own and returns at once, so a caller here launches this
     * rather than waiting on it.
     */
    public suspend fun tileDisplayed(key: String): Unit = onMain {
        if (key in painted || key !in images) return@onMain
        val generation = demandGeneration
        painted += key
        try {
            demand.markPainted(page, key)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            // Suppressed, as every capability failure here is.
        }
        if (generation != demandGeneration || key !in images) return@onMain
        renderWork.runIfIdle {
            if (generation == demandGeneration) pollUntilSettled(null)
        }
    }

    /**
     * Says that a painter has started showing this page. Call it on the main
     * thread when the painter enters the screen, as a Compose painter does
     * from a `DisposableEffect`, and pair it with [painterDetached].
     */
    public fun painterAttached() {
        painters += 1
        newAttachment()
    }

    /** Says that a painter attached with [painterAttached] no longer shows this page. */
    public fun painterDetached() {
        if (painters > 0) painters -= 1
        newAttachment()
    }

    /**
     * Acknowledges a frame the attached painter drew, which shows only the
     * [tiles] and [base] it read while drawing. Call it on the main thread
     * from the draw phase, once for each time the page is drawn, never for a
     * frame that did not draw it. While exactly one painter is attached, a
     * bitmap that left the model goes back to the pool after the second call
     * that follows its leaving.
     */
    public fun frameDrawn() {
        frames += 1
        returnRetired()
    }

    private fun newAttachment() {
        attachment += 1
        frames = 0
        returnRetired()
    }

    /**
     * Releases this page's tile demand and clears local tile images. Cancels
     * and joins previous page work first, so an in-flight response cannot
     * repopulate the retired tiles. With a pool, returns once the retired
     * tiles have gone back to it. The base raster and its key remain;
     * discard the model to release them. Dispose the shared demand separately
     * after all pages stop using it.
     */
    public suspend fun release(): Unit = onMain {
        demandGeneration += 1
        renderWork.run {
            retire(images.values)
            _tiles.value = emptyList()
            images.clear()
            painted.clear()
            lastStamp = null
            try {
                demand.release(page)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                // Suppressed.
            }
            // Nothing polls after this, so the tiles just retired go back here.
            while (currentCoroutineContext().isActive && holdsRetired()) {
                delay(POLL_INTERVAL)
                returnRetired()
            }
        }
    }

    private suspend fun updateDemand(deviceScale: Double, unrotatedWidth: Double, visibleRect: Rect?, onPainted: (() -> Unit)?) {
        try {
            demand.setDemand(page, RenderPageViewDemand(desiredDeviceWidth = deviceScale * unrotatedWidth, visibleRect = visibleRect), annotations)
            if (!currentCoroutineContext().isActive) return
            val key = render.sourceKey(page, deviceScale, annotations)
            if (!currentCoroutineContext().isActive) return
            if (key != baseKey) {
                val rendered = render.renderSource(page, deviceScale, annotations)
                val bitmap = bitmap(rendered)
                if (!currentCoroutineContext().isActive) {
                    pool?.release(bitmap)
                    return
                }
                _base.value?.let(::retire)
                _base.value = bitmap
                baseKey = key
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            return
        }
        pollUntilSettled(onPainted)
    }

    private suspend fun pollUntilSettled(onPainted: (() -> Unit)?) {
        var painter = onPainted
        val clock = TimeSource.Monotonic
        val started = clock.markNow()
        var quietSince = clock.markNow()
        while (currentCoroutineContext().isActive && started.elapsedNow() < POLL_LIMIT) {
            val plan = try {
                demand.plan(page)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                return
            }
            val changed = plan.stamp != lastStamp
            lastStamp = plan.stamp
            reconcile(plan)
            if (!currentCoroutineContext().isActive) return
            val waiting = plan.fetching.isNotEmpty() || (plan.engaged && plan.paint.isEmpty())
            if (!waiting && plan.paint.all { it.key in images }) {
                painter?.invoke()
                painter = null
            }
            if (changed || waiting) {
                quietSince = clock.markNow()
            } else if (quietSince.elapsedNow() >= QUIET_PERIOD && !holdsRetired()) {
                return
            }
            delay(POLL_INTERVAL)
        }
    }

    private suspend fun reconcile(plan: RenderTilePlan) {
        returnRetired()
        for (source in plan.paint) {
            if (source.key in images) continue
            val tile = try {
                demand.tileImage(page, source.key)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                null
            } ?: continue
            val bitmap = bitmap(tile)
            if (!currentCoroutineContext().isActive) {
                pool?.release(bitmap)
                return
            }
            images[source.key] = bitmap
        }
        val wanted = plan.paint.mapTo(HashSet()) { it.key }
        for (key in images.keys - wanted) {
            images.remove(key)?.let(::retire)
            if (painted.remove(key)) {
                try {
                    demand.markUnpainted(page, key)
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    // Suppressed.
                }
            }
        }
        val next = plan.paint.mapNotNull { source -> images[source.key]?.let { Tile(source.key, source.rect, source.z, it) } }
        if (next != _tiles.value) _tiles.value = next
        reconciled?.invoke()
    }

    private fun retire(bitmaps: Collection<Bitmap>) {
        bitmaps.forEach(::retire)
    }

    /** Holds [bitmap], which just left [tiles] or [base], until no frame can show it. */
    private fun retire(bitmap: Bitmap) {
        if (pool == null) return
        retiring += Retired(bitmap, attachment, frames, TimeSource.Monotonic.markNow())
    }

    /** Gives the pool every held bitmap no frame can show any more. */
    private fun returnRetired() {
        val pool = pool ?: return
        retiring.removeAll { retired -> mayReturn(retired).also { if (it) pool.release(retired.bitmap) } }
    }

    private fun mayReturn(retired: Retired): Boolean {
        if (retired.left.elapsedNow() >= RETIRED_HOLD) return true
        // The first frame drawn after the bitmap left may have been composed
        // before it left; by the second, the last frame that drew it is done.
        return painters == 1 && retired.attachment == attachment && frames - retired.frame >= 2
    }

    /** Whether a bitmap still waits to go back to the pool. */
    private fun holdsRetired(): Boolean = retiring.isNotEmpty()

    private suspend fun bitmap(rendered: RenderedPage): Bitmap = withContext(Dispatchers.Default) {
        if (pool != null) rendered.image.toBitmap(pool) else rendered.image.toBitmap()
    }

    internal companion object {
        /** Between polls while tiles are in flight. The controller re-plans an arrival on its own about 16 ms later. */
        val POLL_INTERVAL: Duration = 32.milliseconds

        /**
         * How long the plan must stay unchanged, with nothing in flight, to
         * count as settled. Longer than the tile plane's default `settleMs` of
         * 150, which holds fetches back after a demand change.
         */
        val QUIET_PERIOD: Duration = 250.milliseconds

        /** The longest a bitmap that left the model waits for drawn frames before it goes back to the pool anyway. */
        val RETIRED_HOLD: Duration = 250.milliseconds

        /** The longest an update keeps polling, so a plan that never fills can't poll forever. */
        val POLL_LIMIT: Duration = 5.seconds
    }
}
