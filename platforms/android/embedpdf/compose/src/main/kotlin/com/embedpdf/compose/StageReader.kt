package com.embedpdf.compose

import android.content.Context
import android.view.accessibility.AccessibilityManager
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.absoluteOffset
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.AbsoluteAlignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.input.pointer.PointerInputChange
import androidx.compose.ui.input.pointer.PointerInputScope
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.pointer.positionChanged
import androidx.compose.ui.input.pointer.util.VelocityTracker
import androidx.compose.ui.unit.Velocity
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.runtime.produceState
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.semantics.ScrollAxisRange
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.horizontalScrollAxisRange
import androidx.compose.ui.semantics.scrollBy
import androidx.compose.ui.semantics.verticalScrollAxisRange
import com.embedpdf.PdfException
import com.embedpdf.StageScrollMetrics
import com.embedpdf.StageScrollToOptions
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import com.embedpdf.LinkActivation
import com.embedpdf.StageVisiblePage
import com.embedpdf.models.StageReaderModel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.launch

/**
 * A scrolling, zooming reader over a document's stage. Each visible page
 * sits where the stage put it and renders at the stage's render scale.
 * Viewport units are dp: the reader passes the stage its size in dp and the
 * display density as the pixel ratio.
 *
 * A model made with an `InteractionCapability` also gets the reader's
 * touches as pointer input. A drag pans under the default tool and goes to
 * the active tool under any other ([StageReaderModel.forwardsDrags]), so a
 * drawing tool consumes the drag and the stage does not pan. The route is
 * chosen when the touch lands and holds until it lifts. A pan moves the
 * stage only once the touch passes the platform's touch slop, and leaves the
 * moves within it to the overlay, so a finger's jitter neither pans nor
 * flings. A tap goes to the hub once either way: as
 * [StageReaderModel.tapped] when its touch pans, and as the forwarded drag's
 * own down and up when it does not. Only a tap that pans can be half of a
 * double tap, and a touch whose lift the overlay takes is no tap. A second finger
 * turns the touch into a pinch: an open pan closes without a fling, a
 * forwarded drag is cancelled at the hub so the tool does not commit it, and
 * the rest of the touch zooms. A drag the system cancels is cancelled at the
 * hub too.
 *
 * [pageOverlay] is laid over each visible page, in the page's frame with its
 * origin at the page's top left; `StageVisiblePage.viewportPoint` turns a
 * point in it into a viewport point. The overlay's own gestures take
 * priority over the reader's drag, whether that drag pans or goes to the
 * active tool: a touch whose down the overlay consumes never drags the
 * reader, and a drag whose move the overlay consumes ends there, a pan with
 * no fling and a forwarded drag cancelled. Set
 * [StageReaderModel.isPanEnabled] to false when the overlay needs every
 * touch that would pan. The overlay does not take priority over the reader's
 * tap: a tap the overlay handles still reaches [StageReaderModel.tapped],
 * where a link under it activates.
 *
 * [onLinkActivation] receives each of the model's
 * [StageReaderModel.linkActivations] while the reader is composed; opening
 * a `Uri` is the app's job. The reader runs the model while it is composed,
 * so the app does not run it as well, and a new model starts fresh. Pages lay
 * out left to right whatever the layout direction, as the stage places them.
 *
 * TalkBack reads the reader as "Document pages" and each visible page as
 * "Page N of M", and scrolls the reader with its scroll actions, which the
 * reader reports through its scroll ranges in pixels.
 */
