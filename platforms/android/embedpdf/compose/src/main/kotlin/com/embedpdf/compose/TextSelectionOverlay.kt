package com.embedpdf.compose

import android.content.ClipData
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.awaitLongPressOrCancellation
import androidx.compose.foundation.layout.Spacer
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.AwaitPointerEventScope
import androidx.compose.ui.input.pointer.PointerId
import androidx.compose.ui.input.pointer.PointerInputScope
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.pointer.positionChanged
import androidx.compose.ui.layout.LayoutCoordinates
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.ClipEntry
import androidx.compose.ui.platform.LocalClipboard
import androidx.compose.ui.platform.LocalTextToolbar
import androidx.compose.ui.platform.TextToolbarStatus
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.copyText
import androidx.compose.ui.semantics.semantics
import com.embedpdf.PageGeometry
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.Point
import com.embedpdf.StageVisiblePage
import com.embedpdf.models.TextSelectionModel
import com.embedpdf.models.geometry
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.DelicateCoroutinesApi
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import androidx.compose.ui.geometry.Rect as LocalRect
import androidx.compose.ui.geometry.Size as LocalSize
import com.embedpdf.Rect as PageRect

/** What starts a [TextSelectionOverlay] drag. */
public enum class TextSelectionActivation {
    /** A touch begins the selection where it lands, and the overlay takes every touch in its frame. */
    DRAG,

    /**
     * A long press, of the platform's long-press timeout, arms the selection.
     * It begins where the finger is when it next moves, and extends while the
     * finger drags. A touch that moves or lifts before the press completes is
     * not the overlay's, so a gesture behind it, such as a stage reader's
     * pan, keeps it.
     */
    LONG_PRESS,
}

/**
 * Selects text on one page by dragging, draws the page's selected rects, and
 * copies the selection.
 *
 * The overlay fills its frame and maps the whole frame onto the page as
 * shown, so size it to the page: place it over the page's image, or return it
 * from a [StageReader]'s page overlay. One drag is one selection gesture: its
 * first point begins the selection, later points extend it, and lifting ends
 * it. A drag the system cancels ends the selection the same way, and so does
 * a drag still open when the overlay leaves the composition.
 *
 * When a drag that found text ends, the platform text toolbar offers Copy,
 * which reads [TextSelectionModel.selectedText] and puts it on the clipboard.
 * TalkBack names the overlay "Text selection" and offers the same copy as an
 * action while the page has selected text. Read [TextSelectionModel.error]
 * for a failure and [TextSelectionModel.isSelecting] for whether the drag
 * found text. Swift's overlay has no copy; Android has no system menu that
 * would offer one for a drawn selection.
 *
 * @param model the model the drag drives and whose rects are drawn.
 * @param page the page under the overlay.
 * @param geometry the page's turn and unrotated size; the frame covers the
 *   page turned this way.
 * @param start what the drag's first point selects.
 * @param tint the fill of the selected rects.
 * @param activation what starts a selection drag.
 */
