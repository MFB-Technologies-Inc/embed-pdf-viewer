package com.embedpdf.javascript

import android.content.Context
import android.os.Build
import androidx.annotation.RequiresApi
import androidx.javascriptengine.JavaScriptIsolate
import androidx.javascriptengine.JavaScriptSandbox
import com.embedpdf.EmbedPdfEngineIntegration
import com.embedpdf.HostExceptionFailed
import com.embedpdf.PdfViewer
import com.embedpdf.ScriptEngine
import com.embedpdf.ScriptEngineAvailability
import com.embedpdf.ScriptEngineFactory
import com.embedpdf.ScriptEvaluation
import com.embedpdf.ScriptEvaluationThrew
import com.embedpdf.ScriptEvaluationTimedOut
import com.embedpdf.ScriptEvaluationValue
import com.google.common.util.concurrent.ListenableFuture
import java.util.concurrent.ExecutionException
import java.util.concurrent.Future
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException

/**
 * PDF JavaScript in the platform's JavaScript sandbox (`androidx.javascriptengine`),
 * one isolate per realm, from the `embedpdf-javascript` artifact. Pass it as
 * `ActionsJavaScriptConfig(enabled = true, engine = JavaScriptSandboxEngineFactory(context))`.
 *
 * The sandbox needs API 26 and a WebView that provides it. Where it is
 * missing ([isAvailable] is `false`), a viewer built with this factory runs
 * with JavaScript disabled instead, and reports once through
 * [PdfViewer.observeReportedErrors] (`unsupported`, [unavailableReason] as the
 * detail) when its first document opens or [PdfViewer.actions] is first
 * called, rather than failing every action.
 *
 * The first realm connects to the sandbox service, which blocks the kernel
 * thread for the connection; later realms reuse the connection. A realm that
 * runs past its budget is closed: the call returns `timedOut` and the realm
 * is abandoned. Whether closing stops the running script depends on the
 * sandbox's isolate termination feature. Memory and stack budgets aren't
 * enforced; the sandbox's own limits apply. A result that isn't a string is
 * given back as the sandbox's string form of it.
 */
@OptIn(EmbedPdfEngineIntegration::class)
public class JavaScriptSandboxEngineFactory(context: Context) : ScriptEngineFactory, ScriptEngineAvailability {
    private val context: Context = context.applicationContext ?: context

    /** Why JavaScript can't run here, or `null` when the sandbox is there. */
    override val unavailableReason: String? = unavailableReason(Build.VERSION.SDK_INT) {
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && sandboxSupported()
    }

    /** Whether the sandbox is there: API 26 or later and `JavaScriptSandbox.isSupported()`. */
    public val isAvailable: Boolean get() = unavailableReason == null

    private val lock = Any()
    private var connection: SandboxConnection? = null

    /**
     * A new realm. Throws (as a `HostError`) where the sandbox is missing, or
     * when connecting to it fails.
     */
    override fun create(): ScriptEngine {
        unavailableReason?.let { throw HostExceptionFailed(it) }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) throw HostExceptionFailed(UNAVAILABLE_BELOW_API_26)
        val live = synchronized(lock) { connection ?: SandboxConnection(context).also { connection = it } }
        return try {
            IsolateScriptEngine(live.isolate())
        } catch (e: Exception) {
            throw HostExceptionFailed("the JavaScript sandbox failed to start: ${readable(e)}")
        }
    }

    internal companion object {
        val UNAVAILABLE_BELOW_API_26 = "PDF JavaScript needs API 26 or later; JavaScript is disabled."
        val UNSUPPORTED_SANDBOX = "This device's WebView provides no JavaScript sandbox; JavaScript is disabled."

        /** The SDK gate: below 26 the sandbox classes aren't touched at all. */
        fun unavailableReason(sdk: Int, supported: () -> Boolean): String? = when {
            sdk < Build.VERSION_CODES.O -> UNAVAILABLE_BELOW_API_26
            !supported() -> UNSUPPORTED_SANDBOX
            else -> null
        }

        @RequiresApi(Build.VERSION_CODES.O)
        private fun sandboxSupported(): Boolean = try {
            JavaScriptSandbox.isSupported()
        } catch (e: Throwable) {
            // No WebView package, or one too old to answer.
            false
        }
    }
}

/** A realm's isolate, as [IsolateScriptEngine] uses it. */
internal interface ScriptIsolate {
    fun evaluate(program: String): Future<String>

    /** Idempotent. */
    fun close()
}

/** The one connection to the sandbox service, made when the first realm needs it. */
@RequiresApi(Build.VERSION_CODES.O)
private class SandboxConnection(private val context: Context) {
    private val lock = Any()
    private var sandbox: ListenableFuture<JavaScriptSandbox>? = null

    fun isolate(): ScriptIsolate {
        val future = synchronized(lock) {
            sandbox ?: JavaScriptSandbox.createConnectedInstanceAsync(context).also { sandbox = it }
        }
        val connected = try {
            future.get(CONNECT_TIMEOUT_SECONDS, TimeUnit.SECONDS)
        } catch (e: Exception) {
            // The next realm tries again.
            synchronized(lock) { if (sandbox === future) sandbox = null }
            throw (e as? ExecutionException)?.cause as? Exception ?: e
        }
        return SandboxIsolate(connected.createIsolate())
    }

    private companion object {
        const val CONNECT_TIMEOUT_SECONDS = 10L
    }
}

@RequiresApi(Build.VERSION_CODES.O)
private class SandboxIsolate(private val isolate: JavaScriptIsolate) : ScriptIsolate {
    override fun evaluate(program: String): Future<String> = isolate.evaluateJavaScriptAsync(program)

    override fun close() {
        isolate.close()
    }
}

/**
 * The [ScriptEngine] contract over an isolate: a value, a throw, or a timeout
 * that abandons the realm.
 */
internal class IsolateScriptEngine(private val isolate: ScriptIsolate) : ScriptEngine {
    private val lock = Any()
    private var abandoned = false

    override fun evaluate(program: String, timeLimitMs: UInt): ScriptEvaluation {
        if (synchronized(lock) { abandoned }) return ScriptEvaluationThrew("the realm was abandoned after a timeout")
        val answer = try {
            isolate.evaluate(program)
        } catch (e: Exception) {
            return ScriptEvaluationThrew(readable(e))
        }
        return try {
            ScriptEvaluationValue(answer.get(timeLimitMs.toLong(), TimeUnit.MILLISECONDS))
        } catch (e: TimeoutException) {
            abandon()
            answer.cancel(true)
            ScriptEvaluationTimedOut
        } catch (e: ExecutionException) {
            ScriptEvaluationThrew(readable(e.cause ?: e))
        } catch (e: InterruptedException) {
            Thread.currentThread().interrupt()
            ScriptEvaluationThrew("interrupted")
        } catch (e: Exception) {
            ScriptEvaluationThrew(readable(e))
        }
    }

    override fun dispose() {
        abandon()
    }

    private fun abandon() {
        val first = synchronized(lock) {
            val first = !abandoned
            abandoned = true
            first
        }
        if (first) {
            try {
                isolate.close()
            } catch (e: Exception) {
                // Closing a dead isolate fails; it is gone either way.
            }
        }
    }
}

/** A message for an error: its message, or its class name. */
internal fun readable(error: Throwable): String = error.message ?: error.toString()
