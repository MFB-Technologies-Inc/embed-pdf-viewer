package com.embedpdf.javascript

import com.embedpdf.HostExceptionFailed
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
class SandboxConnectionTest {
    /** A sandbox whose isolates come from [isolate], counting closes. */
    internal class FakeSandbox(private val isolate: () -> ScriptIsolate = { FakeIsolate { CompletableFuture.completedFuture("1") } }) :
        ScriptSandbox {
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
        val broken = FakeSandbox { throw IllegalStateException("sandbox died") }
        val service = FakeService(ready(broken), ready(FakeSandbox()))
        factory(service).create()
        assertEquals(1, broken.closes)
        assertEquals(2, service.connects)
    }

    @Test
    fun twoFailuresInARowFailTheRealm() {
        val first = FakeSandbox { throw IllegalStateException("sandbox died") }
        val second = FakeSandbox { throw IllegalStateException("sandbox died again") }
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
}