@Composable
public fun TextSelectionOverlay(
    model: TextSelectionModel,
    page: PageRef,
    geometry: PageGeometry,
    modifier: Modifier = Modifier,
    start: TextSelectionModel.Start = TextSelectionModel.Start.CARET,
    tint: Color = MaterialTheme.colorScheme.primary.copy(alpha = 0.3f),
    activation: TextSelectionActivation = TextSelectionActivation.DRAG,
) {
    val rects by model.rects.collectAsState()
    val pageRects = rects[page].orEmpty()
    val drag = remember(model) { TextSelectionDrag() }
    val scope = rememberCoroutineScope()
    val toolbar = LocalTextToolbar.current
    val clipboard = LocalClipboard.current
    val label = stringResource(R.string.embedpdf_text_selection)
    val coordinates = remember { arrayOfNulls<LayoutCoordinates>(1) }
    val current by rememberUpdatedState(Placement(page, geometry, start))
    // Whether this overlay put up the toolbar, which the whole window shares.
    val showsMenu = remember { BooleanArray(1) }

    fun hideMenu() {
        if (showsMenu[0] && toolbar.status == TextToolbarStatus.Shown) toolbar.hide()
        showsMenu[0] = false
    }

    fun copy() {
        hideMenu()
        scope.launch {
            val text = model.selectedText()
            if (!text.isNullOrEmpty()) clipboard.setClipEntry(ClipEntry(ClipData.newPlainText(label, text)))
        }
    }

    fun showMenu() {
        val layout = coordinates[0]?.takeIf { it.isAttached } ?: return
        val shown = model.rects.value[current.page].orEmpty()
        if (shown.isEmpty()) return
        val size = LocalSize(layout.size.width.toFloat(), layout.size.height.toFloat())
        val bounds = shown.map { localRect(it, size, current.geometry) }.reduce { a, b -> LocalRect(minOf(a.left, b.left), minOf(a.top, b.top), maxOf(a.right, b.right), maxOf(a.bottom, b.bottom)) }
        val topLeft = layout.localToRoot(bounds.topLeft)
        val bottomRight = layout.localToRoot(bounds.bottomRight)
        toolbar.showMenu(LocalRect(topLeft, bottomRight), onCopyRequested = { copy() })
        showsMenu[0] = true
    }

    /**
     * Ends the open drag. It reaches the model even when the overlay's scope
     * is ending: an atomic start runs the body of a coroutine already
     * cancelled, and the end itself does not cancel.
     */
    @OptIn(DelicateCoroutinesApi::class)
    fun end() {
        scope.launch(start = CoroutineStart.ATOMIC) {
            val found = withContext(NonCancellable) { model.dragEnded() }
            if (found && isActive) showMenu()
        }
    }

    fun handle(event: TextSelectionDrag.Event, local: Offset = Offset.Zero, size: LocalSize = LocalSize.Zero) {
        val place = current
        when (drag.handle(event)) {
            TextSelectionDrag.Command.BEGIN -> {
                hideMenu()
                model.dragBegan(pagePoint(local, size, place.geometry), place.page, place.start)
            }
            TextSelectionDrag.Command.EXTEND -> model.dragChanged(pagePoint(local, size, place.geometry), place.page)
            TextSelectionDrag.Command.END -> end()
            null -> {}
        }
    }

    LaunchedEffect(pageRects.isEmpty()) {
        if (pageRects.isEmpty()) hideMenu()
    }
    DisposableEffect(model) {
        onDispose {
            hideMenu()
            if (drag.handle(TextSelectionDrag.Event.DISAPPEARED) == TextSelectionDrag.Command.END) end()
        }
    }

    Spacer(
        modifier
            .onGloballyPositioned { coordinates[0] = it }
            .semantics {
                contentDescription = label
                if (pageRects.isNotEmpty()) {
                    copyText {
                        copy()
                        true
                    }
                }
            }
            .pointerInput(model, activation) {
                val frame = { LocalSize(size.width.toFloat(), size.height.toFloat()) }
                when (activation) {
                    TextSelectionActivation.DRAG -> dragSelection { event, at -> handle(event, at, frame()) }
                    TextSelectionActivation.LONG_PRESS -> longPressSelection { event, at -> handle(event, at, frame()) }
                }
            }
            .drawBehind {
                // One pass over the page's rects, none of them a node of its own.
                for (rect in pageRects) {
                    val local = localRect(rect, size, geometry)
                    drawRect(tint, local.topLeft, local.size)
                }
            },
    )
}

/**
 * An overlay for a page a stage shows, turned as the stage turns it, view
 * rotation included. [info] is the same page's `PageInfo`, for its
 * unrotated size.
 */
@Composable
public fun TextSelectionOverlay(
    model: TextSelectionModel,
    visiblePage: StageVisiblePage,
    info: PageInfo,
    modifier: Modifier = Modifier,
    start: TextSelectionModel.Start = TextSelectionModel.Start.CARET,
    tint: Color = MaterialTheme.colorScheme.primary.copy(alpha = 0.3f),
    activation: TextSelectionActivation = TextSelectionActivation.DRAG,
) {
    TextSelectionOverlay(model, visiblePage.page, visiblePage.geometry(info), modifier, start, tint, activation)
}

