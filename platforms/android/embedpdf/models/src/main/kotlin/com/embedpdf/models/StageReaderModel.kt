package com.embedpdf.models

import com.embedpdf.Camera
import com.embedpdf.InteractionCapability
import com.embedpdf.InteractionEventToolChanged
import com.embedpdf.LinkActivation
import com.embedpdf.LinkCapability
import com.embedpdf.LinkEventActivated
import com.embedpdf.PageRef
import com.embedpdf.PageRenderResolution
import com.embedpdf.PdfException
import com.embedpdf.Point
import com.embedpdf.PointerSample
import com.embedpdf.Registration
import com.embedpdf.RenderCapability
import com.embedpdf.Size
import com.embedpdf.StageCapability
import com.embedpdf.StageEvent
import com.embedpdf.StageEventCameraChanged
import com.embedpdf.StageEventPageChanged
import com.embedpdf.StageVisiblePage
import com.embedpdf.ToolId
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.selects.select
import kotlin.math.hypot
import kotlin.math.min
import kotlin.math.sqrt

/**
 * What a scrolling reader shows, and the gestures it forwards. The stage
 * lays out; this model only mirrors its visible pages and camera onto the
 * main thread and turns gesture values into stage calls. Points and sizes
 * are in the reader's viewport, in the units the stage's viewport size uses.
 *
 * Given an [InteractionCapability], it also sends touches to the document's
 * pointer hub ([pointerDown] and its siblings, [tapped]), so the active tool
 * draws and a tap reaches whatever handles taps. Given a [LinkCapability]
 * too, a tap on a link activates it and the outcome arrives on
 * [linkActivations]; the model loads the links of each page as it comes into
 * view, so the app calls no `ensureLoaded`. Without an [InteractionCapability]
 * the pointer calls do nothing.
 *
 * The pointer calls, [isPanEnabled]'s drag end and the link loads return at
 * once and reach the engine as [run] drains them, in the order they were
 * made: run [run] for the reader's lifetime. The state lives on the main
 * thread: every call switches to `Dispatchers.Main.immediate` itself, and
 * the calls that return at once are made there.
 */
