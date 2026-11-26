package com.embedpdf.models

import com.embedpdf.Fixture
import com.embedpdf.started
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class ViewerModelTest {
    @get:Rule
    val main = MainThreadRule()

    @Test
    fun aNewModelHoldsNoDocument() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        assertNull(model.activeDocument.value)
        assertEquals(0, model.documentCount.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun opensAndClosesReachTheObservingModel() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val observing = launch { model.run() }
        var firstId: String? = null
        expectObserved(model.documentCount, 1) { firstId = viewer.open(Fixture.bytes("report")).id }
        // The count can reach 1 while the tab still holds its `pending:`
        // ticket; the real id lands with a later revision.
        expectObserved({ model.activeDocument.value?.id }, firstId)
        val second = viewer.open(Fixture.bytes("features"))
        expectObserved({ model.activeDocument.value?.id }, second.id)
        assertEquals(2, model.documentCount.value)
        expectObserved(model.documentCount, 1) { viewer.close(second) }
        assertEquals(viewer.documentCount(), model.documentCount.value)
        observing.cancel()
        observing.join()
        viewer.destroy()
    }

    @Test
    fun destroyingTheViewerEndsTheRunAndEmptiesTheModel() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val observing = launch { model.run() }
        viewer.open(Fixture.bytes("report"))
        expectObserved(model.documentCount, 1)
        viewer.destroy()
        assertTrue(ends(observing))
        assertEquals(0, model.documentCount.value)
        assertNull(model.activeDocument.value)
    }

    @Test
    fun aRefreshNotifiesCollectorsOfEachProperty() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val document = viewer.open(Fixture.bytes("report"))
        assertTrue(changes(model.documentCount) { model.refresh() })
        assertTrue(
            changes(model.activeDocument) {
                viewer.close(document)
                model.refresh()
            },
        )
        viewer.destroy()
    }

    /** Review focus: a cancelled run leaves nothing running, and the model stops changing. */
    @Test
    fun aCancelledRunLeavesNothingRunningAndTheModelStopsChanging() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val observing = launch { model.run() }
        expectObserved(model.documentCount, 1) { viewer.open(Fixture.bytes("report")) }
        observing.cancel()
        observing.join()
        assertTrue(observing.children.none())
        viewer.open(Fixture.bytes("features"))
        kotlinx.coroutines.delay(200)
        assertEquals(1, model.documentCount.value)
        viewer.destroy()
    }

    /** Review focus: a run cancelled while its refresh is out applies nothing. */
    @Test
    fun aRunCancelledDuringARefreshAppliesNothing() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val (reached, release) = holdingFirstRead(model.snapshots)
        val observing = launch(start = CoroutineStart.UNDISPATCHED) { model.run() }
        reached.await()
        viewer.open(Fixture.bytes("report"))
        observing.cancel()
        release.complete(Unit)
        observing.join()
        kotlinx.coroutines.delay(200)
        assertEquals(0, model.documentCount.value)
        assertNull(model.activeDocument.value)
        viewer.destroy()
    }

    @Test
    fun anOlderReadCannotOverwriteANewerOne() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val (reached, release) = holdingFirstRead(model.snapshots)
        val stale = async { model.refresh() }
        reached.await()
        val document = viewer.open(Fixture.bytes("report"))
        model.refresh()
        assertEquals(1, model.documentCount.value)
        release.complete(Unit)
        stale.await()
        assertEquals(1, model.documentCount.value)
        assertEquals(document.id, model.activeDocument.value?.id)
        viewer.destroy()
    }

    @Test
    fun aSecondRunTakesOverFromTheFirst() = mainTest {
        val viewer = started()
        val model = ViewerModel(viewer)
        val first = launch { model.run() }
        val second = launch { model.run() }
        expectObserved(model.documentCount, 1) { viewer.open(Fixture.bytes("report")) }
        // The first run leaves at its next revision.
        viewer.open(Fixture.bytes("features"))
        assertTrue(ends(first))
        assertFalse(second.isCompleted)
        second.cancel()
        viewer.destroy()
    }
}
