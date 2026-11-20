package com.embedpdf.javascript

import com.embedpdf.EmbedPdfEngineIntegration
import com.embedpdf.HostExceptionFailed
import com.embedpdf.PdfException
import com.embedpdf.ScriptEngineReporter
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import java.util.concurrent.CompletableFuture
import java.util.concurrent.Future

/**
 * The connection every factory shares, through fake sandboxes: Robolectric
 * has no sandbox service.
 */
@OptIn(EmbedPdfEngineIntegration::class)
class SandboxConnectionTest {
    /** A sandbox whose isolates come from [isolate], counting closes. */
    internal class FakeSandbox(
        override val terminatesIsolates: Boolean = true,
        private val isolate: () -> ScriptIsolate = { FakeIsolate { CompletableFuture.completedFuture("1") } },
    ) : ScriptSandbox {
        var closes = 0

        override fun createIsolate(): ScriptIsolate = isolate()

        override fun close() {
            closes++
        }
    }

    /** Answers each connect with the next of [answers], counting connects. */
    internal class FakeService(vararg answers: () -> Future<ScriptSandbox>) {
        private val queue = ArrayDeque(answers.toList())
        var connects = 0
        val holder = SandboxConnectionHolder {
            connects++
            queue.removeFirst()()
        }
    }

    private fun ready(sandbox: ScriptSandbox): () -> Future<ScriptSandbox> = { CompletableFuture.completedFuture(sandbox) }

    private fun factory(service: FakeService) = JavaScriptSandboxEngineFactory(null, service.holder)

    @Test
    fun buildingAFactoryStartsConnecting() {
        val service = FakeService(ready(FakeSandbox()))
        factory(service)
        assertEquals(1, service.connects)
    }

    @Test
    fun factoriesShareOneConnection() {
        val service = FakeService(ready(FakeSandbox()))
        val first = factory(service)
        val second = factory(service)
        first.create()
        second.create()
        second.create()
        assertEquals(1, service.connects)
    }

    @Test
    fun theLastFactoryToCloseClosesTheConnection() {
        val sandbox = FakeSandbox()
        val service = FakeService(ready(sandbox), ready(FakeSandbox()))
        val first = factory(service)
        val second = factory(service)
        first.create()
        first.close()
        first.close()
        assertEquals(0, sandbox.closes)
        second.close()
        assertEquals(1, sandbox.closes)
        // A factory built afterwards connects again.
        factory(service).create()
        assertEquals(2, service.connects)
    }

    @Test
    fun aClosedFactoryMakesNoRealms() {
        val service = FakeService(ready(FakeSandbox()))
        val factory = factory(service)
        factory.close()
        try {
            factory.create()
            fail("a closed factory made a realm")
        } catch (e: HostExceptionFailed) {
            assertTrue(e.detail, e.detail.contains(JavaScriptSandboxEngineFactory.CLOSED))
        }
    }

    @Test
    fun aFailedConnectIsClearedAndTriedOnceMore() {
        val failed: () -> Future<ScriptSandbox> = {
            CompletableFuture<ScriptSandbox>().apply { completeExceptionally(IllegalStateException("bind failed")) }
        }
        val service = FakeService(failed, ready(FakeSandbox()))
        factory(service).create()
        assertEquals(2, service.connects)
    }

    @Test
    fun anIsolateThatCantBeMadeClosesTheConnectionAndRetriesOnce() {
        val broken = FakeSandbox(isolate = { throw IllegalStateException("sandbox died") })
        val service = FakeService(ready(broken), ready(FakeSandbox()))
        factory(service).create()
        assertEquals(1, broken.closes)
        assertEquals(2, service.connects)
    }

    @Test
    fun twoFailuresInARowFailTheRealm() {
        val first = FakeSandbox(isolate = { throw IllegalStateException("sandbox died") })
        val second = FakeSandbox(isolate = { throw IllegalStateException("sandbox died again") })
        val service = FakeService(ready(first), ready(second))
        try {
            factory(service).create()
            fail("a realm without an isolate")
        } catch (e: HostExceptionFailed) {
            assertTrue(e.detail, e.detail.contains("sandbox died again"))
        }
        assertEquals(1, first.closes)
        assertEquals(1, second.closes)
    }

    @Test
    fun aSandboxThatCantStopScriptsIsReportedOncePerViewer() {
        val service = FakeService(ready(FakeSandbox(terminatesIsolates = false)))
        val factory = factory(service)
        val early = mutableListOf<PdfException>()
        val late = mutableListOf<PdfException>()
        val earlyReporter = ScriptEngineReporter { early.add(it) }
        val lateReporter = ScriptEngineReporter { late.add(it) }
        factory.attach(earlyReporter)
        factory.create()
        factory.create()
        assertEquals(listOf(JavaScriptSandboxEngineFactory.NO_TERMINATION), early.map { it.detail })
        assertEquals(PdfException.Code.UNSUPPORTED, early.single().code)
        // A viewer built after the sandbox connected hears at once.
        factory.attach(lateReporter)
        assertEquals(listOf(JavaScriptSandboxEngineFactory.NO_TERMINATION), late.map { it.detail })
        factory.create()
        assertEquals(1, early.size)
        assertEquals(1, late.size)
    }

    @Test
    fun aSandboxThatStopsScriptsReportsNothing() {
        val service = FakeService(ready(FakeSandbox()))
        val factory = factory(service)
        val reports = mutableListOf<PdfException>()
        val reporter = ScriptEngineReporter { reports.add(it) }
        factory.attach(reporter)
        factory.create()
        assertEquals(emptyList<PdfException>(), reports)
    }
}
