package com.embedpdf

import kotlinx.coroutines.delay
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The facade's side of `ScriptEngineAvailability`, with a fake engine module:
 * the viewer disables JavaScript for an engine that says it can't run, and
 * every report waits for the first open, since the error hub doesn't replay.
 */
@OptIn(EmbedPdfEngineIntegration::class)
class ScriptEngineAvailabilityTest {
    private class FakeEngine(override val unavailableReason: String?) : ScriptEngineFactory, ScriptEngineAvailability {
        var reporter: ScriptEngineReporter? = null

        override fun create(): ScriptEngine = throw HostExceptionFailed("no realms here")

        override fun attach(reporter: ScriptEngineReporter) {
            this.reporter = reporter
        }
    }

    private fun config(engine: FakeEngine) = ViewerConfig(
        actions = ActionsConfig(
            openSequence = OpenSequence.OFF,
            javascript = ActionsJavaScriptConfig(enabled = true, engine = engine),
        ),
    )

    @Test
    fun anUnavailableEngineIsReportedAtTheFirstOpenAndDisablesScripts() = viewerTest {
        val engine = FakeEngine("no sandbox")
        val viewer = started(config(engine))
        val errors = subscribed(viewer.observeReportedErrors())
        val document = viewer.open(Fixture.bytes("document_actions"))
        val reported = withTimeout(5_000) { errors.receive() }
        assertEquals(PdfException.Code.UNSUPPORTED, reported.code)
        assertEquals("no sandbox", reported.detail)
        assertFalse(viewer.actions(document).isScriptingEnabled())
        viewer.destroy()
    }

    @Test
    fun aReportBeforeTheFirstOpenIsHeldAndALaterOneIsSentAtOnce() = viewerTest {
        val engine = FakeEngine(null)
        val viewer = started(config(engine))
        val reporter = engine.reporter!!
        reporter.report(PdfException(PdfException.Code.UNSUPPORTED, "held", "NotSupportedError"))
        // Subscribed after the report: the hold is what lets it arrive.
        val errors = subscribed(viewer.observeReportedErrors())
        delay(50)
        assertTrue("sent before the first open", errors.tryReceive().isFailure)
        viewer.open(Fixture.bytes("report"))
        assertEquals("held", withTimeout(5_000) { errors.receive() }.detail)
        reporter.report(PdfException(PdfException.Code.UNSUPPORTED, "later", "NotSupportedError"))
        assertEquals("later", withTimeout(5_000) { errors.receive() }.detail)
        viewer.destroy()
    }
}
