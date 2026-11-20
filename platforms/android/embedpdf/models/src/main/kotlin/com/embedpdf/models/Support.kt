package com.embedpdf.models

import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.ReceiveChannel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.launch
import kotlinx.coroutines.selects.select
import kotlinx.coroutines.withContext
import kotlin.coroutines.cancellation.CancellationException

/**
 * Runs [block] on `Dispatchers.Main.immediate`, where every model keeps its
 * state. A caller already on the main thread runs it without a dispatch.
 */
internal suspend fun <T> onMain(block: suspend CoroutineScope.() -> T): T =
    withContext(Dispatchers.Main.immediate, block)

/**
 * The [PdfException] a model shows for [error]: a `PdfException` is itself,
 * and anything else is `operation-failed` under its class name, as Swift's
 * `controlError` makes it.
 */
internal fun controlError(error: Throwable): PdfException = when (error) {
    is PdfException -> error
    is CancellationException ->
        PdfException(PdfException.Code.OPERATION_CANCELLED, "operation cancelled", "AbortError", error)
    else -> PdfException(
        PdfException.Code.OPERATION_FAILED,
        error.message ?: error.toString(),
        error.javaClass.simpleName,
        error,
    )
}

/**
 * Collects [flow] into a channel from a child of this scope that has
 * subscribed by the time this returns, so nothing the flow sends after this
 * call is missed. A flow that fails, as a capability's events do when its
 * document has already closed, closes the channel as a flow that completes
 * does.
 */
internal fun <T> CoroutineScope.subscribe(flow: Flow<T>): ReceiveChannel<T> {
    val channel = Channel<T>(Channel.UNLIMITED)
    launch(start = CoroutineStart.UNDISPATCHED) {
        try {
            flow.collect { channel.send(it) }
        } catch (e: PdfException) {
            // Ends like a completed flow.
        } finally {
            channel.close()
        }
    }
    return channel
}

/**
 * Follows a capability: subscribes to [events] and to the viewer's state
 * revisions, runs [initial], then hands each event to [receive] and each
 * revision to [refresh] until either stream ends, which is when the document
 * closes or the viewer is destroyed. The other stream is then cancelled.
 * Each stream is handled in order, the two concurrently.
 */
internal suspend fun <E> observeCapability(
    viewer: PdfViewer,
    events: Flow<E>,
    initial: suspend () -> Unit,
    refresh: suspend () -> Unit,
    receive: suspend (E) -> Unit,
): Unit = coroutineScope {
    val eventChannel = subscribe(events)
    val revisionChannel = subscribe(viewer.observeStateRevisions())
    val loops: List<Job> = listOf(
        launch { for (event in eventChannel) receive(event) },
        launch { for (revision in revisionChannel) refresh() },
    )
    initial()
    select { loops.forEach { loop -> loop.onJoin {} } }
    coroutineContext[Job]?.children?.forEach { it.cancel() }
}

/** A count or index the engine answered as `UInt`, as `Int`. */
internal fun UInt.toIntResult(result: String): Int {
    if (this > Int.MAX_VALUE.toUInt()) {
        throw PdfException(PdfException.Code.INVALID_STATE, "$result $this is past Int", "RangeError")
    }
    return toInt()
}
