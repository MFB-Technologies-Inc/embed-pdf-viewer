package com.embedpdf

import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow

/**
 * Fans one producer out to any number of collectors. Each collection of
 * [flow] is its own subscription with its own buffer, installed when
 * collection starts, so one collector's cancellation or slowness never
 * affects another. A subscription only sees values sent after it was
 * installed, and every subscription completes at [finish].
 */
internal class BroadcastHub<T>(private val capacity: Int) {
    private val lock = Any()
    private val subscribers = LinkedHashSet<Channel<T>>()
    private var finished = false

    /** The number of live subscriptions, for tests. */
    val subscriberCount: Int get() = synchronized(lock) { subscribers.size }

    /**
     * Collecting it subscribes before its first suspension, so a collector
     * started with `CoroutineStart.UNDISPATCHED` is subscribed by the time the
     * `launch` returns.
     */
    fun flow(): Flow<T> = flow {
        val channel = Channel<T>(capacity, BufferOverflow.DROP_OLDEST)
        val live = synchronized(lock) {
            if (!finished) subscribers.add(channel)
            !finished
        }
        if (!live) return@flow
        try {
            for (value in channel) emit(value)
        } finally {
            synchronized(lock) { subscribers.remove(channel) }
            channel.cancel()
        }
    }

    fun send(value: T) {
        val current = synchronized(lock) { subscribers.toList() }
        for (subscriber in current) subscriber.trySend(value)
    }

    /** Completes every subscription after its buffered values, and every later one at once. */
    fun finish() {
        val current = synchronized(lock) {
            finished = true
            subscribers.toList().also { subscribers.clear() }
        }
        for (subscriber in current) subscriber.close()
    }
}

/** State revisions coalesce: a slow collector keeps only the newest. */
internal fun stateRevisionHub(): BroadcastHub<Long> = BroadcastHub(1)

/** Reported errors keep the newest 16 per collector. */
internal fun reportedErrorHub(): BroadcastHub<PdfException> = BroadcastHub(16)
