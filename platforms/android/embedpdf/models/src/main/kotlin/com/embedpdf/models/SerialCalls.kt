package com.embedpdf.models

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.isActive
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
 * `run()`, which drains as calls arrive ([drainContinuously]), or a caller
 * awaiting a result with [Call.await], which runs every call queued ahead of
 * its own and then its own. When a drainer is cancelled, a call it had not
 * started goes back to the front of the queue for the next drainer; a call
 * it had started ends cancelled for its awaiter and never runs again. Used on
 * the main thread only.
 *
 * A call made to return at once ([post]) needs a drainer it does not wait
 * for. When no `run()` drains the queue, [post] starts one drain in
 * [actionScope], the caller's scope; without one it calls [unattended] so the
 * model can report that the call waits.
 */
internal class SerialCalls(
    private val actionScope: CoroutineScope? = null,
    private val unattended: (() -> Unit)? = null,
) {
    /** One queued call. */
    inner class Call<R> internal constructor(private val work: suspend () -> R) {
        internal val result = CompletableDeferred<R>()
        private var job: Job? = null
        private var cancelled = false

        /**
         * Runs every call queued ahead of this one, then this one, and returns
         * its result. Throws `CancellationException` when the call was
         * cancelled with [cancel], or when another drainer started it and was
         * cancelled.
         */
        suspend fun await(): R {
            drainThrough(this)
            return result.await()
        }

        /** Cancels this call: a queued call never runs, and a running one is cancelled. */
        fun cancel() {
            cancelled = true
            job?.cancel()
        }

        internal suspend fun execute() {
            if (cancelled) {
                result.cancel()
                return
            }
            var started = false
            try {
                supervisorScope {
                    val running = async {
                        started = true
                        work()
                    }
                    job = running
                    result.complete(running.await())
                }
            } catch (e: CancellationException) {
                // The drainer's own cancellation leaves a call that never
                // started for the next drainer. A started call ends cancelled:
                // running it again would repeat what it already did.
                if (!currentCoroutineContext().isActive) {
                    if (started) result.cancel(e)
                    throw e
                }
                result.cancel(e)
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

    /** [drainContinuously] calls under way. */
    private var continuousDrainers = 0

    /** Whether a drain [post] started in [actionScope] has not yet finished. */
    private var actionDrainActive = false

    /** Queues [work] after every earlier call; it joins the queue before this returns. */
    fun <R> enqueue(work: suspend () -> R): Call<R> {
        val call = Call(work)
        queue.addLast(call)
        arrivals.trySend(Unit)
        return call
    }

    /**
     * Queues [work] for a caller that does not wait for it. Unless a drainer
     * is running, starts a drain in [actionScope], or calls [unattended]
     * when there is no scope.
     */
    fun post(work: suspend () -> Unit): Call<Unit> {
        val call = enqueue(work)
        if (continuousDrainers > 0 || actionDrainActive) return call
        val scope = actionScope
        if (scope == null) {
            unattended?.invoke()
            return call
        }
        actionDrainActive = true
        // Atomic, so the flag clears even when the scope is already cancelled.
        scope.launch(Dispatchers.Main.immediate, start = CoroutineStart.ATOMIC) {
            try {
                drain()
            } finally {
                actionDrainActive = false
            }
        }
        return call
    }

    /** Runs every queued call, in order, including those queued while it runs. */
    suspend fun drain() = drainThrough(null)

    /**
     * Drains each time a call arrives, until cancelled. A model's `run()`
     * calls it, from a coroutine started undispatched so the queue counts as
     * drained from the moment `run()` launches it.
     */
    suspend fun drainContinuously() {
        continuousDrainers += 1
        try {
            drain()
            for (arrival in arrivals) drain()
        } finally {
            continuousDrainers -= 1
        }
    }

    /** Runs queued calls in order until [target] has finished, or until the queue is empty. */
    private suspend fun drainThrough(target: Call<*>?) {
        mutex.withLock {
            while (target == null || !target.result.isCompleted) {
                val next = queue.removeFirstOrNull() ?: break
                try {
                    next.execute()
                } catch (e: CancellationException) {
                    if (!next.result.isCompleted) queue.addFirst(next)
                    arrivals.trySend(Unit)
                    throw e
                }
            }
        }
    }
}
