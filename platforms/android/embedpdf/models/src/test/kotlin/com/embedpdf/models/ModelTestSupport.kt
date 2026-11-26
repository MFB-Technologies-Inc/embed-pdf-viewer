package com.embedpdf.models

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExecutorCoroutineDispatcher
import kotlinx.coroutines.Job
import kotlinx.coroutines.asCoroutineDispatcher
import kotlinx.coroutines.cancelChildren
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.withTimeoutOrNull
import org.junit.Assert.assertEquals
import org.junit.rules.TestWatcher
import org.junit.runner.Description
import java.util.concurrent.Executors

/**
 * Makes `Dispatchers.Main` one real thread for the test, the way the main
 * looper is one thread on a device. The engine answers on its own thread, so
 * these tests run in real time rather than on a virtual-time test scheduler,
 * whose skipped delays would fire a timeout while the engine still works.
 */
class MainThreadRule : TestWatcher() {
    private var dispatcher: ExecutorCoroutineDispatcher? = null

    override fun starting(description: Description) {
        val main = Executors.newSingleThreadExecutor { Thread(it, "test-main").apply { isDaemon = true } }
            .asCoroutineDispatcher()
        dispatcher = main
        Dispatchers.setMain(main)
    }

    override fun finished(description: Description) {
        Dispatchers.resetMain()
        dispatcher?.close()
    }
}

/**
 * Runs [body] on the main thread, as Swift's `@MainActor` tests run, and
 * cancels whatever it launched and left running when it ends. Fails after a
 * minute.
 */
fun mainTest(body: suspend CoroutineScope.() -> Unit): Unit = runBlocking(Dispatchers.Main) {
    withTimeout(60_000) {
        coroutineScope {
            try {
                body()
            } finally {
                coroutineContext.cancelChildren()
            }
        }
    }
}

/**
 * Runs [action], then waits up to five seconds for [read] to answer
 * [expected], reading again every few milliseconds on the main thread.
 */
suspend fun <T> expectObserved(read: suspend () -> T, expected: T, action: suspend () -> Unit = {}) {
    action()
    withTimeoutOrNull(5_000) {
        while (read() != expected) delay(5)
    }
    assertEquals(expected, read())
}

/** [expectObserved] for a state flow's value. */
suspend fun <T> expectObserved(flow: StateFlow<T>, expected: T, action: suspend () -> Unit = {}) =
    expectObserved({ flow.value }, expected, action)

/**
 * Whether [flow] took a new value during [action]. A state flow keeps the
 * value it holds when given an equal one, so an unchanged state keeps the
 * same instance.
 */
suspend fun <T> changes(flow: StateFlow<T>, action: suspend () -> Unit): Boolean {
    val before = flow.value
    action()
    return flow.value !== before
}

/** Whether any of [flows] took a new value during [action]. */
suspend fun anyChanges(flows: List<StateFlow<*>>, action: suspend () -> Unit): Boolean {
    val before = flows.map { it.value }
    action()
    return flows.indices.any { flows[it].value !== before[it] }
}

/**
 * Holds the first read that finishes on [snapshots] until the second value
 * completes, so a refresh started after it can land first. The first value
 * completes once the read is held.
 */
internal fun holdingFirstRead(snapshots: SnapshotRefresh): Pair<CompletableDeferred<Unit>, CompletableDeferred<Unit>> {
    val reached = CompletableDeferred<Unit>()
    val release = CompletableDeferred<Unit>()
    var held = false
    snapshots.readFinished = {
        if (!held) {
            held = true
            reached.complete(Unit)
            release.await()
        }
    }
    return reached to release
}

/** Whether [job] completes within five seconds. */
suspend fun ends(job: Job): Boolean = withTimeoutOrNull(5_000) { job.join() } != null
