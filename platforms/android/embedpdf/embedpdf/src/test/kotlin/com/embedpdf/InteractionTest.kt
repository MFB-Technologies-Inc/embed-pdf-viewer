package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** Records what reaches it. Captures on down unless [throwsOnDown]. */
class RecordingHandler(
    private val throwsOnDown: Boolean = false,
    private val onDownHook: (() -> Unit)? = null,
) : InteractionHandler {
    private val log = mutableListOf<String>()

    val entries: List<String> get() = synchronized(log) { log.toList() }

    override fun enabledFor(tool: InteractionTool): Boolean = true

    override fun onDown(sample: PointerSample): Boolean {
        synchronized(log) { log.add("down") }
        onDownHook?.invoke()
        if (throwsOnDown) throw IllegalStateException("refused")
        return true
    }

    override fun onMove(sample: PointerSample) {
        synchronized(log) { log.add("move") }
    }

    override fun onUp(sample: PointerSample) {
        synchronized(log) { log.add("up") }
    }

    override fun onHover(sample: PointerSample) {
        synchronized(log) { log.add("hover") }
    }
}

fun sample(phase: PointerPhase, page: PageRef, x: Double = 10.0): PointerSample = PointerSample(
    phase = phase,
    viewport = Point(x, 10.0),
    page = PointerPageHit(page = page, point = Point(x, 10.0)),
    modifiers = Modifiers(),
    pointerType = PointerKind.TOUCH,
)

class InteractionTest {
    data class Opened(
        val viewer: PdfViewer,
        val document: DocumentInfo,
        val interaction: InteractionCapability,
        val page: PageRef,
    )

    companion object {
        suspend fun opened(): Opened {
            val viewer = started(ViewerConfig(interaction = true))
            val document = viewer.open(Fixture.bytes("report"))
            val page = viewer.pages(document)[0].page
            return Opened(viewer, document, viewer.interaction(document), page)
        }
    }

    @Test
    fun activatingAToolIsObserved() = viewerTest {
        val (viewer, _, interaction, _) = opened()
        assertEquals(listOf("pointer", "pan", "zoom"), interaction.tools().map { it.id })
        val events = subscribed(interaction.observeEvents())
        interaction.activateTool(ToolId.PAN)
        val changed = events.nextMapped { it as? InteractionEventToolChanged }
        assertEquals("pan", changed.event.toolId)
        assertEquals("pointer", changed.event.previousToolId)
        assertEquals(ToolId.PAN, interaction.activeToolId())
        viewer.destroy()
    }

    @Test
    fun anUnknownToolThrowsNotFound() = viewerTest {
        val (viewer, _, interaction, _) = opened()
        expectCode(PdfException.Code.NOT_FOUND) { interaction.activateTool(ToolId("laser")) }
        viewer.destroy()
    }

    @Test
    fun aKotlinHandlerCapturesAGesture() = viewerTest {
        val (viewer, _, interaction, page) = opened()
        val handler = RecordingHandler()
        interaction.registerHandler(handler, id = "recording", priority = 5.0)
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page))
        interaction.dispatchPointer(sample(PointerPhase.MOVE, page, x = 20.0))
        interaction.dispatchPointer(sample(PointerPhase.UP, page, x = 30.0))
        // A reply after the samples means they have run.
        interaction.cursor()
        assertEquals(listOf("down", "move", "up"), handler.entries)
        viewer.destroy()
    }

    @Test
    fun aThrowingHandlerReportsAndFails() = viewerTest {
        val (viewer, _, interaction, page) = opened()
        val handler = RecordingHandler(throwsOnDown = true)
        interaction.registerHandler(handler, id = "throws", priority = 5.0)
        val errors = subscribed(viewer.observeReportedErrors())
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page))
        interaction.dispatchPointer(sample(PointerPhase.MOVE, page, x = 20.0))
        val reported = withTimeout(5_000) { errors.receive() }
        assertEquals("HostError", reported.name)
        interaction.cursor()
        // Not captured: the move hovered.
        assertEquals(listOf("down", "hover"), handler.entries)
        viewer.destroy()
    }

    @Test
    fun aHandlerThatCallsBackIntoTheViewerDoesNotDeadlock() = viewerTest {
        val (viewer, _, interaction, page) = opened()
        val answer = CompletableDeferred<String>()
        val handler = RecordingHandler(
            onDownHook = {
                launch {
                    answer.complete(runCatching { interaction.cursor() }.getOrElse { "failed" })
                }
            },
        )
        interaction.registerHandler(handler, id = "reenters", priority = 5.0)
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page))
        assertEquals("default", withTimeout(5_000) { answer.await() })
        viewer.destroy()
    }

    @Test
    fun droppingARegistrationKeepsTheTool() = viewerTest {
        val (viewer, _, interaction, _) = opened()
        interaction.registerTool(InteractionTool(id = "ink", cursor = "crosshair", enables = listOf("annotation-create")))
        assertTrue(interaction.hasTool(ToolId.INK))
        val registration = interaction.registerTool(
            InteractionTool(id = "ink", cursor = "crosshair", enables = emptyList()),
            replace = true,
        )
        registration.unregister()
        interaction.cursor()
        assertEquals(false, interaction.hasTool(ToolId.INK))
        viewer.destroy()
    }

    @Test
    fun aToolPermissionAnswersCanUseTool() = viewerTest {
        val (viewer, _, interaction, _) = opened()
        val refuse = object : ToolPermission {
            override fun canUse(): Boolean = false
        }
        interaction.registerTool(InteractionTool(id = "kotlin-refused", cursor = "default", enables = emptyList()), permission = refuse)
        interaction.registerTool(InteractionTool(id = "kotlin-open", cursor = "default", enables = emptyList()))
        assertEquals(false, interaction.canUseTool(ToolId("kotlin-refused")))
        assertEquals(true, interaction.canUseTool(ToolId("kotlin-open")))
        assertEquals(false, interaction.canUseTool(ToolId("nobody")))
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, interaction, page) = opened()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { interaction.activeToolId() }
        // Dispatching to a closed document is dropped, not a crash.
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page))
        viewer.destroy()
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page))
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, interaction, _) = opened()
        val collector = collecting(interaction.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        interaction.activateTool(ToolId.PAN)
        viewer.destroy()
    }

    @Test
    fun theFlowCompletesWhenTheDocumentCloses() = viewerTest {
        val (viewer, document, interaction, _) = opened()
        val collector = collecting(interaction.observeEvents())
        viewer.close(document)
        assertTrue(finishes(collector))
        viewer.destroy()
    }

    @Test
    fun toolIdsKeepTheirWireStrings() {
        assertEquals(37, ToolId.named.size)
        assertEquals(ToolId.named.size, ToolId.named.toSet().size)
        assertEquals("form-signature", ToolId.FORM_SIGNATURE.value)
        assertEquals("free-text-callout", ToolId.FREE_TEXT_CALLOUT.toString())
        assertEquals(ToolId.PAN, InteractionTool(id = "pan", cursor = "grab", enables = emptyList()).toolId)
    }
}