public class StageReaderModel(
    public val stage: StageCapability,
    public val render: RenderCapability,
    private val interaction: InteractionCapability? = null,
    private val link: LinkCapability? = null,
) {
    private val _visiblePages = MutableStateFlow<List<StageVisiblePage>>(emptyList())
    private val _camera = MutableStateFlow<Camera?>(null)
    private val _pageIndex = MutableStateFlow(0)
    private val _error = MutableStateFlow<PdfException?>(null)
    private val _isZooming = MutableStateFlow(false)
    private val _activeToolId = MutableStateFlow<ToolId?>(null)
    private val _forwardsDrags = MutableStateFlow(false)
    private val _isPanEnabled = MutableStateFlow(true)

    public val visiblePages: StateFlow<List<StageVisiblePage>> = _visiblePages.asStateFlow()
    public val camera: StateFlow<Camera?> = _camera.asStateFlow()

    /** The stage's current page as a zero-based display position. */
    public val pageIndex: StateFlow<Int> = _pageIndex.asStateFlow()

    /**
     * The latest failure of one of the model's calls, `null` when the latest
     * call of that kind succeeded. The kinds are: a read of the stage's pages
     * and camera, [sizeChanged], a drag call, [doubleTapped], the tool read,
     * the link tap registration in [run], a page's link load, and a pointer
     * sample's page lookup. A failure of any kind replaces the error. Only a
     * later success of the kind that set it clears it, so a good read of the
     * pages does not hide a failed link load, and the next good link load
     * does. The cost: a caller that polls this can miss a failure a later
     * success cleared. Collect it to see each one.
     */
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /**
     * Whether the zoom is changing: a pinch is under way, or the zoom
     * animation a pinch's end or a double tap starts hasn't settled. Pages
     * render at banded scales while it is ([renderScale]). A pan or a fling
     * moves the camera without changing the scale, so it leaves this false.
     */
    public val isZooming: StateFlow<Boolean> = _isZooming.asStateFlow()

    /**
     * The hub's active tool, read when [run] starts and followed from the
     * hub's `toolChanged` events while it runs. `null` before the first read,
     * after a failed one, and always without an [InteractionCapability].
     */
    public val activeToolId: StateFlow<ToolId?> = _activeToolId.asStateFlow()

    /**
     * Whether a drag belongs to the active tool rather than to the pan: true
     * when the model has an [InteractionCapability] and [activeToolId] is not
     * the hub's default tool. A reader view reads it when a drag starts: true
     * sends the drag to [pointerDown], [pointerMoved] and [pointerUp] instead
     * of [dragChanged]. [isPanEnabled] does not gate it.
     */
    public val forwardsDrags: StateFlow<Boolean> = _forwardsDrags.asStateFlow()

    /**
     * Whether a drag pans the stage; [isPanEnabledFlow] follows it. Turn it
     * off while a page overlay needs every touch. Turning it off mid-drag ends
     * the open stage gesture without a fling. It gates the pan only: while
     * [forwardsDrags] is true a drag still goes to the active tool, and a tap
     * still reaches [tapped]. Set it on the main thread.
     */
    public var isPanEnabled: Boolean
        get() = _isPanEnabled.value
        set(value) {
            val was = _isPanEnabled.value
            _isPanEnabled.value = value
            // Joins the drag queue before returning, so a drag call made after
            // this setter runs after the end.
            if (was && !value) dragCalls.enqueue { applyDragEnd(0.0, 0.0) }
        }

    /** [isPanEnabled] as a flow. */
    public val isPanEnabledFlow: StateFlow<Boolean> = _isPanEnabled.asStateFlow()

    /**
     * Whether a pan begins only once the finger has moved more than
     * [PAN_MOVEMENT_THRESHOLD] from where it landed. Until then a drag's
     * changes open no stage gesture, and a drag that ends before then flings
     * nothing. False by default, so a touch opens the gesture where it lands
     * and stops a fling under it. Turn it on when something above the reader
     * delays its drag until the touch moves or lifts, as a long-press
     * gesture does. The pan begins from the point where the drag crosses the
     * threshold, so the page does not jump. The cost: the first stretch of a
     * drag does not scroll, and a still touch or a tap does not stop a fling.
     */
    public var panBeginsOnMovement: Boolean = false

    /** Per-page render revisions, counted while [run] runs. */
    public val revisions: RenderRevisions = RenderRevisions(render)

    private val _linkActivations = MutableSharedFlow<LinkActivation>(
        extraBufferCapacity = 16,
        onBufferOverflow = BufferOverflow.DROP_OLDEST,
    )

    /**
     * Link activations the document reports while [run] runs: the outcome of
     * a tap that lands on a link, and also of an activation the app makes
     * itself through [LinkCapability]. A `Uri` is the app's to open. Never
     * emits without a [LinkCapability]. Every collector gets every
     * activation from when it started collecting; none gets one from before.
     */
    public val linkActivations: SharedFlow<LinkActivation> = _linkActivations.asSharedFlow()

    /** The kinds of call whose failure lands in [error]. */
    internal enum class Call { READ, RESIZE, DRAG, DOUBLE_TAP, TOOL_READ, LINK_TAP_REGISTRATION, LINK_LOAD, PAGE_LOOKUP }

    /** The kind of call that set [error], `null` while it is `null`. */
    private var failedCall: Call? = null
    private var lastTranslationX = 0.0
    private var lastTranslationY = 0.0
    private var lastMagnification = 1.0
    internal var gestureOpen = false
        private set

    /** Where a zoom stands. An open pinch outranks a settling animation. */
    private enum class ZoomPhase { IDLE, PINCHING, SETTLING }

    private var zoomPhase = ZoomPhase.IDLE

    /** Bumped when a zoom start begins and when its stage call returns (see Swift's `zoomEpoch`). */
    private var zoomEpoch = 0

    /** Zoom starts whose stage call has not returned. */
    private var zoomStartsInFlight = 0

    /** Runs in a double tap after the start is marked and before the stage call. Tests hold it. */
    internal var zoomStartReached: (suspend () -> Unit)? = null

    /** Runs in a refresh after its `isMoving()` read returns and before it is applied. Tests hold it. */
    internal var zoomReadReturned: (suspend () -> Unit)? = null

    /** The drag calls, each run after every earlier one. */
    private val dragCalls = SerialCalls()

    /** Sends the pointer calls to the hub; `null` without an [InteractionCapability]. */
    private val pointer: StagePointerForwarder? = interaction?.let { StagePointerForwarder(stage, it) }

    /** The link loads, one set of newly visible pages after another, off the stage event loop. */
    private val linkLoads = SerialCalls()

    /** The pages whose links have loaded or are loading. A failed page leaves it. */
    private val linkPages = HashSet<PageRef>()

    /** Runs as each pointer sample is handed to the hub. Tests record them. */
    internal var pointerSampleDispatched: ((PointerSample) -> Unit)?
        get() = pointer?.dispatched
        set(value) {
            pointer?.dispatched = value
        }

    init {
        pointer?.lookedUp = { error -> if (error != null) failed(Call.PAGE_LOOKUP, error) else succeeded(Call.PAGE_LOOKUP) }
    }

    /**
     * Run for the view's lifetime: projects stage events, counts render
     * revisions, and sends the queued pointer, drag and link calls. With an
     * [InteractionCapability] it also follows the active tool, and with a
     * [LinkCapability] as well it registers the link tap handler, once, and
     * keeps the registration until it returns. Returns when the document
     * closes.
     */
    public suspend fun run(): Unit = onMain {
        coroutineScope {
            // Subscribed before the first read and before the handler
            // registers, so no tool change and no activation falls between.
            val stageEvents = subscribe(stage.observeEvents())
            val toolEvents = interaction?.let { subscribe(it.observeEvents()) }
            val linkEvents = link?.let { subscribe(it.observeEvents()) }
            val linkTaps = registerLinkTaps()
            try {
                val background = listOfNotNull(
                    launch { revisions.run() },
                    launch { dragCalls.drainContinuously() },
                    launch { linkLoads.drainContinuously() },
                    pointer?.let { launch { it.calls.drainContinuously() } },
                    linkEvents?.let { events ->
                        launch {
                            for (event in events) {
                                if (event is LinkEventActivated) _linkActivations.emit(event.event.activation)
                            }
                        }
                    },
                )
                val loops = listOfNotNull(
                    launch { for (event in stageEvents) receiveStage(event) },
                    toolEvents?.let { events ->
                        launch {
                            refreshTool()
                            for (event in events) if (event is InteractionEventToolChanged) refreshTool()
                        }
                    },
                )
                refresh()
                select { loops.forEach { loop -> loop.onJoin {} } }
                (loops + background).forEach(Job::cancel)
                coroutineContext[Job]?.children?.forEach(Job::cancel)
            } finally {
                linkTaps?.unregister()
            }
        }
    }

    /** `null` without a [LinkCapability], or when registering fails; the failure is in [error]. */
    private suspend fun registerLinkTaps(): Registration? {
        val link = link ?: return null
        return try {
            link.registerTapHandler().also { succeeded(Call.LINK_TAP_REGISTRATION) }
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            failed(Call.LINK_TAP_REGISTRATION, e)
            null
        }
    }

    /** Reads the active and the default tool. A failed read leaves no tool and no forwarding. */
    private suspend fun refreshTool() {
        val interaction = interaction ?: return
        try {
            val active = interaction.activeToolId()
            val fallback = interaction.defaultToolId()
            _activeToolId.value = active
            _forwardsDrags.value = active != fallback
            succeeded(Call.TOOL_READ)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _activeToolId.value = null
            _forwardsDrags.value = false
            failed(Call.TOOL_READ, e)
        }
    }

    /**
     * Sends a pointer down at ([x], [y]) in the reader's viewport to the hub,
     * with the page the stage finds under it. Returns at once; the pointer
     * calls reach the hub in the order they are made.
     */
    public fun pointerDown(x: Float, y: Float) {
        pointer?.down(Point(x.toDouble(), y.toDouble()))
    }

    /**
     * Sends a pointer move. Moves that arrive while an earlier pointer call is
     * still on its way collapse into the newest, so the hub sees fewer samples
     * than a fast drag makes and always the latest position. A collapsed move
     * reaches the hub before the up or cancel that follows it and never joins
     * another gesture.
     */
    public fun pointerMoved(x: Float, y: Float) {
        pointer?.move(Point(x.toDouble(), y.toDouble()))
    }

    /** Sends a pointer up, after any move still waiting. */
    public fun pointerUp(x: Float, y: Float) {
        pointer?.up(Point(x.toDouble(), y.toDouble()))
    }

    /**
     * Sends a pointer cancel at the last location, so the tool drops the
     * gesture instead of committing it. Call it when the system takes a drag
     * away without ending it.
     */
    public fun pointerCancelled() {
        pointer?.cancel()
    }

    /**
     * Sends a down and an up at ([x], [y]), each with a click count of 1: a
     * tap for the active tool, and for a link under the default tool. A
     * pointer gesture still open is cancelled first.
     */
    public fun tapped(x: Float, y: Float) {
        pointer?.tap(Point(x.toDouble(), y.toDouble()))
    }

    /** Returns once every pointer call made so far has been handed to the hub. */
    internal suspend fun pointerCallsFinished() {
        onMain { pointer?.finished() }
    }

    /** Returns once every link load started so far has finished. */
    internal suspend fun linkLoadsFinished() {
        onMain { linkLoads.enqueue {}.await() }
    }

    /** A presentation revision that changes when the engine invalidates this page. */
    public fun renderRevision(page: PageRef): Long = revisions.revision(page)

    private suspend fun receiveStage(event: StageEvent) {
        when (event) {
            is StageEventCameraChanged -> {
                // The event carries the camera; only the pages it shows are read back.
                _camera.value = event.event.camera
                // Performance: one `visiblePages` round trip per camera event,
                // and a pan, a pinch or a fling emits one per step.
                try {
                    show(stage.visiblePages())
                    succeeded(Call.READ)
                } catch (e: CancellationException) {
                    throw e
                } catch (e: PdfException) {
                    failed(Call.READ, e)
                }
            }
            is StageEventPageChanged -> try {
                _pageIndex.value = event.event.pageIndex.toIntResult("page index")
            } catch (e: PdfException) {
                failed(Call.READ, e)
            }
            else -> refresh()
        }
    }

    /** Applies the viewport size in points and the display's pixels per point. */
    public suspend fun sizeChanged(width: Float, height: Float, displayScale: Float): Unit = onMain {
        try {
            stage.setDevicePixelRatio(displayScale.toDouble())
            stage.setViewportSize(Size(width.toDouble(), height.toDouble()))
            _pageIndex.value = stage.currentPageIndex()
            succeeded(Call.RESIZE)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            failed(Call.RESIZE, e)
        }
        refresh()
    }

    /**
     * The drag's translation from where it began. Runs after every earlier
     * drag call has finished. Does nothing to the stage while [isPanEnabled]
     * is false.
     */
    public suspend fun dragChanged(translationX: Float, translationY: Float): Unit = onMain {
        dragCalls.enqueue { applyDragChange(translationX.toDouble(), translationY.toDouble()) }.await()
    }

    /**
     * Ends the drag with its velocity in viewport units per second. Runs
     * after every earlier drag call has finished. Skips the fling while
     * [isPanEnabled] is false, and for a drag that never began a pan under
     * [panBeginsOnMovement].
     */
    public suspend fun dragEnded(velocityX: Float, velocityY: Float): Unit = onMain {
        dragCalls.enqueue { applyDragEnd(velocityX.toDouble(), velocityY.toDouble()) }.await()
    }

    /**
     * Ends a pan the gesture system took away without ending it: closes the
     * open stage gesture with no fling. Runs after every earlier drag call.
     */
    public suspend fun dragCancelled(): Unit = onMain {
        dragCalls.enqueue { applyDragEnd(0.0, 0.0) }.await()
    }

    private suspend fun applyDragChange(translationX: Double, translationY: Double) {
        if (!isPanEnabled) return
        var originX = 0.0
        var originY = 0.0
        if (panBeginsOnMovement && !gestureOpen) {
            // A touch that has not moved past the threshold may be a tap.
            val distance = hypot(translationX, translationY)
            if (distance <= PAN_MOVEMENT_THRESHOLD) return
            // The pan starts where the drag crossed the threshold.
            val crossing = PAN_MOVEMENT_THRESHOLD / distance
            originX = translationX * crossing
            originY = translationY * crossing
        }
        try {
            if (!gestureOpen) {
                gestureOpen = true
                lastTranslationX = originX
                lastTranslationY = originY
                stage.beginGesture(elastic = true)
            }
            // `panBy` follows the finger: a finger moving up scrolls down.
            stage.panBy(translationX - lastTranslationX, translationY - lastTranslationY)
            lastTranslationX = translationX
            lastTranslationY = translationY
            succeeded(Call.DRAG)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            failed(Call.DRAG, e)
        }
        // The pan's `cameraChanged` event brings the camera and pages back.
    }

    private suspend fun applyDragEnd(velocityX: Double, velocityY: Double) {
        // A drag that never passed the threshold panned nothing, so it flings nothing.
        val panned = gestureOpen || !panBeginsOnMovement
        try {
            if (gestureOpen) {
                gestureOpen = false
                stage.endGesture()
            }
            if ((velocityX != 0.0 || velocityY != 0.0) && isPanEnabled && panned) {
                stage.fling(velocityX, velocityY)
            }
            _pageIndex.value = stage.currentPageIndex()
            succeeded(Call.DRAG)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            failed(Call.DRAG, e)
        }
        refresh()
    }

    /**
     * A pinch's step: [magnification] since the pinch began, around ([x],
     * [y]). A pinch's first finger may already have opened a pointer gesture;
     * the pinch cancels it at the hub, and the pointer calls for the rest of
     * that touch send nothing. Call it on the main thread.
     */
    public fun magnifyChanged(magnification: Float, x: Float, y: Float) {
        pointer?.interrupt()
        zoomPhase = ZoomPhase.PINCHING
        setZooming(true)
        val factor = magnification / lastMagnification
        lastMagnification = magnification.toDouble()
        stage.zoomAround(Point(x.toDouble(), y.toDouble()), factor)
    }

    public suspend fun magnifyEnded(): Unit = onMain {
        zoomPhase = ZoomPhase.SETTLING
        zoomEpoch += 1
        lastMagnification = 1.0
        refresh()
    }

    public suspend fun doubleTapped(x: Float, y: Float): Unit = onMain {
        if (zoomPhase != ZoomPhase.PINCHING) zoomPhase = ZoomPhase.SETTLING
        zoomEpoch += 1
        zoomStartsInFlight += 1
        try {
            zoomStartReached?.invoke()
            stage.doubleTapZoom(Point(x.toDouble(), y.toDouble()))
            succeeded(Call.DOUBLE_TAP)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            failed(Call.DOUBLE_TAP, e)
        } finally {
            zoomStartsInFlight -= 1
            zoomEpoch += 1
        }
        refresh()
    }

    /**
     * Reads the pages, the camera and, after a pinch's end or a double tap,
     * whether the zoom animation still runs. A failed read ends the animation,
     * so [isZooming] can't stay set. Only a refresh that began after the last
     * zoom start returned may end the animation.
     */
    private suspend fun refresh() {
        val epoch = zoomEpoch
        val mayJudge = zoomStartsInFlight == 0
        try {
            // Performance: two round trips in sequence, three while a zoom
            // settles. One stage read that returns the pages, the camera and
            // whether the stage moves makes it one.
            show(stage.visiblePages())
            _camera.value = stage.camera()
            if (zoomPhase == ZoomPhase.SETTLING && mayJudge) {
                val moving = stage.isMoving()
                zoomReadReturned?.invoke()
                // A pinch may have started while the read was out.
                if (epoch == zoomEpoch && zoomPhase == ZoomPhase.SETTLING && !moving) zoomPhase = ZoomPhase.IDLE
            }
            succeeded(Call.READ)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            if (mayJudge && epoch == zoomEpoch && zoomPhase == ZoomPhase.SETTLING) zoomPhase = ZoomPhase.IDLE
            failed(Call.READ, e)
        }
        setZooming(zoomPhase != ZoomPhase.IDLE)
    }

    /** Takes the stage's visible pages and, with a [LinkCapability], queues the links of new ones. */
    private fun show(pages: List<StageVisiblePage>) {
        val link = link
        if (link == null) {
            _visiblePages.value = pages
            return
        }
        val shown = _visiblePages.value.mapTo(HashSet()) { it.page }
        _visiblePages.value = pages
        val fresh = pages.map { it.page }.filter { it !in shown && linkPages.add(it) }
        if (fresh.isEmpty()) return
        linkLoads.enqueue { loadLinks(fresh, link) }
    }

    /** A failed load is in [error], and its page loads again the next time it comes into view. */
    private suspend fun loadLinks(pages: List<PageRef>, link: LinkCapability) {
        for (page in pages) {
            // Performance: one bridge call per page the first time it comes
            // into view. A link call that takes a list of pages removes the
            // per-page hop.
            try {
                link.ensureLoaded(page)
                succeeded(Call.LINK_LOAD)
            } catch (e: CancellationException) {
                throw e
            } catch (e: PdfException) {
                linkPages.remove(page)
                failed(Call.LINK_LOAD, e)
            }
        }
    }

    /** Records a failed call of one kind in [error]. */
    internal fun failed(call: Call, error: PdfException) {
        _error.value = error
        failedCall = call
    }

    /** Clears [error] when the call that set it was of this kind. */
    private fun succeeded(call: Call) {
        if (failedCall != call) return
        failedCall = null
        _error.value = null
    }

    private fun setZooming(zooming: Boolean) {
        _isZooming.value = zooming
    }

    public companion object {
        /** The most pixels one page renders at: 4096 by 4096, 64 MB of RGBA. */
        public const val MAX_PAGE_PIXELS: Double = 4096.0 * 4096.0

        /** How far a drag moves before it pans while [panBeginsOnMovement] is on. */
        public const val PAN_MOVEMENT_THRESHOLD: Double = 10.0

        /**
         * The scale to render a page at: the stage's `renderScale`, capped so
         * the whole page stays within [MAX_PAGE_PIXELS]. While zooming the
         * scale is first rounded up to a width band
         * ([PageRenderResolution.bandedScale]), so a pinch restarts a page's
         * render only when it crosses a band. Otherwise it is the stage's scale
         * itself. Sizes in page points.
         */
        public fun renderScale(scale: Double, contentWidth: Double, contentHeight: Double, zooming: Boolean = false): Double {
            val wanted = if (zooming) PageRenderResolution.bandedScale(scale, contentWidth) else scale
            val area = contentWidth * contentHeight
            if (area <= 0) return wanted
            return min(wanted, sqrt(MAX_PAGE_PIXELS / area))
        }
    }
}
