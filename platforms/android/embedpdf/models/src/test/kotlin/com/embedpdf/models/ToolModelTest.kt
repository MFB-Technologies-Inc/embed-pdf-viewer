package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.InteractionCapability
import com.embedpdf.InteractionEventToolChanged
import com.embedpdf.InteractionTool
import com.embedpdf.InteractionToolChangedEvent
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.Registration
import com.embedpdf.ToolId
import com.embedpdf.ToolPermission
import com.embedpdf.ViewerConfig
import com.embedpdf.interaction
import com.embedpdf.started
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Deferred
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import kotlinx.coroutines.yield
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class ToolModelTest {
    @get:Rule
    val main = MainThreadRule()

    private class Tools(
        val viewer: PdfViewer,
        val document: DocumentInfo,
        val interaction: InteractionCapability,
        val model: ToolModel,
        val fallback: ToolId,
        val observing: Job,
    )

    /** A `canUse` a test flips. */
    private class Gate : ToolPermission {
        @Volatile var open = false
        override fun canUse(): Boolean = open
    }

    /** A model over a viewer with the annotation tools, its `run()` running and its first snapshot read. */
    private suspend fun CoroutineScope.tools(): Tools {
        val viewer = started(ViewerConfig(interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("annotation_kinds"))
        val interaction = viewer.interaction(document)
        val model = ToolModel(interaction)
        val observing = launch { model.run() }
        val fallback = interaction.defaultToolId()
        expectObserved(model.activeToolId, fallback)
        return Tools(viewer, document, interaction, model, fallback, observing)
    }

    /** A model with no `run()`: its only reads are the ones a test causes. */
    private suspend fun unobserved(): Triple<PdfViewer, InteractionCapability, ToolModel> {
        val viewer = started(ViewerConfig(interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("annotation_kinds"))
        val interaction = viewer.interaction(document)
        return Triple(viewer, interaction, ToolModel(interaction))
    }

    private suspend fun Tools.end() {
        viewer.destroy()
        assertTrue(ends(observing))
    }

    private suspend fun kernelSnapshot(interaction: InteractionCapability): Triple<List<InteractionTool>, ToolId, Set<ToolId>> {
        val tools = interaction.tools()
        val usable = tools.map { ToolId(it.id) }.filter { interaction.canUseTool(it) }.toSet()
        return Triple(tools, interaction.activeToolId(), usable)
    }

    private suspend fun expectRegistered(id: ToolId, tools: Tools, permission: Gate): Registration {
        var registration: Registration? = null
        expectObserved({ tools.model.tools.value.any { it.id == id.value } }, true) {
            registration = tools.interaction.registerTool(
                InteractionTool(id = id.value, cursor = "crosshair", enables = emptyList()),
                permission = permission,
            )
        }
        return checkNotNull(registration)
    }

    /** Every `toolChanged` the hub sends from now until the viewer is destroyed. */
    private fun CoroutineScope.toolChanges(interaction: InteractionCapability): Deferred<List<InteractionToolChangedEvent>> =
        async(start = CoroutineStart.UNDISPATCHED) {
            val changes = mutableListOf<InteractionToolChangedEvent>()
            interaction.observeEvents().collect { if (it is InteractionEventToolChanged) changes += it.event }
            changes
        }

    @Test
    fun theFirstSnapshotListsTheRegisteredToolsAsUsable() = mainTest {
        val tools = tools()
        val kernel = tools.interaction.tools()
        assertEquals(kernel, tools.model.tools.value)
        assertTrue(kernel.any { it.id == ToolId.INK.value })
        assertEquals(kernel.map { ToolId(it.id) }.toSet(), tools.model.usable.value)
        assertNull(tools.model.error.value)
        tools.end()
    }

    @Test
    fun activateSetsTheActiveTool() = mainTest {
        val tools = tools()
        expectObserved(tools.model.activeToolId, ToolId.INK) { tools.model.activate(ToolId.INK) }
        assertNull(tools.model.error.value)
        assertEquals(ToolId.INK, tools.interaction.activeToolId())
        tools.end()
    }

    @Test
    fun aToolActivatedOnTheCapabilityReachesTheModelThroughItsEvent() = mainTest {
        val tools = tools()
        expectObserved(tools.model.activeToolId, ToolId.INK) { tools.interaction.activateTool(ToolId.INK) }
        tools.end()
    }

    @Test
    fun activateDefaultReturnsToTheDefaultTool() = mainTest {
        val tools = tools()
        expectObserved(tools.model.activeToolId, ToolId.INK) { tools.model.activate(ToolId.INK) }
        expectObserved(tools.model.activeToolId, tools.fallback) { tools.model.activateDefault() }
        assertNull(tools.model.error.value)
        tools.end()
    }

    @Test
    fun anUnregisteredToolSetsNotFoundAndLeavesTheActiveTool() = mainTest {
        val tools = tools()
        val model = tools.model
        expectObserved(model.activeToolId, ToolId.INK) { model.activate(ToolId.INK) }
        model.activate(ToolId("nobody-registered-this"))
        assertEquals(PdfException.Code.NOT_FOUND, model.error.value?.code)
        assertEquals(ToolId.INK, model.activeToolId.value)
        assertEquals(ToolId.INK, tools.interaction.activeToolId())
        tools.end()
    }

    @Test
    fun aLaterSuccessfulActivationClearsTheError() = mainTest {
        val tools = tools()
        val model = tools.model
        model.activate(ToolId("nobody-registered-this"))
        assertEquals(PdfException.Code.NOT_FOUND, model.error.value?.code)
        model.activate(ToolId.INK)
        assertNull(model.error.value)
        model.activate(ToolId("nobody-registered-this"))
        assertEquals(PdfException.Code.NOT_FOUND, model.error.value?.code)
        model.activateDefault()
        assertNull(model.error.value)
        tools.end()
    }

    @Test
    fun aBackgroundRefreshKeepsAnActionsError() = mainTest {
        val tools = tools()
        tools.model.activate(ToolId("nobody-registered-this"))
        tools.model.refresh()
        assertEquals(PdfException.Code.NOT_FOUND, tools.model.error.value?.code)
        tools.end()
    }

    @Test
    fun closingTheDocumentEndsTheRunAndEmptiesTheModel() = mainTest {
        val tools = tools()
        val model = tools.model
        expectObserved(model.activeToolId, ToolId.INK) { model.activate(ToolId.INK) }
        tools.viewer.close(tools.document)
        assertTrue(ends(tools.observing))
        assertEquals(emptyList<InteractionTool>(), model.tools.value)
        assertNull(model.activeToolId.value)
        assertEquals(emptySet<ToolId>(), model.usable.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        tools.viewer.destroy()
    }

    @Test
    fun aToolRegisteredLaterReachesTheModelWithItsPermission() = mainTest {
        val tools = tools()
        val model = tools.model
        val gate = Gate()
        val gated = ToolId("gated")
        val registration = expectRegistered(gated, tools, gate)
        assertFalse(gated in model.usable.value)
        assertTrue(model.isDisabled(gated))
        // The host's permission changes with no word from the viewer: the app refreshes.
        gate.open = true
        expectObserved({ gated in model.usable.value }, true) { model.refresh() }
        assertFalse(model.isDisabled(gated))
        expectObserved({ model.tools.value.any { it.id == gated.value } }, false) { registration.unregister() }
        assertFalse(gated in model.usable.value)
        tools.end()
    }

    @Test
    fun aButtonIsSelectedForTheActiveToolAndDisabledForAnUnusableOne() = mainTest {
        val tools = tools()
        val model = tools.model
        assertFalse(model.isSelected(ToolId.INK))
        assertFalse(model.isDisabled(ToolId.INK))
        assertTrue(model.isDisabled(ToolId("nobody-registered-this")))
        model.activate(ToolId.INK)
        assertTrue(model.isSelected(ToolId.INK))
        assertFalse(model.isSelected(ToolId.PAN))
        assertFalse(model.isDisabled(ToolId.PAN))
        tools.end()
    }

    @Test
    fun theActiveToolsButtonStaysEnabledWhenItsToolIsNoLongerUsable() = mainTest {
        val tools = tools()
        val model = tools.model
        val gate = Gate().apply { open = true }
        val gated = ToolId("gated")
        expectRegistered(gated, tools, gate)
        model.activate(gated)
        assertTrue(model.isSelected(gated))
        gate.open = false
        expectObserved({ gated in model.usable.value }, false) { model.refresh() }
        // Still the way back to the default tool.
        assertTrue(model.isSelected(gated))
        assertFalse(model.isDisabled(gated))
        model.buttonTapped(gated)
        assertEquals(tools.fallback, model.activeToolId.value)
        assertTrue(model.isDisabled(gated))
        tools.end()
    }

    @Test
    fun aSecondTapWhileAnActivationIsInFlightIsIgnored() = mainTest {
        val tools = tools()
        val model = tools.model
        val changes = toolChanges(tools.interaction)
        val first = launch { model.buttonTapped(ToolId.INK) }
        val second = launch { model.buttonTapped(ToolId.INK) }
        first.join()
        second.join()
        // Neither toggled off nor activated twice.
        assertEquals(ToolId.INK, model.activeToolId.value)
        assertFalse(model.submitting.value)
        tools.end()
        assertEquals(listOf(InteractionToolChangedEvent(ToolId.INK.value, tools.fallback.value)), changes.await())
    }

    @Test
    fun aRequestForAnotherToolWhileAnActivationIsInFlightIsIgnored() = mainTest {
        val tools = tools()
        val model = tools.model
        val changes = toolChanges(tools.interaction)
        val first = launch { model.activate(ToolId.INK) }
        val second = launch { model.activate(ToolId.PAN) }
        first.join()
        second.join()
        assertEquals(ToolId.INK, model.activeToolId.value)
        assertFalse(model.submitting.value)
        tools.end()
        assertEquals(listOf(InteractionToolChangedEvent(ToolId.INK.value, tools.fallback.value)), changes.await())
    }

    @Test
    fun everyButtonIsDisabledWhileTheHubCallIsOut() = mainTest {
        val tools = tools()
        val model = tools.model
        assertFalse(model.submitting.value)
        var duringCall = emptyList<Boolean>()
        model.hubCallStarting = {
            duringCall = listOf(model.submitting.value, model.isDisabled(ToolId.INK), model.isDisabled(ToolId.PAN))
        }
        model.activate(ToolId.INK)
        model.hubCallStarting = null
        assertEquals(listOf(true, true, true), duringCall)
        assertEquals(
            listOf(false, false, false),
            listOf(model.submitting.value, model.isDisabled(ToolId.INK), model.isDisabled(ToolId.PAN)),
        )
        assertEquals(ToolId.INK, model.activeToolId.value)
        tools.end()
    }

    /** The guard covers the hub call alone: by the time the action's read has finished, the buttons are back. */
    @Test
    fun theButtonsAreEnabledAgainBeforeTheActivationsRead() = mainTest {
        val (viewer, _, model) = unobserved()
        model.refresh()
        var duringRead = emptyList<Boolean>()
        var activeDuringRead: ToolId? = null
        model.snapshots.readFinished = {
            duringRead = listOf(
                model.submitting.value, model.isDisabled(ToolId.INK), model.isDisabled(ToolId.PAN), model.isSelected(ToolId.INK),
            )
            activeDuringRead = model.activeToolId.value
        }
        model.activate(ToolId.INK)
        model.snapshots.readFinished = null
        assertEquals(listOf(false, false, false, true), duringRead)
        assertEquals(ToolId.INK, activeDuringRead)
        viewer.destroy()
    }

    /** The action writes the active tool itself; here its read is dropped, so `tools` trails by that read. */
    @Test
    fun theActiveToolIsTheRequestedOneWhenActivateReturnsWithNoRunGoing() = mainTest {
        val (viewer, interaction, model) = unobserved()
        model.snapshots.readFinished = { model.snapshots.invalidate() }
        model.activate(ToolId.INK)
        model.snapshots.readFinished = null
        assertEquals(ToolId.INK, model.activeToolId.value)
        assertFalse(model.submitting.value)
        assertNull(model.error.value)
        assertEquals(emptyList<InteractionTool>(), model.tools.value)
        assertEquals(ToolId.INK, interaction.activeToolId())
        model.refresh()
        val snapshot = kernelSnapshot(interaction)
        assertEquals(snapshot.first, model.tools.value)
        assertEquals(snapshot.third, model.usable.value)
        assertEquals(ToolId.INK, model.activeToolId.value)
        viewer.destroy()
    }

    @Test
    fun theActiveToolIsTheDefaultWhenActivateDefaultReturnsWithNoRunGoing() = mainTest {
        val (viewer, interaction, model) = unobserved()
        val fallback = interaction.defaultToolId()
        model.activate(ToolId.INK)
        model.snapshots.readFinished = { model.snapshots.invalidate() }
        model.activateDefault()
        model.snapshots.readFinished = null
        assertEquals(fallback, model.activeToolId.value)
        assertFalse(model.submitting.value)
        viewer.destroy()
    }

    /** A read that started before the hub answered must not put the old tool back. */
    @Test
    fun aReadFromBeforeTheActivationDoesNotPutTheOldToolBack() = mainTest {
        val (viewer, _, model) = unobserved()
        model.refresh()
        val (reached, release) = holdingFirstRead(model.snapshots)
        val stale = async { model.refresh() }
        reached.await()
        model.activate(ToolId.INK)
        assertEquals(ToolId.INK, model.activeToolId.value)
        release.complete(Unit)
        stale.await()
        assertEquals(ToolId.INK, model.activeToolId.value)
        viewer.destroy()
    }

    @Test
    fun aFailedActivationEndsTheFlight() = mainTest {
        val tools = tools()
        tools.model.activate(ToolId("nobody-registered-this"))
        assertEquals(PdfException.Code.NOT_FOUND, tools.model.error.value?.code)
        assertFalse(tools.model.submitting.value)
        assertFalse(tools.model.isDisabled(ToolId.INK))
        tools.end()
    }

    @Test
    fun tappingAnotherToolActivatesItAndTappingTheActiveToolReturnsToTheDefault() = mainTest {
        val tools = tools()
        expectObserved(tools.model.activeToolId, ToolId.INK) { tools.model.buttonTapped(ToolId.INK) }
        expectObserved(tools.model.activeToolId, tools.fallback) { tools.model.buttonTapped(ToolId.INK) }
        tools.end()
    }

    @Test
    fun anActivationCompletesWhileStateRevisionsKeepArriving() = mainTest {
        val tools = tools()
        val model = tools.model
        val interaction = tools.interaction
        // Each registration and each removal is a state revision.
        var churning = true
        val churn = launch {
            val tool = InteractionTool(id = "churn", cursor = "crosshair", enables = emptyList())
            repeat(3000) {
                if (!churning) return@launch
                interaction.registerTool(tool, replace = true).unregister()
            }
            churning = false
        }
        expectObserved({ model.tools.value.any { it.id == "churn" } }, true)
        model.activate(ToolId.INK)
        val finishedDuringTheRevisions = churning
        churning = false
        churn.join()
        assertTrue(finishedDuringTheRevisions)
        assertFalse(model.submitting.value)
        assertNull(model.error.value)
        val kernel = kernelSnapshot(interaction)
        expectObserved(model.tools, kernel.first)
        expectObserved(model.usable, kernel.third)
        assertEquals(kernel.second, model.activeToolId.value)
        assertEquals(ToolId.INK, kernel.second)
        tools.end()
    }

    /** Cancels an activation at [point] and expects the model, at rest with `run()` going, to hold what the hub holds. */
    private fun expectAtRestAfterCancelling(point: suspend (Job, ToolModel) -> Unit) = mainTest {
        val tools = tools()
        val model = tools.model
        val activation = launch { model.activate(ToolId.INK) }
        point(activation, model)
        activation.join()
        model.snapshots.readFinished = null
        assertTrue(activation.isCancelled)
        assertFalse(model.submitting.value)
        val kernel = kernelSnapshot(tools.interaction)
        expectObserved(model.activeToolId, kernel.second)
        expectObserved(model.tools, kernel.first)
        expectObserved(model.usable, kernel.third)
        assertFalse(model.submitting.value)
        tools.end()
    }

    @Test
    fun anActivationCancelledBeforeTheHubCallLeavesTheModelCurrent() =
        expectAtRestAfterCancelling { activation, _ -> activation.cancel() }

    @Test
    fun anActivationCancelledDuringTheHubCallLeavesTheModelCurrent() =
        expectAtRestAfterCancelling { activation, model ->
            repeat(1000) { if (!model.submitting.value) yield() }
            activation.cancel()
        }

    /** The cancelled caller drops the action's read; the read `run()` makes for the action's event brings the rest. */
    @Test
    fun anActivationCancelledDuringItsReadLeavesTheModelCurrent() =
        expectAtRestAfterCancelling { activation, model ->
            model.snapshots.readFinished = {
                if (model.activeToolId.value == ToolId.INK) activation.cancel()
            }
        }
}
