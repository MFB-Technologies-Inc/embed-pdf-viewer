package com.embedpdf.javascript

import android.util.Log
import androidx.javascriptengine.JavaScriptSandbox
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.embedpdf.ActionNodeStatus
import com.embedpdf.ActionResultStatus
import com.embedpdf.ActionsEventExecuted
import com.embedpdf.ActionsConfig
import com.embedpdf.ActionsJavaScriptConfig
import com.embedpdf.DeviceFixture
import com.embedpdf.FormConfig
import com.embedpdf.FormValidation
import com.embedpdf.FormValueInputValue
import com.embedpdf.OpenSequence
import com.embedpdf.PdfViewer
import com.embedpdf.ScriptErrorKind
import com.embedpdf.ScriptEngineCheck
import com.embedpdf.ScriptEvaluationTimedOut
import com.embedpdf.ScriptEvaluationValue
import com.embedpdf.ViewerConfig
import com.embedpdf.actions
import com.embedpdf.forCurrentTimeZone
import com.embedpdf.form
import com.embedpdf.onDevice
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.filterIsInstance
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.withTimeout
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.TimeUnit

/**
 * The platform's JavaScript sandbox, which Robolectric doesn't have: the JVM
 * tests cover only the unavailable path and a fake isolate.
 */
@RunWith(AndroidJUnit4::class)
class DeviceSandboxTest {
    private val context = InstrumentationRegistry.getInstrumentation().targetContext
    private val factories = mutableListOf<JavaScriptSandboxEngineFactory>()

    private fun factory(): JavaScriptSandboxEngineFactory = JavaScriptSandboxEngineFactory(context).also { factories += it }

    @After
    fun closeFactories() {
        factories.forEach { it.close() }
    }

    @Test
    fun theSandboxIsSupportedAndAScriptEvaluates() = onDevice {
        val factory = factory()
        assertTrue("unavailable: ${factory.unavailableReason}", factory.isAvailable)
        val check = ScriptEngineCheck.run(factory, "app.alert('hi')")
        assertEquals(listOf("hi"), check.alerts)
        assertNull(check.error)
        assertFalse(check.disposed)
        val engine = factory.create()
        assertEquals(ScriptEvaluationValue("3"), engine.evaluate("String(1 + 2)", 5_000u))
        engine.dispose()
    }

    /**
     * A runaway script times out within its budget, and the factory makes a
     * working realm afterwards. Whether closing the realm stopped the script
     * is the sandbox's isolate termination feature, recorded here.
     */
    @Test
    fun aRunawayScriptIsStoppedAndTheFactoryStillWorks() = onDevice {
        val sandbox = JavaScriptSandbox.createConnectedInstanceAsync(context).get(10, TimeUnit.SECONDS)
        val terminates = sandbox.isFeatureSupported(JavaScriptSandbox.JS_FEATURE_ISOLATE_TERMINATION)
        sandbox.close()
        Log.i(TAG, "JS_FEATURE_ISOLATE_TERMINATION supported: $terminates")
        assertTrue("this sandbox can't terminate an isolate", terminates)

        val factory = factory()
        val engine = factory.create()
        val started = System.nanoTime()
        assertEquals(ScriptEvaluationTimedOut, engine.evaluate("while (true) {}", 200u))
        val tookMs = (System.nanoTime() - started) / 1_000_000
        Log.i(TAG, "the runaway script timed out after $tookMs ms")
        assertTrue("took $tookMs ms", tookMs < 2_000)

        // Through the check too, with the default budget.
        val check = ScriptEngineCheck.run(factory, "while (true) {}")
        assertEquals(ScriptErrorKind.BUDGET, check.error?.kind)
        assertTrue(check.disposed)

        // The factory and its connection still work: several fresh realms in a row.
        repeat(3) {
            val fresh = factory.create()
            assertEquals(ScriptEvaluationValue("ok"), fresh.evaluate("'ok'", 5_000u))
            fresh.dispose()
        }
        assertEquals(listOf("after"), ScriptEngineCheck.run(factory, "app.alert('after')").alerts)
    }

