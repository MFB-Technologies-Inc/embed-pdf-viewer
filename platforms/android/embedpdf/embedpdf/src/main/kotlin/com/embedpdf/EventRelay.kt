package com.embedpdf

import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.SendChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import uniffi.epdf_ffi.Subscription
import uniffi.epdf_ffi.ViewerException
import uniffi.epdf_viewer_host.ViewerDelegate

/**
 * The viewer's delegate. The kernel calls it on its own thread, so it only
 * hands values to the hubs, which never block.
 */
internal class StateRelay(
    private val revisions: BroadcastHub<Long>,
    private val errors: BroadcastHub<PdfException>,
) : ViewerDelegate {
    override fun stateChanged(revision: ULong) {
        // A revision counts kernel changes from zero; it never reaches 2^63.
        revisions.send(revision.toLong())
    }

    override fun report(error: ViewerException) {
        errors.send(PdfException.from(error))
    }
}

/**
 * Event subscriptions that end with the viewer. Each collection of a flow from
 * [stream] subscribes when collection starts, cancels its kernel subscription
 * when collection ends, and completes when [finishAll] runs.
 */
internal class StreamRegistry {
    private val lock = Any()
    private val channels = LinkedHashSet<Channel<*>>()
    private var finished = false

    /**
     * [subscribe] receives the channel the listener sends into and returns the
     * kernel subscription; events arrive unbounded, in order.
     */
    fun <E> stream(subscribe: (SendChannel<E>) -> Subscription): Flow<E> = flow {
        val channel = Channel<E>(Channel.UNLIMITED)
        val live = synchronized(lock) {
            if (!finished) channels.add(channel)
            !finished
        }
        if (!live) return@flow
        var subscription: Subscription? = null
        try {
            subscription = try {
                subscribe(channel)
            } catch (e: Exception) {
                throw PdfException.from(e)
            }
            for (event in channel) emit(event)
        } finally {
            synchronized(lock) { channels.remove(channel) }
            channel.cancel()
            subscription?.cancel()
            subscription?.close()
        }
    }

    fun finishAll() {
        val current = synchronized(lock) {
            finished = true
            channels.toList().also { channels.clear() }
        }
        for (channel in current) channel.close()
    }
}
