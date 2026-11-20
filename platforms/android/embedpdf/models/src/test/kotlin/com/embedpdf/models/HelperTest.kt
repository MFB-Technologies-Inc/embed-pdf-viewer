package com.embedpdf.models

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.yield
import kotlinx.coroutines.Dispatchers
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/** `SnapshotRefreshTests` and `SerialCallsTests`, on a virtual-time main dispatcher. */
@OptIn(ExperimentalCoroutinesApi::class)
class HelperTest {
    private val main = StandardTestDispatcher()

    @Before
    fun setMain() = Dispatchers.setMain(main)

    @After
    fun resetMain() = Dispatchers.resetMain()

    @Test
    fun aSlowerReadCannotOverwriteANewerSnapshot() = runTest(main) {
        val refresh = SnapshotRefresh()
        val applied = mutableListOf<String>()
        val started = CompletableDeferred<Unit>()
        val gate = CompletableDeferred<Unit>()
        val slow = launch {
            refresh.run(
                load = {
                    started.complete(Unit)
                    gate.await()
                    "old"
                },
                apply = { applied += it },
                failure = { throw AssertionError("unexpected read failure") },
            )
        }
        started.await()
        refresh.run(load = { "new" }, apply = { applied += it }, failure = { throw AssertionError("unexpected read failure") })
        gate.complete(Unit)
        slow.join()
        assertEquals(listOf("new"), applied)
    }

    @Test
    fun invalidationDiscardsAPendingFailure() = runTest(main) {
        val refresh = SnapshotRefresh()
        var failures = 0
        val started = CompletableDeferred<Unit>()
        val gate = CompletableDeferred<Unit>()
        val slow = launch {
            refresh.run<String>(
                load = {
                    started.complete(Unit)
                    gate.await()
                    throw IllegalStateException("failure")
                },
                apply = { throw AssertionError("unexpected snapshot") },
                failure = { failures++ },
            )
        }
        started.await()
        refresh.invalidate()
        gate.complete(Unit)
        slow.join()
        assertEquals(0, failures)
    }

    @Test
    fun aCancelledReadAppliesNothing() = runTest(main) {
        val refresh = SnapshotRefresh()
        var applied = false
        val gate = CompletableDeferred<Unit>()
        val reading = launch {
            refresh.run(load = { gate.await(); 1 }, apply = { applied = true }, failure = {})
        }
        yield()
        reading.cancel()
        gate.complete(Unit)
        reading.join()
        assertTrue(!applied)
    }

    @Test
    fun callsRunInTheOrderEnqueued() = runTest(main) {
        val calls = SerialCalls()
        val order = mutableListOf<Int>()
        // An earlier call suspends for longer, so calls that only shared the
        // main thread would finish in reverse.
        for ((number, yields) in listOf(1 to 8, 2 to 4, 3 to 0)) {
            calls.enqueue {
                repeat(yields) { yield() }
                order += number
            }
        }
        calls.enqueue {}.await()
        assertEquals(listOf(1, 2, 3), order)
    }

    @Test
    fun aCallReturnsItsResult() = runTest(main) {
        val calls = SerialCalls()
        calls.enqueue { yield() }
        assertEquals(7, calls.enqueue { 7 }.await())
    }

    @Test
    fun aContinuousDrainRunsCallsAsTheyArrive() = runTest(main) {
        val calls = SerialCalls()
        val drainer = launch { calls.drainContinuously() }
        var ran = false
        calls.enqueue { ran = true }
        testScheduler.runCurrent()
        assertTrue(ran)
        drainer.cancel()
    }

    @Test
    fun aCancelledCallLetsTheNextOneRun() = runTest(main) {
        val calls = SerialCalls()
        val gate = CompletableDeferred<Unit>()
        val first = calls.enqueue { gate.await() }
        val second = calls.enqueue { 2 }
        val waiting = launch { second.await() }
        testScheduler.runCurrent()
        first.cancel()
        waiting.join()
        assertEquals(2, second.await())
    }
}