    @Test
    fun twoFactoriesShareOneConnection() = onDevice {
        val first = factory()
        val second = factory()
        val field = JavaScriptSandboxEngineFactory::class.java.getDeclaredField("connection").apply { isAccessible = true }
        assertSame(field.get(first), field.get(second))
        assertEquals(listOf("one"), ScriptEngineCheck.run(first, "app.alert('one')").alerts)
        assertEquals(listOf("two"), ScriptEngineCheck.run(second, "app.alert('two')").alerts)
        // Closing one keeps the connection for the other.
        first.close()
        assertEquals(listOf("still"), ScriptEngineCheck.run(second, "app.alert('still')").alerts)
    }

    /**
     * `action_buttons_form.pdf`'s `btn-chain`, pressed through the form and
     * dispatched by the actions plugin: JavaScript appending "A" to the log
     * field, a ResetForm of `alpha`, then JavaScript appending "B". All three
     * nodes run in the sandbox, the reset puts back `alpha` alone, and the log
     * field ends as "AB", as the TypeScript plugin's e2e
     * (`packages/plugin/form/test/action-buttons.integration.test.ts`) expects.
     */
    @Test
    fun aFormButtonsJavaScriptRunsThroughTheActionsPlugin() = onDevice {
        val viewer = PdfViewer(
            ViewerConfig(
                interaction = true,
                annotation = "",
                form = FormConfig(validation = FormValidation.NONE),
                actions = ActionsConfig.forCurrentTimeZone(
                    openSequence = OpenSequence.OFF,
                    javascript = ActionsJavaScriptConfig(enabled = true, engine = factory()),
                ),
            ),
        )
        viewer.start()
        val document = viewer.open(DeviceFixture.bytes("action_buttons_form"))
        val actions = viewer.actions(document)
        assertTrue(actions.isScriptingEnabled())
        val form = viewer.form(document)
        form.refresh()
        val fields = form.fields().associateBy { it.name }
        val log = fields.getValue("log").fieldRef
        val alpha = fields.getValue("alpha").fieldRef
        val beta = fields.getValue("beta").fieldRef
        val button = fields.getValue("btn-chain").widgets.first().annotationRef!!
        assertEquals(FormValueInputValue("filled-a"), form.value(alpha))

        val executed = async(start = CoroutineStart.UNDISPATCHED) {
            actions.observeEvents().filterIsInstance<ActionsEventExecuted>().first()
        }
        form.activate(button)
        val result = withTimeout(10_000) { executed.await() }.event.result
        assertEquals(ActionResultStatus.EXECUTED, result.status)
        assertEquals(
            listOf("javascript" to ActionNodeStatus.EXECUTED, "reset-form" to ActionNodeStatus.EXECUTED, "javascript" to ActionNodeStatus.EXECUTED),
            result.nodes.map { it.nodeType to it.status },
        )
        assertEquals(FormValueInputValue("default-a"), form.value(alpha))
        assertEquals(FormValueInputValue("filled-b"), form.value(beta))
        assertEquals(FormValueInputValue("AB"), form.value(log))
        viewer.destroy()
    }

    /** Apple's `FormTests` case: `document_actions.pdf`'s will-close script runs with JavaScript on. */
    @Test
    fun theWillCloseScriptRunsThroughActionsCapability() = onDevice {
        val viewer = PdfViewer(
            ViewerConfig(
                interaction = true,
                annotation = "",
                form = FormConfig(),
                actions = ActionsConfig.forCurrentTimeZone(
                    openSequence = OpenSequence.OFF,
                    javascript = ActionsJavaScriptConfig(enabled = true, engine = factory()),
                ),
            ),
        )
        viewer.start()
        val document = viewer.open(DeviceFixture.bytes("document_actions"))
        val result = viewer.actions(document).prepareClose()
        assertEquals(listOf(ActionNodeStatus.EXECUTED), result.steps.first().result.nodes.map { it.status })
        viewer.destroy()
    }

    private companion object {
        const val TAG = "EmbedPdfDevice"
    }
}
