package com.embedpdf.javascript

import com.embedpdf.EmbedPdfEngineIntegration
import com.embedpdf.HostExceptionFailed
import com.embedpdf.PdfException
import com.embedpdf.ScriptEngineReporter
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import java.util.concurrent.CompletableFuture
import java.util.concurrent.Future
import java.util.concurrent.TimeoutException

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
    fun aDeadSandboxClosesTheConnectionAndRetriesOnce() {
        val broken = FakeSandbox(isolate = { throw DeadSandboxException("sandbox died") })
        val service = FakeService(ready(broken), ready(FakeSandbox()))
        factory(service).create()
        assertEquals(1, broken.closes)
        assertEquals(2, service.connects)
    }

    @Test
    fun twoFailuresInARowFailTheRealm() {
        val first = FakeSandbox(isolate = { throw DeadSandboxException("sandbox died") })
        val second = FakeSandbox(isolate = { throw DeadSandboxException("sandbox died again") })
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

    /** An isolate the live sandbox refuses fails that realm alone: the connection stays for the next factory's. */
    @Test
    fun anIsolateFailureOfALiveSandboxKeepsTheConnection() {
        var refusals = 1
        val sandbox = FakeSandbox(isolate = {
            if (refusals-- > 0) throw IllegalArgumentException("too many isolates")
            FakeIsolate { CompletableFuture.completedFuture("1") }
        })
        val service = FakeService(ready(sandbox), ready(FakeSandbox()))
        val first = factory(service)
        val second = factory(service)
        try {
            first.create()
            fail("a realm without an isolate")
        } catch (e: HostExceptionFailed) {
            assertTrue(e.detail, e.detail.contains("too many isolates"))
        }
        second.create()
        assertEquals(0, sandbox.closes)
        assertEquals(1, service.connects)
    }

    /** The connect's own timeout is the only one: no second wait, and the pending connect is kept. */
    @Test
    fun aConnectThatTimesOutIsNotRetried() {
        val pending = CompletableFuture<ScriptSandbox>()
        val service = FakeService({ pending })
        service.holder.retain()
        try {
            service.holder.isolate(timeoutMs = 20)
            fail("a pending connect gave an isolate")
        } catch (e: TimeoutException) {
            // Expected.
        }
        assertEquals(1, service.connects)
        assertFalse(pending.isCancelled)
        pending.complete(FakeSandbox())
        service.holder.isolate(timeoutMs = 20)
        assertEquals(1, service.connects)
    }

    /** A create that passed its factory's check after the last release doesn't connect again. */
    @Test
    fun aReleasedConnectionRefusesToConnect() {
        val service = FakeService(ready(FakeSandbox()), ready(FakeSandbox()))
        val first = factory(service)
        val second = factory(service)
        first.close()
        second.close()
        try {
            service.holder.isolate(timeoutMs = 20)
            fail("a released connection connected")
        } catch (e: ClosedConnectionException) {
            assertEquals(JavaScriptSandboxEngineFactory.CLOSED, e.message)
        }
        assertEquals(1, service.connects)
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
