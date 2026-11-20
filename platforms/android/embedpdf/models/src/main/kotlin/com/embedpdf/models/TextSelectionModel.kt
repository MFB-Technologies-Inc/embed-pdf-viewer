package com.embedpdf.models

import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.Point
import com.embedpdf.Rect
import com.embedpdf.SelectionCapability
import com.embedpdf.SelectionEvent
import com.embedpdf.SelectionEventCleared
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Turns a drag over a page into a text selection and mirrors the
 * selection's rects onto the main thread. Every call runs after the calls
 * made before it, and drag points that arrive while an extension waits
 * collapse into the newest one.
 *
 * Swift's model follows the selection from `init` with a task of its own. A
 * model here owns no scope, so [run] does both jobs: it follows selection
 * events into [rects] and sends the drag calls as they are made. Without
 * [run], [dragBegan] and [dragChanged] wait in the queue until the next
 * [dragEnded] or [clear], which sends everything queued before it.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself, and [dragBegan] and [dragChanged] are
 * made there.
 */
public class TextSelectionModel(private val selection: SelectionCapability) {
    /** What a drag's first point selects. */
    public enum class Start {
        /** An empty selection at the point, grown by the drag. */
        CARET,

        /** The word under the point, grown by the drag. */
        WORD,
    }

    private val _rects = MutableStateFlow<Map<PageRef, List<Rect>>>(emptyMap())
    private val _isSelecting = MutableStateFlow(false)
    private val _error = MutableStateFlow<PdfException?>(null)

    /** Selected rects per page, in unrotated page points. */
    public val rects: StateFlow<Map<PageRef, List<Rect>>> = _rects.asStateFlow()

    /** Whether a drag has begun on text and not yet ended. */
    public val isSelecting: StateFlow<Boolean> = _isSelecting.asStateFlow()

    /** The last error the engine threw, kept until the next [dragBegan] or [clear] runs. */
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /** `extend` calls made so far. Tests read it. */
    internal var extensionCount = 0

    /** Runs after a refresh's reads return and before they are applied. Tests hold a refresh here. */
    internal var rectsReadReturned: (suspend () -> Unit)? = null

    /**
     * Orders the drag calls and every rects refresh, so an end never reaches
     * the engine before the begin it closes, and an older rects read never
     * overwrites a newer one.
     */
    private val calls = SerialCalls()

    /** The newest drag point no extension has applied yet. */
    private var pending: Pair<Point, PageRef>? = null

    /** Whether an extension waits in the queue; it applies whatever [pending] holds when it runs. */
    private var extensionQueued = false

    /**
     * Follows selection events into [rects] and sends the queued calls until
     * the document closes or the calling coroutine is cancelled.
     */
    public suspend fun run(): Unit = onMain {
        coroutineScope {
            val events = subscribe(selection.observeEvents())
            val draining = launch { calls.drainContinuously() }
            for (event in events) follow(event)
            draining.cancel()
        }
    }

    /**
     * Queues the event's effect on [rects] behind the calls made so far, so a
     * selection made elsewhere never lands out of order with a drag's own
     * refresh.
     */
    private fun follow(event: SelectionEvent) {
        if (event is SelectionEventCleared) {
            calls.enqueue { _rects.value = emptyMap() }
        } else {
            // Performance: every `changed` or `committed` event queues a full
            // rects refresh, and an extension that moves the selection emits
            // one, so a drag point pays two rounds of reads. A queued flag
            // collapses back-to-back event refreshes into one.
            calls.enqueue { refreshRects() }
        }
    }

    /**
     * Starts a selection at [point]. Returns at once: the begin runs after the
     * calls made before it, and [error] clears when it runs. [isSelecting]
     * turns true when the engine finds text at the point, and [dragEnded]
     * reports whether it did. With [Start.WORD], a failure after the word is
     * selected leaves it selected with [isSelecting] false and the error in
     * [error].
     */
    public fun dragBegan(point: Point, page: PageRef, start: Start = Start.CARET) {
        // A point left by a drag that never began belongs to that drag.
        pending = null
        calls.enqueue { applyBegin(point, page, start) }
    }

    /**
     * Moves the selection's focus to [point]. Points that arrive while an
     * extension waits replace each other, and only the newest reaches the
     * engine.
     */
    public fun dragChanged(point: Point, page: PageRef) {
        pending = point to page
        if (extensionQueued) return
        extensionQueued = true
        calls.enqueue {
            extensionQueued = false
            // Before a begin succeeds, the point waits for it.
            if (_isSelecting.value) applyPending()
        }
    }

    /**
     * Ends the drag, keeping the selection. True when the drag began on text;
     * false when the point had no text, the begin failed, or no drag was
     * open. [rects] are final when this returns: every refresh queued before
     * the end, a selection event's included, has been applied in order.
     */
    public suspend fun dragEnded(): Boolean = onMain { calls.enqueue { applyEnd() }.await() }

    /** Clears the selection, [rects] and [error]. */
    public suspend fun clear(): Unit = onMain { calls.enqueue { applyClear() }.await() }

    private suspend fun applyBegin(point: Point, page: PageRef, start: Start) {
        _error.value = null
        try {
            selection.ensureLoaded(page)
            val began = when (start) {
                Start.CARET -> selection.beginGesture(point, page)
                Start.WORD -> selection.selectWord(point, page).also { if (it) selection.beginGesture() }
            }
            if (!began) return
            _isSelecting.value = true
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e
            return
        }
        // A point that arrived before or during the begin applies inside this
        // call, so an end queued behind the begin finds it applied.
        if (pending != null) applyPending() else refreshRects()
    }

    private suspend fun applyPending() {
        val (point, page) = pending ?: return
        pending = null
        try {
            // Performance: one `isLoaded` round trip before every extension,
            // though a drag stays on one page for most of its points.
            if (!selection.isLoaded(page)) selection.ensureLoaded(page)
            extensionCount += 1
            selection.extend(point, page)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e
            return
        }
        refreshRects()
    }

    private suspend fun applyEnd(): Boolean {
        pending = null
        if (!_isSelecting.value) return false
        try {
            selection.endGesture()
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e
        }
        _isSelecting.value = false
        return true
    }

    private suspend fun applyClear() {
        _error.value = null
        try {
            selection.clear()
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e
        }
        _rects.value = emptyMap()
    }

    private suspend fun refreshRects() {
        val next = LinkedHashMap<PageRef, List<Rect>>()
        try {
            // Performance: one round trip for the page list and one more per
            // selected page, in sequence, on every refresh.
            for (page in selection.selectedPages()) next[page] = selection.rects(page)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e
            return
        }
        rectsReadReturned?.invoke()
        _rects.value = next
    }
}
