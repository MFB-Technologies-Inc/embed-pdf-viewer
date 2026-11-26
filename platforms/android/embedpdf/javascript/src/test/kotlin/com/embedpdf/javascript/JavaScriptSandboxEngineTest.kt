package com.embedpdf.javascript

import android.content.Context
import android.content.ContextWrapper
import com.embedpdf.ActionContext
import com.embedpdf.ActionOrigin
import com.embedpdf.ActionSourceApi
import com.embedpdf.ActionTreeSourceDocument
import com.embedpdf.ActionTriggerEventActivate
import com.embedpdf.ActionsConfig
import com.embedpdf.ActionsJavaScriptConfig
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.Fixture
import com.embedpdf.OpenSequence
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ScriptEngineCheck
import com.embedpdf.ScriptEvaluationThrew
import com.embedpdf.ScriptEvaluationTimedOut
import com.embedpdf.ScriptEvaluationValue
import com.embedpdf.ViewerConfig
import com.embedpdf.actions
import com.embedpdf.expectCode
import com.embedpdf.subscribed
import com.embedpdf.viewerTest

import kotlinx.coroutines.delay
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.util.concurrent.CompletableFuture
import java.util.concurrent.ExecutionException
import java.util.concurrent.Future

/**
 * Robolectric has no WebView, so the sandbox is never there: these cover the
 * unavailable path through a real viewer, and the [ScriptEngine] contract
 * through a fake isolate.
 */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class JavaScriptSandboxEngineTest {
    private val factory = JavaScriptSandboxEngineFactory(RuntimeEnvironment.getApplication())
    private val user = ActionContext(origin = ActionOrigin.USER, source = ActionSourceApi, event = ActionTriggerEventActivate)

    @Test
    fun withoutAWebViewTheSandboxIsUnavailable() {
        assertFalse(factory.isAvailable)
        assertEquals(JavaScriptSandboxEngineFactory.UNSUPPORTED_SANDBOX, factory.unavailableReason)
    }

    @Test
    fun theSdkGateComesBeforeTheSandboxIsAsked() {
        var asked = false
        val reason = JavaScriptSandboxEngineFactory.unavailableReason(25) { asked = true; true }
        assertEquals(JavaScriptSandboxEngineFactory.UNAVAILABLE_BELOW_API_26, reason)
        assertFalse("the sandbox was asked below API 26", asked)
        assertNull(JavaScriptSandboxEngineFactory.unavailableReason(26) { true })
        assertEquals(JavaScriptSandboxEngineFactory.UNSUPPORTED_SANDBOX, JavaScriptSandboxEngineFactory.unavailableReason(35) { false })
    }

    @Test
    fun anUnavailableFactoryFailsTheCheck() = viewerTest {
        val error = expectCode(PdfException.Code.OPERATION_FAILED) { ScriptEngineCheck.run(factory, "app.alert('hi')") }
        assertTrue("${error.detail}", error.detail.contains("JavaScript is disabled"))
    }

    @Test
    fun actionsDegradeToJavaScriptDisabledAndReportOnce() = viewerTest {
        val viewer = PdfViewer(
            ViewerConfig(
                actions = ActionsConfig(
                    openSequence = OpenSequence.OFF,
                    javascript = ActionsJavaScriptConfig(enabled = true, engine = factory),
                ),
            ),
        )
        viewer.start()
        val errors = subscribed(viewer.observeReportedErrors())
        val document = viewer.open(Fixture.bytes("document_actions"))
        val reported = withTimeout(5_000) { errors.receive() }
        assertEquals(PdfException.Code.UNSUPPORTED, reported.code)
        assertEquals(JavaScriptSandboxEngineFactory.UNSUPPORTED_SANDBOX, reported.detail)
        val actions = viewer.actions(document)
        assertFalse(actions.isScriptingEnabled())
        val open = actions.actionTree(ActionTreeSourceDocument(event = null))!!
        repeat(3) { actions.execute(open, user) }
        actions.prepareClose()
        viewer.open(Fixture.bytes("document_actions"))
        delay(100)
        assertTrue("a second report", errors.tryReceive().isFailure)
        viewer.destroy()
    }

    @Test
    fun aValueComesBack() {
        val engine = IsolateScriptEngine(FakeIsolate { CompletableFuture.completedFuture("\"ok\"") })
        assertEquals(ScriptEvaluationValue("\"ok\""), engine.evaluate("JSON.stringify('ok')", 50u))
    }

    @Test
    fun aThrowIsThrewAndKeepsTheRealm() {
        val isolate = FakeIsolate { program ->
            if (program == "boom") {
                CompletableFuture<String>().apply { completeExceptionally(IllegalStateException("Uncaught Error: boom")) }
            } else {
                CompletableFuture.completedFuture("1")
            }
        }
        val engine = IsolateScriptEngine(isolate)
        assertEquals(ScriptEvaluationThrew("Uncaught Error: boom"), engine.evaluate("boom", 50u))
        assertEquals(ScriptEvaluationValue("1"), engine.evaluate("1", 50u))
        assertEquals(0, isolate.closes)
    }

    @Test
    fun aRunawayScriptTimesOutAndAbandonsTheRealm() {
        val isolate = FakeIsolate { CompletableFuture() }
        val engine = IsolateScriptEngine(isolate)
        val started = System.nanoTime()
        assertEquals(ScriptEvaluationTimedOut, engine.evaluate("while (true) {}", 50u))
        assertTrue((System.nanoTime() - started) / 1_000_000 < 2_000)
        assertEquals(1, isolate.closes)
        assertEquals(ScriptEvaluationThrew("the realm was abandoned after a timeout"), engine.evaluate("1", 50u))
        assertEquals(1, isolate.evaluations)
    }

    @Test
    fun anIsolateThatRefusesTheProgramIsThrew() {
        val engine = IsolateScriptEngine(FakeIsolate { throw IllegalStateException("isolate closed") })
        assertEquals(ScriptEvaluationThrew("isolate closed"), engine.evaluate("1", 50u))
    }

    @Test
    fun disposeClosesTheIsolateOnce() {
        val isolate = FakeIsolate { CompletableFuture.completedFuture("1") }
        val engine = IsolateScriptEngine(isolate)
        engine.dispose()
        engine.dispose()
        assertEquals(1, isolate.closes)
        assertTrue(engine.evaluate("1", 50u) is ScriptEvaluationThrew)
    }

    @Test
    fun anExecutionExceptionReportsItsCause() {
        val failed = object : Future<String> by CompletableFuture() {
            override fun get(timeout: Long, unit: java.util.concurrent.TimeUnit): String =
                throw ExecutionException(RuntimeException("RangeError: Maximum call stack size exceeded"))
        }
        val engine = IsolateScriptEngine(FakeIsolate { failed })
        assertEquals(ScriptEvaluationThrew("RangeError: Maximum call stack size exceeded"), engine.evaluate("f()", 50u))
    }

    /** The shared connection never keeps an activity, or any context that isn't the application's. */
    @Test
    fun theSharedConnectionKeepsOnlyTheApplicationContext() {
        val application = RuntimeEnvironment.getApplication()
        assertTrue(applicationContextOf(application) === application)
        assertTrue(applicationContextOf(ContextWrapper(application)) === application)
        val orphan = object : ContextWrapper(null) {
            override fun getApplicationContext(): Context? = null
        }
        try {
            applicationContextOf(orphan)
            fail("a context without an application context was kept")
        } catch (e: IllegalArgumentException) {
            // Expected.
        }
    }
}

/** An isolate answering each program with [answer]. */
internal class FakeIsolate(private val answer: (String) -> Future<String>) : ScriptIsolate {
    var evaluations = 0
    var closes = 0

    override fun evaluate(program: String): Future<String> {
        evaluations++
        return answer(program)
    }

    override fun close() {
        closes++
    }
}
