package com.embedpdf.models

import android.graphics.Bitmap
import com.embedpdf.PageGeometry
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.RenderCapability
import com.embedpdf.geometry
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * An observable model of a document's page thumbnails, which a view reads,
 * each turned to the page as shown.
 *
 * [load] renders a page with `RenderCapability.renderThumbnail`, which takes
 * no size: the engine draws the whole page 512 pixels on its long side, so
 * neither the model nor a view picks the pixel size. The engine's thumbnail
 * is unrotated; the model turns it with [PageGeometry], using the rotation
 * [pages] reports. [pages] is a snapshot. After a page edit that rotates,
 * inserts or removes pages, make a new model from the new page list.
 *
 * The model keeps its own [RenderRevisions] over [render], which counts
 * invalidations while [run] runs. A thumbnail is cached with the page's
 * render revision at the time its render started. [image] keeps answering
 * with that image after the page is invalidated, so a cell shows the old
 * thumbnail until the next [load] replaces it.
 *
 * The cache holds the 128 pages used most recently, where a use is a [load]
 * call: up to 1 MiB of pixels a thumbnail, so about 128 MiB however long the
 * document is. A page dropped from the cache has no image until it is loaded
 * again.
 *
 * Calls for one page at one revision share a single render. While [run]
 * runs, the render runs in [run]'s coroutine, so it outlives the call that
 * started it; without [run] it runs in that call's coroutine, and cancelling
 * that call cancels it for every call waiting on it. A cancelled [load]
 * gives up its claim on the render and throws its cancellation at once; the
 * render is cancelled when no call waits on it any more.
 *
 * [error] is the latest failed load. It clears when the page it is for
 * loads; another page's thumbnail leaves it. A failed load leaves the page's
 * earlier image, if it has one. A cancelled load is not a failure.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself; the turn and the bitmap are made on
 * `Dispatchers.Default`.
 */
public class ThumbnailModel(
    private val render: RenderCapability,
    pages: List<PageInfo>,
) {
    private val _error = MutableStateFlow<PdfException?>(null)

    // Performance: one map holds every thumbnail, so each image that lands
    // notifies every collector of [images], not only that page's cell. A
    // flow per page limits a landing image to its own cell.
    private val _images = MutableStateFlow<Map<PageRef, Bitmap>>(emptyMap())

    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /** Every cached thumbnail, as shown. */
    public val images: StateFlow<Map<PageRef, Bitmap>> = _images.asStateFlow()

    private val geometries: Map<PageRef, PageGeometry> = pages.associate { it.page to it.geometry }
    private val revisions = RenderRevisions(render)
    private val cachedRevisions = HashMap<PageRef, Long>()

    /** Cached pages, least recently used first. */
    private val recency = ArrayList<PageRef>()
    private val loads = HashMap<PageRef, Load>()
    private var lastLoadId = 0

    /** The page [error] is for. */
    private var failedPage: PageRef? = null

    /** Where shared renders run while [run] runs. */
    private var runScope: CoroutineScope? = null

    /** How many thumbnails the cache keeps. Tests lower it. */
    internal var cacheLimit = 128

    /** How many renders have started. Tests read it. */
    internal var renderCount = 0

    /** Runs when a render has started, before it reaches the engine. Tests hold a render open with it. */
    internal var renderStarted: (suspend () -> Unit)? = null

    /** How many [load] calls wait on the page's render. Tests read it. */
    internal fun waiterCount(page: PageRef): Int = loads[page]?.waiters ?: 0

    /** One render in flight and the number of [load] calls waiting on it. */
    private class Load(val id: Int, val revision: Long, val job: Job, var waiters: Int)

    /** The page's cached thumbnail as shown, `null` until a load of it lands. */
    public fun image(page: PageRef): Bitmap? = _images.value[page]

    /** The page's render revision, which rises each time the engine invalidates the page. */
    public fun revision(page: PageRef): Long = revisions.revision(page)

    /**
     * Every invalidated page's render revision, as [revision] answers it; a
     * page never invalidated is absent. A view keys a cell's load on its
     * page's entry, so an invalidation loads that thumbnail again.
     */
    public val pageRevisions: StateFlow<Map<PageRef, Long>> get() = revisions.revisions

    /**
     * Counts invalidations and carries shared renders until the document
     * closes or the calling coroutine is cancelled.
     */
    public suspend fun run(): Unit = onMain {
        coroutineScope {
            runScope = this
            try {
                launch { revisions.run() }.join()
            } finally {
                runScope = null
            }
        }
    }

    /**
     * Renders the page's thumbnail unless the cache holds one for the page's
     * current render revision, and returns when that render ends: with the
     * image cached, with [error] set, or with neither when the render was
     * superseded. A page that is not in the model's pages sets [error] to
     * `not-found` without rendering.
     */
    public suspend fun load(page: PageRef): Unit = onMain {
        ensureActive()
        val geometry = geometries[page]
        if (geometry == null) {
            _error.value = PdfException(PdfException.Code.NOT_FOUND, "The page is not among the thumbnail model's pages.")
            failedPage = page
            return@onMain
        }
        val revision = revisions.revision(page)
        if (page in _images.value && cachedRevisions[page] == revision) {
            markUsed(page)
            return@onMain
        }
        if (loads[page]?.revision != revision) {
            loads[page]?.job?.cancel()
            val id = ++lastLoadId
            val owner = runScope ?: this
            val job = owner.launch { render(page, geometry, revision, id) }
            loads[page] = Load(id, revision, job, 0)
        }
        val load = loads.getValue(page)
        load.waiters += 1
        try {
            load.job.join()
        } catch (e: CancellationException) {
            stopWaiting(page, load.id)
            throw e
        }
    }

    /** Cancels the render once no call waits on it. A finished or superseded load is gone from `loads`. */
    private fun stopWaiting(page: PageRef, id: Int) {
        val load = loads[page] ?: return
        if (load.id != id) return
        load.waiters -= 1
        if (load.waiters <= 0) {
            load.job.cancel()
            loads.remove(page)
        }
    }

    private suspend fun render(page: PageRef, geometry: PageGeometry, revision: Long, id: Int) {
        renderCount += 1
        renderStarted?.invoke()
        try {
            val rendered = render.renderThumbnail(page)
            // A cancelled or superseded load no longer owns the page.
            if (loads[page]?.id != id) return
            val image = withContext(Dispatchers.Default) { geometry.toDisplay(rendered.image).toBitmap() }
            if (loads[page]?.id != id) return
            loads.remove(page)
            _images.value = _images.value + (page to image)
            cachedRevisions[page] = revision
            markUsed(page)
            if (failedPage == page) {
                _error.value = null
                failedPage = null
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            if (loads[page]?.id != id) return
            loads.remove(page)
            _error.value = controlError(e)
            failedPage = page
        }
    }

    /** Moves a cached page to the most recent end and drops the pages over the limit, oldest first. */
    private fun markUsed(page: PageRef) {
        if (page !in _images.value) return
        recency.remove(page)
        recency.add(page)
        if (recency.size > maxOf(cacheLimit, 1)) {
            val dropped = recency.subList(0, recency.size - maxOf(cacheLimit, 1))
            _images.value = _images.value - dropped.toSet()
            dropped.forEach { cachedRevisions.remove(it) }
            dropped.clear()
        }
    }
}