@Composable
public fun StageReader(
    model: StageReaderModel,
    modifier: Modifier = Modifier,
    onLinkActivation: (LinkActivation) -> Unit = {},
    pageOverlay: @Composable (StageVisiblePage) -> Unit = {},
) {
    ModelScope(model) { reader ->
        // Performance: `visiblePages` changes with every camera event, so
        // each step of a pan or a pinch recomposes every visible page's row
        // and calls `pageOverlay` for each. It bites with an overlay that is
        // costly to build. Page positions in content space under one offset
        // for the camera leave the rows unchanged through a pan.
        val pages by reader.visiblePages.collectAsState()
        val zooming by reader.isZooming.collectAsState()
        val revisions by reader.revisions.revisions.collectAsState()
        val activations by rememberUpdatedState(onLinkActivation)
        val density = LocalDensity.current.density
        val scope = rememberCoroutineScope()
        var size by remember { mutableStateOf(IntSize.Zero) }
        val label = stringResource(R.string.embedpdf_document_pages)
        val resources = LocalResources.current
        val pageLabel = { number: Int, count: Int ->
            if (count > 0) {
                resources.getString(R.string.embedpdf_page_of, number, count)
            } else {
                resources.getString(R.string.embedpdf_page_number, number)
            }
        }
        val camera by reader.camera.collectAsState()
        // Accessibility reads the scroll position from the stage, so the
        // metrics are read only while an accessibility service is on.
        // Performance: then one engine round trip per camera event, so a pan
        // or a pinch reads the metrics at each step; a model flow that
        // carries them with the camera removes the reads.
        val accessibility = rememberAccessibilityEnabled()
        val metrics by produceState<StageScrollMetrics?>(null, reader, camera, size, accessibility) {
            value = if (accessibility) readOrNull { reader.stage.scrollMetrics() } else null
        }
        val visibleSet = pages.map { it.page }
        val pageCount by produceState(0, reader, visibleSet) {
            value = readOrNull { reader.stage.pageCount() } ?: value
        }

        LaunchedEffect(reader) { reader.run() }
        LaunchedEffect(reader) { reader.linkActivations.collect { activations(it) } }
        LaunchedEffect(reader, size, density) {
            if (size != IntSize.Zero) reader.sizeChanged(size.width / density, size.height / density, density)
        }

        Box(
            modifier
                .clipToBounds()
                .onSizeChanged { size = it }
                .semantics {
                    contentDescription = label
                    metrics?.let { scroll ->
                        // In pixels, as the platform's scroll actions measure.
                        if (scroll.scrollableX) {
                            horizontalScrollAxisRange = ScrollAxisRange(
                                value = { (scroll.scrollLeft * density).toFloat() },
                                maxValue = { ((scroll.scrollWidth - scroll.clientWidth) * density).toFloat().coerceAtLeast(0f) },
                            )
                        }
                        if (scroll.scrollableY) {
                            verticalScrollAxisRange = ScrollAxisRange(
                                value = { (scroll.scrollTop * density).toFloat() },
                                maxValue = { ((scroll.scrollHeight - scroll.clientHeight) * density).toFloat().coerceAtLeast(0f) },
                            )
                        }
                        // False at an edge the scroll would push against, from the metrics last read.
                        scrollBy { x, y ->
                            val left = (x / density).toDouble()
                            val top = (y / density).toDouble()
                            if (!scroll.canScroll(left, top)) return@scrollBy false
                            reader.stage.scrollBy(StageScrollToOptions(left = left, top = top))
                            true
                        }
                    }
                }
                .pointerInput(reader) { readerGestures(reader, scope) },
            contentAlignment = AbsoluteAlignment.TopLeft,
        ) {
            for (page in pages) {
                key(page.page.pageObjectNumber) {
                    Box(
                        Modifier
                            .absoluteOffset(page.screenX.dp, page.screenY.dp)
                            .size(page.transform.viewWidth.dp, page.transform.viewHeight.dp)
                            .semantics { contentDescription = pageLabel(page.pageIndex.toInt() + 1, pageCount) },
                        contentAlignment = AbsoluteAlignment.TopLeft,
                    ) {
                        val transform = page.transform
                        PageImageView(
                            reader.render,
                            page.page,
                            StageReaderModel.renderScale(transform.renderScale, transform.contentWidth, transform.contentHeight, zooming),
                            Modifier.fillMaxSize(),
                            revision = revisions[page.page] ?: 0,
                        )
                        pageOverlay(page)
                    }
                }
            }
        }
    }
}

/** Whether a scroll by [left] and [top] viewport dp moves the stage on either axis. */
internal fun StageScrollMetrics.canScroll(left: Double, top: Double): Boolean {
    val maxLeft = (scrollWidth - clientWidth).coerceAtLeast(0.0)
    val maxTop = (scrollHeight - clientHeight).coerceAtLeast(0.0)
    val alongX = scrollableX && ((left > 0 && scrollLeft < maxLeft) || (left < 0 && scrollLeft > 0))
    val alongY = scrollableY && ((top > 0 && scrollTop < maxTop) || (top < 0 && scrollTop > 0))
    return alongX || alongY
}

