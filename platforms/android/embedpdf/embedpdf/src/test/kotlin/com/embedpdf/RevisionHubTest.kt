package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.yield
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class RevisionHubTest {
    @Test
    fun aSlowCollectorKeepsOnlyTheNewestAndEveryObserverFinishes() = viewerTest {
        val hub = stateRevisionHub()
        val gate = CompletableDeferred<Unit>()
        val slow = Channel<Long>(Channel.UNLIMITED)
        launch(start = CoroutineStart.UNDISPATCHED) {
            hub.flow().collect {
                slow.send(it)
                gate.await()
            }
            slow.close()
        }
        val other = subscribed(hub.flow())
        hub.send(1)
        // The slow collector took 1 and is busy; 2 and 3 coalesce behind it.
        assertEquals(1L, slow.receive())
        hub.send(2)
        hub.send(3)
        gate.complete(Unit)
        assertEquals(3L, withTimeout(5_000) { slow.receive() })
        val cancelled = launch(start = CoroutineStart.UNDISPATCHED) { hub.flow().collect {} }
        cancelled.cancel()
        cancelled.join()
        hub.send(4)
        assertEquals(4L, withTimeout(5_000) { slow.receive() })
        hub.finish()
        assertEquals(emptyList<Long>(), slow.drain())
        // A collector that keeps up sees every revision, in order.
        assertEquals(listOf(1L, 2L, 3L, 4L), other.drain())
        assertEquals(emptyList<Long>(), hub.flow().toList())
    }

    @Test
    fun repeatedCancellationLeavesTheSurvivingObserverLive() = viewerTest {
        val hub = stateRevisionHub()
        val survivor = subscribed(hub.flow())
        for (revision in 1L..20L) {
            val disposable = launch(start = CoroutineStart.UNDISPATCHED) { hub.flow().collect {} }
            hub.send(revision)
            disposable.cancel()
            disposable.join()
            assertEquals(revision, withTimeout(5_000) { survivor.receive() })
        }
        // Each cancelled collector removed its own subscription.
        assertEquals(1, hub.subscriberCount)
        hub.finish()
        assertNull(survivor.receiveCatching().getOrNull())
    }

    @Test
    fun twoViewerCollectorsBothReceiveRevisions() = viewerTest {
        val viewer = started()
        val first = subscribed(viewer.observeStateRevisions())
        val second = subscribed(viewer.observeStateRevisions())
        yield()
        viewer.open(Fixture.bytes("report"))
        val a = withTimeout(5_000) { first.receive() }
        val b = withTimeout(5_000) { second.receive() }
        assertEquals(true, a > 0 && b > 0)
        viewer.destroy()
    }
}
