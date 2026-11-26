package com.embedpdf.models

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/** `PageRenderTaskTests`, on a virtual-time main dispatcher. */
@OptIn(ExperimentalCoroutinesApi::class)
class PageRenderTaskTest {
    private val main = StandardTestDispatcher()

    @Before
    fun setMain() = Dispatchers.setMain(main)

    @After
    fun resetMain() = Dispatchers.resetMain()

    /** Work that takes [finish] to unwind once cancelled, as an engine call does. */
    private suspend fun slowToUnwind(entered: CompletableDeferred<Unit>, finish: CompletableDeferred<Unit>, events: MutableList<String>, name: String) {
        entered.complete(Unit)
        withContext(NonCancellable) { finish.await() }
        events += name
    }

    @Test
    fun replacementWaitsForCancelledWorkToFinish() = runTest(main) {
        val work = PageRenderTask()
        val entered = CompletableDeferred<Unit>()
        val finish = CompletableDeferred<Unit>()
        val events = mutableListOf<String>()
        val first = launch { work.run { slowToUnwind(entered, finish, events, "first finished") } }
        entered.await()
        val second = launch { work.run { events += "replacement" } }
        testScheduler.runCurrent()
        assertTrue(events.isEmpty())
        finish.complete(Unit)
        first.join()
        second.join()
        assertEquals(listOf("first finished", "replacement"), events)
    }

    @Test
    fun theNewestQueuedRequestSupersedesEarlierWaitingWork() = runTest(main) {
        val work = PageRenderTask()
        val entered = CompletableDeferred<Unit>()
        val finish = CompletableDeferred<Unit>()
        val events = mutableListOf<String>()
        val first = launch { work.run { slowToUnwind(entered, finish, events, "first") } }
        entered.await()
        val second = launch { work.run { events += "superseded" } }
        testScheduler.runCurrent()
        val third = launch { work.run { events += "newest" } }
        testScheduler.runCurrent()
        finish.complete(Unit)
        first.join()
        second.join()
        third.join()
        assertEquals(listOf("first", "newest"), events)
    }

    @Test
    fun cancellingTheCallerCancelsItsQueuedOperation() = runTest(main) {
        val work = PageRenderTask()
        val entered = CompletableDeferred<Unit>()
        val events = mutableListOf<String>()
        val first = launch {
            work.run {
                entered.complete(Unit)
                events += "first"
            }
        }
        entered.await()
        first.join()
        val gate = CompletableDeferred<Unit>()
        val second = launch { work.run { gate.await(); events += "cancelled" } }
        testScheduler.runCurrent()
        second.cancel()
        gate.complete(Unit)
        second.join()
        assertEquals(listOf("first"), events)
        assertTrue(work.isIdle)
    }

    @Test
    fun backgroundWorkDoesNotCompeteWithAnActiveRequest() = runTest(main) {
        val work = PageRenderTask()
        val entered = CompletableDeferred<Unit>()
        val finish = CompletableDeferred<Unit>()
        val events = mutableListOf<String>()
        val first = launch {
            work.run {
                entered.complete(Unit)
                finish.await()
                events += "first"
            }
        }
        entered.await()
        work.runIfIdle { events += "duplicate" }
        finish.complete(Unit)
        first.join()
        work.runIfIdle { events += "background" }
        assertEquals(listOf("first", "background"), events)
    }
}
