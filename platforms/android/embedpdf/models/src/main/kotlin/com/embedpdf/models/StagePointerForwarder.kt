package com.embedpdf.models

import com.embedpdf.InteractionCapability
import com.embedpdf.Modifiers
import com.embedpdf.PdfException
import com.embedpdf.Point
import com.embedpdf.PointerKind
import com.embedpdf.PointerPageHit
import com.embedpdf.PointerPhase
import com.embedpdf.PointerSample
import com.embedpdf.StageCapability
import kotlinx.coroutines.CancellationException

/**
 * Turns a reader's touches into pointer samples for the interaction hub.
 * Each sample carries the touch's viewport point and the page the stage
 * finds under it, and reaches the hub after every sample sent before it.
 * Moves that arrive while another call is in flight collapse into the newest
 * one, and a collapsed move stays with its own gesture: the hub gets a
 * gesture's down, its newest move, then its up or cancel, with no sample of
 * another gesture between them.
 *
 * The samples go out as [calls] drains: from the owning model's `run()`,
 * from a drain [calls] starts in the owner's action scope, or from
 * [finished]. Used on the main thread only.
 */
internal class StagePointerForwarder(
    private val stage: StageCapability,
    private val interaction: InteractionCapability,
    /** Orders the samples, each of which leaves the main thread while the stage finds its page. */
    val calls: SerialCalls,
) {

    /** A move waiting in the queue. The call that sends it reads [point] when it runs. */
    private class WaitingMove(var point: Point?)

    /** The waiting move later moves still collapse into. A down, an up, a cancel or a tap lets go of it. */
    private var waitingMove: WaitingMove? = null

    /** Where the newest touch was. A cancel has no location of its own. */
    private var lastLocation = Point(0.0, 0.0)

    /** Whether a down has been asked for with no up or cancel after it. */
    private var gestureOpen = false

    /** Whether [interrupt] closed the gesture. Until the next down or tap, a move, an up and a cancel send nothing. */
    private var interrupted = false

    /** Each page lookup's outcome: `null` for one that answered, the error for one that failed. */
    var lookedUp: ((PdfException?) -> Unit)? = null

    /** Runs as each sample is handed to the hub. Tests record them. */
    var dispatched: ((PointerSample) -> Unit)? = null

    fun down(location: Point) {
        // An earlier gesture's waiting move stays queued ahead of its up.
        waitingMove = null
        interrupted = false
        gestureOpen = true
        lastLocation = location
        calls.post { send(PointerPhase.DOWN, location) }
    }

    fun move(location: Point) {
        if (interrupted) return
        lastLocation = location
        waitingMove?.let {
            it.point = location
            return
        }
        val move = WaitingMove(location)
        waitingMove = move
        calls.post {
            // Let go before the send suspends: a move that arrives during it
            // queues a call of its own.
            if (waitingMove === move) waitingMove = null
            val point = move.point ?: return@post
            send(PointerPhase.MOVE, point)
        }
    }

    /** A move still waiting is dispatched first, so the gesture ends where the touch last was. */
    fun up(location: Point) {
        if (interrupted) return
        waitingMove = null
        gestureOpen = false
        lastLocation = location
        calls.post { send(PointerPhase.UP, location) }
    }

    /** Drops this gesture's move if one still waits and sends a cancel at the last location. */
    fun cancel() {
        if (interrupted) return
        waitingMove?.point = null
        waitingMove = null
        gestureOpen = false
        val location = lastLocation
        calls.post { send(PointerPhase.CANCEL, location) }
    }

    /**
     * Cancels the open gesture, if there is one, and mutes the rest of its
     * touch: until the next down or tap, a move, an up and a cancel send
     * nothing. Does nothing with no gesture open.
     */
    fun interrupt() {
        if (!gestureOpen) return
        cancel()
        interrupted = true
    }

    /**
     * A down and an up at one point, both with a click count of 1. A gesture
     * still open is cancelled first, so the hub gets no second down inside it.
     */
    fun tap(location: Point) {
        if (gestureOpen) cancel()
        waitingMove = null
        interrupted = false
        gestureOpen = false
        lastLocation = location
        calls.post {
            val page = pageHit(location)
            dispatch(sample(PointerPhase.DOWN, location, page, 1u))
            dispatch(sample(PointerPhase.UP, location, page, 1u))
        }
    }

    /** Returns once every sample asked for so far has been handed to the hub. */
    suspend fun finished() {
        calls.enqueue {}.await()
    }

    private suspend fun send(phase: PointerPhase, location: Point) {
        dispatch(sample(phase, location, pageHit(location), null))
    }

    private suspend fun pageHit(location: Point): PointerPageHit? = try {
        // Performance: every sample awaits this lookup before its dispatch,
        // so a sample costs a round trip to the stage on top of the dispatch.
        // The page under a drag rarely changes between moves; mapping the
        // point through the last hit's page and transform while the camera
        // stands still saves the lookup.
        val hit = stage.pageAt(location)
        lookedUp?.invoke(null)
        hit?.let { PointerPageHit(page = it.page, point = it.point, scale = it.scale, rotation = it.rotation, zoom = it.zoom) }
    } catch (e: CancellationException) {
        throw e
    } catch (e: PdfException) {
        lookedUp?.invoke(e)
        null
    }

    private fun sample(phase: PointerPhase, location: Point, page: PointerPageHit?, clickCount: UInt?): PointerSample =
        PointerSample(
            phase = phase, viewport = location, page = page, modifiers = Modifiers(),
            clickCount = clickCount, pointerType = PointerKind.TOUCH,
        )

    private fun dispatch(sample: PointerSample) {
        interaction.dispatchPointer(sample)
        dispatched?.invoke(sample)
    }
}
