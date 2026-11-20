package com.embedpdf.models

import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.Point
import com.embedpdf.Rect
import com.embedpdf.SelectionCapability
import com.embedpdf.SelectionEvent
import com.embedpdf.SelectionEventCleared
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Turns a drag over a page into a text selection and mirrors the
 * selection's rects onto the main thread. Every call runs after the calls
 * made before it, drag points that arrive while an extension waits collapse
 * into the newest one, and a point only ever extends its own drag.
 *
 * Swift's model follows the selection from `init` with a task of its own. A
 * model here owns no scope, so [run] does both jobs: it follows selection
 * events into [rects] and sends the drag calls as they are made. When
 * [dragBegan] or [dragChanged] is called while [run] is not running, the
 * model starts one drain of its queue in `actionScope`, the caller's scope,
 * when the constructor was given one. Without one the call waits in the queue
 * until the next [dragEnded] or [clear], which sends everything queued before
 * it, and the first such call sets [error] to `invalid-state` to say that
 * [run] is not running.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself, and [dragBegan] and [dragChanged] are
 * made there.
 */
public class TextSelectionModel(
    private val selection: SelectionCapability,
    /**
     * Where a drag call made while [run] is not running starts the drain that
     * sends it; `null` leaves the call queued until the next suspend call.
     */
    actionScope: CoroutineScope? = null,
) {
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
    private val calls = SerialCalls(actionScope) { reportNotRunning() }

    /** Whether [error] has said once that a drag call waits for [run]. */
    private var reportedNotRunning = false

    /** A drag point no extension has applied yet, and the drag it belongs to. */
    private class Pending(val point: Point, val page: PageRef, val drag: Long)

    /** The newest drag point no extension has applied yet. */
    private var pending: Pending? = null

    /** Counts [dragBegan] calls, so each call knows which drag it belongs to. */
    private var drag = 0L

    /**
     * Whether an extension of the current drag waits in the queue; it applies
     * whatever [pending] holds when it runs, if that point is its drag's.
     */
    private var extensionQueued = false

    /**
     * Follows selection events into [rects] and sends the queued calls until
     * the document closes or the calling coroutine is cancelled.
     */
    public suspend fun run(): Unit = onMain {
        coroutineScope {
            val events = subscribe(selection.observeEvents())
            val draining = launch(start = CoroutineStart.UNDISPATCHED) { calls.drainContinuously() }
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
        // A point left by an earlier drag, and its queued extension, belong to that drag.
        val began = ++drag
        pending = null
        extensionQueued = false
        calls.post { applyBegin(point, page, start, began) }
    }

    /**
     * Moves the selection's focus to [point]. Points that arrive while an
     * extension waits replace each other, and only the newest reaches the
     * engine. A point belongs to the drag the latest [dragBegan] opened and
     * never extends an earlier drag whose calls are still queued.
     */
    public fun dragChanged(point: Point, page: PageRef) {
        val current = drag
        pending = Pending(point, page, current)
        if (extensionQueued) return
        extensionQueued = true
        calls.post {
            if (drag == current) extensionQueued = false
            // Before a begin succeeds, the point waits for it.
            if (_isSelecting.value) applyPending(current)
        }
    }

    /**
     * Ends the drag, keeping the selection. True when the drag began on text;
     * false when the point had no text, the begin failed, or no drag was
     * open. [rects] are final when this returns: every refresh queued before
     * the end, a selection event's included, has been applied in order.
     */
    public suspend fun dragEnded(): Boolean = onMain {
        val ending = drag
        calls.enqueue { applyEnd(ending) }.await()
    }

    /** Clears the selection, [rects] and [error]. */
    public suspend fun clear(): Unit = onMain { calls.enqueue { applyClear() }.await() }

    /** Sets [error], the first time only, to say that a drag call waits because [run] is not running. */
    private fun reportNotRunning() {
        if (reportedNotRunning) return
        reportedNotRunning = true
        _error.value = PdfException(
            PdfException.Code.INVALID_STATE,
            "TextSelectionModel.run() is not running and the model has no actionScope, " +
                "so drag calls wait until dragEnded() or clear()",
            "InvalidStateError",
        )
    }

    private suspend fun applyBegin(point: Point, page: PageRef, start: Start, began: Long) {
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
        if (pending?.drag == began) applyPending(began) else refreshRects()
    }

    /** Applies [pending] when it belongs to drag [owner]; a later drag's point waits for that drag. */
    private suspend fun applyPending(owner: Long) {
        val next = pending ?: return
        if (next.drag != owner) return
        pending = null
        val point = next.point
        val page = next.page
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

    private suspend fun applyEnd(ending: Long): Boolean {
        if (pending?.drag == ending) pending = null
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