/** Whether an accessibility service is on, following it as it changes. */
@Composable
private fun rememberAccessibilityEnabled(): Boolean {
    val context = LocalContext.current
    val manager = remember(context) { context.getSystemService(Context.ACCESSIBILITY_SERVICE) as AccessibilityManager }
    var enabled by remember(manager) { mutableStateOf(manager.isEnabled) }
    DisposableEffect(manager) {
        val listener = AccessibilityManager.AccessibilityStateChangeListener { enabled = it }
        manager.addAccessibilityStateChangeListener(listener)
        enabled = manager.isEnabled
        onDispose { manager.removeAccessibilityStateChangeListener(listener) }
    }
    return enabled
}

/** [read]'s value, or `null` when the stage refuses, as after its document closes. */
private suspend fun <T> readOrNull(read: suspend () -> T): T? = try {
    read()
} catch (e: PdfException) {
    null
}

/** Where a touch's drag goes, chosen when it lands. */
private enum class DragRoute { PAN, TOOL, NONE }

/**
 * Routes each touch to the model: a pan, a forwarded tool drag or nothing,
 * then a pinch once a second finger lands, a tap and a double tap. Positions
 * reach the model in dp.
 */
private suspend fun PointerInputScope.readerGestures(reader: StageReaderModel, scope: CoroutineScope) {
    // Kept across touches, for the double tap.
    var lastTapUptime: Long? = null
    var lastTapPosition = Offset.Zero

    fun send(call: suspend () -> Unit) {
        // Undispatched, so calls reach the model's queues in the order made.
        scope.launch(start = CoroutineStart.UNDISPATCHED) {
            try {
                call()
            } catch (e: PdfException) {
                // The model's run() ended while it ran this call, as when the
                // viewer closes during a drag: the call is dropped, and an
                // uncaught exception here would crash the app.
                if (e.code != PdfException.Code.OPERATION_CANCELLED) throw e
            }
        }
    }

    fun dp(position: Offset): Offset = position / density

    awaitEachGesture {
        val down = awaitFirstDown(requireUnconsumed = false)
        val start = dp(down.position)
        var route = when {
            // The overlay took the touch.
            down.isConsumed -> DragRoute.NONE
            reader.forwardsDrags.value -> DragRoute.TOOL
            reader.isPanEnabled -> DragRoute.PAN
            else -> DragRoute.NONE
        }
        // Whether the hub has this touch's down and up from the drag already.
        val forwarded = route == DragRoute.TOOL
        val velocity = VelocityTracker()
        velocity.addPosition(down.uptimeMillis, down.position)
        when (route) {
            DragRoute.TOOL -> {
                reader.pointerDown(start.x, start.y)
                down.consume()
            }
            DragRoute.PAN -> {
                send { reader.dragChanged(0f, 0f) }
                down.consume()
            }
            DragRoute.NONE -> {}
        }
        var moved = false
        // Where the pan's translation is measured from: the point where the
        // finger crossed the slop, so the stage does not jump by the slop.
        var panOrigin = start
        var pinchStart: Pair<Float, Offset>? = null
        var pinchEnded = false
        var lifted = false

        /** Closes the drag without its natural end: a pan with no fling, a forwarded drag cancelled. */
        fun drop() {
            when (route) {
                DragRoute.PAN -> send { reader.dragCancelled() }
                DragRoute.TOOL -> reader.pointerCancelled()
                DragRoute.NONE -> {}
            }
            route = DragRoute.NONE
        }

        try {
            while (true) {
                val event = awaitPointerEvent()
                val pressed = event.changes.filter { it.pressed }
                if (pressed.size >= 2 || pinchStart != null) {
                    if (pinchStart == null) {
                        // The pinch cancels a forwarded drag at the hub itself.
                        if (route == DragRoute.PAN) drop() else route = DragRoute.NONE
                        moved = true
                        pinchStart = span(pressed) to centroid(pressed)
                    }
                    val (startSpan, center) = pinchStart
                    if (pressed.size >= 2 && !pinchEnded && startSpan > 0f) {
                        val anchor = dp(center)
                        reader.magnifyChanged(span(pressed) / startSpan, anchor.x, anchor.y)
                    } else if (pressed.size < 2 && !pinchEnded) {
                        // The pinch ends with its second finger; the rest of the touch does nothing.
                        pinchEnded = true
                        send { reader.magnifyEnded() }
                    }
                    event.changes.forEach(PointerInputChange::consume)
                    if (pressed.isEmpty()) {
                        lifted = true
                        break
                    }
                    continue
                }
                val change = event.changes.firstOrNull { it.id == down.id } ?: break
                if (!change.pressed) {
                    lifted = true
                    velocity.addPosition(change.uptimeMillis, change.position)
                    val end = dp(change.position)
                    var doubled = false
                    when {
                        // An overlay took the lift, as a text selection armed
                        // by a long press does: no tap, and no half of a double tap.
                        change.isConsumed -> lastTapUptime = null
                        // The tool had this touch's down and up; it is not a tap
                        // of the reader's, and a double tap would zoom under the tool.
                        forwarded -> lastTapUptime = null
                        !moved -> {
                            reader.tapped(end.x, end.y)
                            val previous = lastTapUptime
                            doubled = previous != null &&
                                change.uptimeMillis - previous <= viewConfiguration.doubleTapTimeoutMillis &&
                                (change.position - lastTapPosition).getDistance() <= viewConfiguration.touchSlop * 4
                            lastTapUptime = if (doubled) null else change.uptimeMillis
                            lastTapPosition = change.position
                        }
                    }
                    when (route) {
                        DragRoute.PAN -> {
                            // A touch that never passed the slop is a tap: it ends with no fling.
                            val fling = if (moved) velocity.calculateVelocity() else Velocity.Zero
                            send {
                                reader.dragEnded(fling.x / density, fling.y / density)
                                // After the pan's end, whose stage gesture would stop the zoom it starts.
                                if (doubled) reader.doubleTapped(end.x, end.y)
                            }
                            change.consume()
                        }
                        DragRoute.TOOL -> {
                            reader.pointerUp(end.x, end.y)
                            change.consume()
                        }
                        DragRoute.NONE -> {}
                    }
                    if (doubled && route != DragRoute.PAN) send { reader.doubleTapped(end.x, end.y) }
                    break
                }
                val travel = change.position - down.position
                val distance = travel.getDistance()
                if (!moved && distance > viewConfiguration.touchSlop) {
                    moved = true
                    panOrigin = dp(down.position + travel * (viewConfiguration.touchSlop / distance))
                }
                // An overlay took the move, or the pan was switched off under it.
                if (change.isConsumed || (route == DragRoute.PAN && !reader.isPanEnabled)) {
                    drop()
                    continue
                }
                if (!change.positionChanged()) continue
                velocity.addPosition(change.uptimeMillis, change.position)
                // Within the slop a pan neither moves nor takes the move, so a
                // finger's jitter does not pan and an overlay's long press,
                // which a taken move would cancel, still arms.
                if (route == DragRoute.PAN && !moved) continue
                val at = dp(change.position)
                when (route) {
                    DragRoute.PAN -> {
                        send { reader.dragChanged(at.x - panOrigin.x, at.y - panOrigin.y) }
                        change.consume()
                    }
                    DragRoute.TOOL -> {
                        reader.pointerMoved(at.x, at.y)
                        change.consume()
                    }
                    DragRoute.NONE -> {}
                }
            }
        } finally {
            // Cancelled: the pointer input restarted, or the touch went away unlifted.
            if (!lifted) {
                drop()
                if (pinchStart != null && !pinchEnded) send { reader.magnifyEnded() }
            }
        }
    }
}

/** The mean distance of the pointers from their centroid. */
private fun span(pointers: List<PointerInputChange>): Float {
    if (pointers.size < 2) return 0f
    val center = centroid(pointers)
    return pointers.map { (it.position - center).getDistance() }.average().toFloat()
}

private fun centroid(pointers: List<PointerInputChange>): Offset {
    if (pointers.isEmpty()) return Offset.Zero
    var sum = Offset.Zero
    for (pointer in pointers) sum += pointer.position
    return sum / pointers.size.toFloat()
}
