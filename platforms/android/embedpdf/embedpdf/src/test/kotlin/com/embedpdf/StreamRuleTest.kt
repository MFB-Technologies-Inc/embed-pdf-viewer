package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.take
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Test
import uniffi.epdf_ffi.HostException
import uniffi.epdf_ffi.Modifiers
import uniffi.epdf_ffi.PointerKind
import uniffi.epdf_ffi.PointerPageHit
import uniffi.epdf_ffi.PointerPhase
import uniffi.epdf_ffi.PointerSample
import uniffi.epdf_plugin_interaction.InteractionHandle
import uniffi.epdf_plugin_interaction.InteractionHandler
import uniffi.epdf_plugin_interaction.InteractionTool

/** Throws a `HostException` on every pointer down, which the kernel reports. */
private object ThrowingHandler : InteractionHandler {
    override fun enabledFor(tool: InteractionTool): Boolean = true

    override fun onDown(sample: PointerSample): Boolean = throw HostException.Failed("refused")

    override fun onMove(sample: PointerSample) {}

    override fun onUp(sample: PointerSample) {}

    override fun onCancel(sample: PointerSample) {}

    override fun onHover(sample: PointerSample) {}

    override fun claimsTouch(sample: PointerSample): Boolean = false
}

private fun down(page: PageRef) = PointerSample(
    phase = PointerPhase.DOWN,
    viewport = Point(10.0, 10.0),
    page = PointerPageHit(page = page, point = Point(10.0, 10.0)),
    modifiers = Modifiers(),
    pointerType = PointerKind.TOUCH,
)

class StreamRuleTest {
    /** A viewer whose throwing handler reports one `HostError` per pointer down. */
    private suspend fun viewerThatReports(): Triple<PdfViewer, InteractionHandle, PageRef> {
        val viewer = started(ViewerConfig(interaction = true))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        // The interaction capability is not wrapped yet; its handle stands in.
        val interaction = viewer.call { viewer.host.interaction(document.id) }
        interaction.registerHandler("throws", 5.0, null, ThrowingHandler)
        return Triple(viewer, interaction, page)
    }

    private fun Flow<PdfException>.names(count: Int) = take(count).map { it.name }

    @Test
    fun everyReportedErrorObserverReceivesEveryError() = viewerTest {
        val (viewer, interaction, page) = viewerThatReports()
        val first = async(start = CoroutineStart.UNDISPATCHED) { viewer.observeReportedErrors().names(3).toList() }
        val second = async(start = CoroutineStart.UNDISPATCHED) { viewer.observeReportedErrors().names(3).toList() }
        repeat(3) { interaction.dispatchPointer(down(page)) }
        val a = withTimeout(5_000) { first.await() }
        assertEquals(listOf("HostError", "HostError", "HostError"), a)
        assertEquals(a, withTimeout(5_000) { second.await() })
        viewer.destroy()
    }

    @Test
    fun cancellingOneErrorObserverLeavesTheOthersLiveAndDestroyFinishesThem() = viewerTest {
        val (viewer, interaction, page) = viewerThatReports()
        val survivor = async(start = CoroutineStart.UNDISPATCHED) { viewer.observeReportedErrors().names(1).toList() }
        val cancelled = launch(start = CoroutineStart.UNDISPATCHED) { viewer.observeReportedErrors().collect {} }
        cancelled.cancel()
        cancelled.join()
        interaction.dispatchPointer(down(page))
        assertEquals(listOf("HostError"), withTimeout(5_000) { survivor.await() })
        viewer.destroy()
        assertEquals(emptyList<PdfException>(), viewer.observeReportedErrors().toList())
    }

    @Test
    fun aSlowObserverKeepsTheNewestSixteenReportedErrors() = viewerTest {
        val hub = reportedErrorHub()
        val gate = CompletableDeferred<Unit>()
        val first = CompletableDeferred<Unit>()
        val names = async(start = CoroutineStart.UNDISPATCHED) {
            hub.flow().map { it.name }.onEach {
                first.complete(Unit)
                gate.await()
            }.toList()
        }
        // E0 goes straight to the waiting collector; the rest queue behind it.
        repeat(21) { hub.send(PdfException(PdfException.Code.OPERATION_FAILED, "", name = "E$it")) }
        first.await()
        hub.finish()
        gate.complete(Unit)
        assertEquals(listOf("E0") + (5..20).map { "E$it" }, names.await())
    }
}