/** The page, geometry and start an event maps onto, read when it arrives. */
private data class Placement(val page: PageRef, val geometry: PageGeometry, val start: TextSelectionModel.Start)

/** Takes every touch: the first point begins, later points extend, and the lift ends. */
private suspend fun PointerInputScope.dragSelection(handle: (TextSelectionDrag.Event, Offset) -> Unit) {
    awaitEachGesture {
        val down = awaitFirstDown()
        down.consume()
        handle(TextSelectionDrag.Event.CHANGED, down.position)
        followDrag(down.id, handle)
    }
}

/**
 * Takes a touch only once a long press has succeeded; until then it consumes
 * nothing, so a gesture behind the overlay keeps a touch that moves first.
 */
private suspend fun PointerInputScope.longPressSelection(handle: (TextSelectionDrag.Event, Offset) -> Unit) {
    awaitEachGesture {
        val down = awaitFirstDown(requireUnconsumed = false)
        val press = awaitLongPressOrCancellation(down.id) ?: return@awaitEachGesture
        press.consume()
        followDrag(press.id, handle)
    }
}

/**
 * Reports [pointer]'s moves as changes and its lift as the end, consuming
 * them. A gesture that ends any other way, the pointer gone or the input
 * cancelled, is reported cancelled.
 */
private suspend fun AwaitPointerEventScope.followDrag(pointer: PointerId, handle: (TextSelectionDrag.Event, Offset) -> Unit) {
    var lifted = false
    try {
        while (true) {
            val change = awaitPointerEvent().changes.firstOrNull { it.id == pointer } ?: break
            if (!change.pressed) {
                change.consume()
                lifted = true
                handle(TextSelectionDrag.Event.ENDED, change.position)
                break
            }
            if (change.positionChanged()) handle(TextSelectionDrag.Event.CHANGED, change.position)
            change.consume()
        }
    } finally {
        if (!lifted) handle(TextSelectionDrag.Event.CANCELLED, Offset.Zero)
    }
}

/**
 * The point in unrotated page space at [local], a point in a frame of [size]
 * that covers the page as shown. Each axis scales by its own length. An axis
 * on which [size] is not greater than zero maps to coordinate 0 of the page
 * as shown, so the result is finite for an empty frame.
 */
internal fun pagePoint(local: Offset, size: LocalSize, geometry: PageGeometry): Point {
    val shown = geometry.displaySize
    return geometry.toPage(
        Point(local.x * ratio(shown.width, size.width.toDouble()), local.y * ratio(shown.height, size.height.toDouble())),
    )
}

/**
 * Where [rect], a rect in unrotated page space, sits in a frame of [size]
 * that covers the page as shown: the inverse of [pagePoint]. An axis on which
 * the page as shown is not greater than zero gets offset 0 and length 0.
 */
internal fun localRect(rect: PageRect, size: LocalSize, geometry: PageGeometry): LocalRect {
    val shown = geometry.displaySize
    val turned = geometry.toDisplay(rect)
    val x = ratio(size.width.toDouble(), shown.width)
    val y = ratio(size.height.toDouble(), shown.height)
    return LocalRect(
        (turned.x * x).toFloat(),
        (turned.y * y).toFloat(),
        ((turned.x + turned.width) * x).toFloat(),
        ((turned.y + turned.height) * y).toFloat(),
    )
}

/**
 * Where [point], a point in unrotated page space, sits in a frame of [size]
 * that covers the page as shown: [localRect] for one point.
 */
internal fun localPoint(point: Point, size: LocalSize, geometry: PageGeometry): Offset {
    val shown = geometry.displaySize
    val turned = geometry.toDisplay(point)
    return Offset(
        (turned.x * ratio(size.width.toDouble(), shown.width)).toFloat(),
        (turned.y * ratio(size.height.toDouble(), shown.height)).toFloat(),
    )
}

/** `length / divisor`, or 0 when [divisor] is not greater than zero. */
private fun ratio(length: Double, divisor: Double): Double = if (divisor > 0) length / divisor else 0.0
