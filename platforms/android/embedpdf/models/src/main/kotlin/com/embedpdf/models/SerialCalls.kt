package com.embedpdf.models

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.supervisorScope
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlin.coroutines.cancellation.CancellationException

/**
 * Runs main-thread calls one at a time, in the order they are enqueued. Each
 * call may suspend, and the next starts only after it returns.
 *
 * Swift's `SerialCalls` starts a task per call. A model here owns no scope,
 * so a call runs in whichever coroutine drains the queue: the model's
 * `run()`, which drains as calls arrive, or a caller awaiting a result with
 * [Call.await], which first runs every call queued ahead of its own. Used on
 * the main thread only.
 */
internal class SerialCalls {
    /** One queued call and the job running it, once it runs. */
    inner class Call<R> internal constructor(private val work: suspend () -> R) {
        internal val result = CompletableDeferred<R>()
        private var job: Job? = null
        private var cancelled = false

        /** Runs every call queued so far up to this one, and returns its result. */
        suspend fun await(): R {
            drain()
            return result.await()
        }

        /**
         * Cancels this call: a queued call never runs, and a running one is
         * cancelled. Calls after it still run.
         */
        fun cancel() {
            cancelled = true
            job?.cancel()
        }

        internal suspend fun execute() {
            if (cancelled) {
                result.cancel()
                return
            }
            try {
                supervisorScope {
                    val running = async { work() }
                    job = running
                    result.complete(running.await())
                }
            } catch (e: CancellationException) {
                result.cancel(e)
                // The drainer's own cancellation ends the drain; the call's
                // alone lets the next call run.
                currentCoroutineContext().ensureActive()
            } catch (e: Throwable) {
                result.completeExceptionally(e)
            } finally {
                job = null
            }
        }
    }

    private val queue = ArrayDeque<Call<*>>()
    private val mutex = Mutex()
    private val arrivals = Channel<Unit>(Channel.CONFLATED)

    /** Queues [work] after every earlier call; it joins the queue before this returns. */
    fun <R> enqueue(work: suspend () -> R): Call<R> {
        val call = Call(work)
        queue.addLast(call)
        arrivals.trySend(Unit)
        return call
    }

    /** Runs every queued call, in order, including those queued while it runs. */
    suspend fun drain() {
        mutex.withLock {
            while (true) {
                val next = queue.removeFirstOrNull() ?: break
                next.execute()
            }
        }
    }

    /** Drains each time a call arrives, until cancelled. A model's `run()` calls it. */
    suspend fun drainContinuously() {
        drain()
        for (arrival in arrivals) drain()
    }
}
