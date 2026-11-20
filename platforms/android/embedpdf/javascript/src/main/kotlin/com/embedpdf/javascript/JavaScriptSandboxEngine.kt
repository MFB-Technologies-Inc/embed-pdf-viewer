package com.embedpdf.javascript

import android.content.Context
import android.os.Build
import androidx.annotation.RequiresApi
import androidx.javascriptengine.JavaScriptIsolate
import androidx.javascriptengine.JavaScriptSandbox
import com.embedpdf.EmbedPdfEngineIntegration
import com.embedpdf.HostExceptionFailed
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ScriptEngineReporter
import com.embedpdf.ScriptEngine
import com.embedpdf.ScriptEngineAvailability
import com.embedpdf.ScriptEngineFactory
import com.embedpdf.ScriptEvaluation
import com.embedpdf.ScriptEvaluationThrew
import com.embedpdf.ScriptEvaluationTimedOut
import com.embedpdf.ScriptEvaluationValue
import java.util.concurrent.ExecutionException
import java.util.concurrent.Future
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException
import java.lang.ref.WeakReference
import java.util.concurrent.atomic.AtomicBoolean

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
 * called, rather than failing every action. The report isn't replayed: a
 * collector subscribed after that first open misses it.
 *
 * Every factory in the process shares one connection to the sandbox service.
 * Building a factory starts connecting (a prewarm), and [close] releases the
 * factory's hold on the connection, which closes when the last factory lets
 * go. The connection completes on the main looper, so the first realm needs
 * a free main looper: a main thread blocked waiting for the viewer while a
 * document's first script runs stalls that script until the connect times out.
 * A connection that fails, or whose isolate can't be made, is closed and
 * tried once more.
 *
 * A realm that runs past its budget is closed: the call returns `timedOut`
 * and the realm is abandoned. Whether closing stops the running script
 * depends on the sandbox's isolate termination feature. A sandbox without it
 * is reported once to each viewer built with this factory, through
 * [PdfViewer.observeReportedErrors] (`unsupported`), when the first realm
 * connects: a runaway script there keeps running after its realm is
 * abandoned. Memory and stack
 * budgets aren't enforced; the sandbox's own limits apply. A result that
 * isn't a string is given back as the sandbox's string form of it.
 */
@OptIn(EmbedPdfEngineIntegration::class)
public class JavaScriptSandboxEngineFactory internal constructor(
    /** Why JavaScript can't run here, or `null` when the sandbox is there. */
    override val unavailableReason: String?,
    private val connection: SandboxConnectionHolder?,
) : ScriptEngineFactory, ScriptEngineAvailability, AutoCloseable {
    public constructor(context: Context) : this(context, platformUnavailableReason())

    private constructor(context: Context, reason: String?) : this(
        reason,
        if (reason == null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) SharedSandbox.holder(context) else null,
    )

    /** Whether the sandbox is there: API 26 or later and `JavaScriptSandbox.isSupported()`. */
    public val isAvailable: Boolean get() = unavailableReason == null

    private val closed = AtomicBoolean(false)

    private val lock = Any()

    /** The viewers built with this factory, held weakly. */
    private val reporters = mutableListOf<WeakReference<ScriptEngineReporter>>()

    /** The termination report, once a sandbox without the feature connected. */
    private var limitation: PdfException? = null

    init {
        connection?.retain()
        connection?.prewarm()
    }

    /**
     * A new realm. Throws (as a `HostError`) where the sandbox is missing,
     * after [close], or when connecting to it fails twice.
     */
    override fun create(): ScriptEngine {
        unavailableReason?.let { throw HostExceptionFailed(it) }
        if (closed.get()) throw HostExceptionFailed(CLOSED)
        val live = connection ?: throw HostExceptionFailed(UNAVAILABLE_BELOW_API_26)
        val (sandbox, isolate) = try {
            live.isolate(CONNECT_TIMEOUT_MS)
        } catch (e: Exception) {
            throw HostExceptionFailed("the JavaScript sandbox failed to start: ${readable(e)}")
        }
        if (!sandbox.terminatesIsolates) reportNoTermination()
        return IsolateScriptEngine(isolate)
    }

    /** Holds [reporter] weakly; sends it the termination report at once if there is one. */
    override fun attach(reporter: ScriptEngineReporter) {
        val known = synchronized(lock) {
            reporters.removeAll { it.get() == null }
            reporters.add(WeakReference(reporter))
            limitation
        }
        known?.let(reporter::report)
    }

    /** Reports, once per viewer, that a runaway script can't be stopped. */
    private fun reportNoTermination() {
        val (report, targets) = synchronized(lock) {
            if (limitation != null) return
            val report = PdfException(PdfException.Code.UNSUPPORTED, NO_TERMINATION, "NotSupportedError")
            limitation = report
            report to reporters.mapNotNull { it.get() }
        }
        for (target in targets) target.report(report)
    }

    /**
     * Releases this factory's hold on the shared connection. Realms already
     * made keep running until the connection closes. Idempotent.
     */
    override fun close() {
        if (closed.compareAndSet(false, true)) connection?.release()
    }

    internal companion object {
        val UNAVAILABLE_BELOW_API_26 = "PDF JavaScript needs API 26 or later; JavaScript is disabled."
        val UNSUPPORTED_SANDBOX = "This device's WebView provides no JavaScript sandbox; JavaScript is disabled."
        val CLOSED = "the JavaScript sandbox factory is closed"
        val NO_TERMINATION =
            "This device's JavaScript sandbox can't stop a running script; a script past its time budget runs on after its realm is abandoned."
        val CONNECT_TIMEOUT_MS = 10_000L

        /** The SDK gate: below 26 the sandbox classes aren't touched at all. */
        fun unavailableReason(sdk: Int, supported: () -> Boolean): String? = when {
            sdk < Build.VERSION_CODES.O -> UNAVAILABLE_BELOW_API_26
            !supported() -> UNSUPPORTED_SANDBOX
            else -> null
        }

        private fun platformUnavailableReason(): String? = unavailableReason(Build.VERSION.SDK_INT) {
            Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && sandboxSupported()
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

/** A connected sandbox, as [SandboxConnectionHolder] uses it. */
internal interface ScriptSandbox {
    /** Whether closing an isolate stops a script running in it. */
    val terminatesIsolates: Boolean

    fun createIsolate(): ScriptIsolate

    fun close()
}

/**
 * One connection to the sandbox service, shared by the factories that
 * [retain] it and closed when the last one calls [release]. [connect] starts a
 * connection; the holder makes at most one at a time.
 */
internal class SandboxConnectionHolder(private val connect: () -> Future<ScriptSandbox>) {
    private val lock = Any()
    private var references = 0
    private var current: Future<ScriptSandbox>? = null

    fun retain() {
        synchronized(lock) { references++ }
    }

    /** The last release closes the connection; the next retain connects again. */
    fun release() {
        val closing = synchronized(lock) {
            if (references == 0) return
            references--
            if (references > 0) return
            current.also { current = null }
        }
        closing?.let(::closeQuietly)
    }

    /** Starts connecting now, when nothing is connected or connecting. */
    fun prewarm() {
        synchronized(lock) { connection() }
    }

    /**
     * A new isolate and the sandbox it lives in. When waiting for the
     * connection or making the isolate throws, the connection is closed and a
     * new one tried, once.
     */
    fun isolate(timeoutMs: Long): Pair<ScriptSandbox, ScriptIsolate> {
        var failure: Exception? = null
        repeat(2) {
            val future = synchronized(lock) { connection() }
            try {
                val sandbox = awaitConnection(future, timeoutMs)
                return sandbox to sandbox.createIsolate()
            } catch (e: Exception) {
                failure = e
                discard(future)
            }
        }
        throw failure!!
    }

    /** The connection, started when there is none. Hold [lock]. */
    private fun connection(): Future<ScriptSandbox> = current ?: connect().also { current = it }

    /** Closes [failed] and forgets it, when it is still the current connection. */
    private fun discard(failed: Future<ScriptSandbox>) {
        val forgotten = synchronized(lock) {
            if (current !== failed) return
            current = null
            true
        }
        if (forgotten) closeQuietly(failed)
    }

    private fun awaitConnection(future: Future<ScriptSandbox>, timeoutMs: Long): ScriptSandbox =
        try {
            future.get(timeoutMs, TimeUnit.MILLISECONDS)
        } catch (e: ExecutionException) {
            throw e.cause as? Exception ?: e
        }

    private fun closeQuietly(future: Future<ScriptSandbox>) {
        if (!future.isDone) {
            future.cancel(true)
            return
        }
        try {
            future.get().close()
        } catch (e: Exception) {
            // A connection that failed or already closed has nothing to close.
        }
    }
}

/** The process-wide connection every [JavaScriptSandboxEngineFactory] shares. */
@RequiresApi(Build.VERSION_CODES.O)
private object SharedSandbox {
    private var holder: SandboxConnectionHolder? = null

    @Synchronized
    fun holder(context: Context): SandboxConnectionHolder {
        // The application context lives as long as the process.
        val application = context.applicationContext ?: context
        return holder ?: SandboxConnectionHolder { PlatformSandbox.connect(application) }.also { holder = it }
    }
}

@RequiresApi(Build.VERSION_CODES.O)
private class PlatformSandbox(private val sandbox: JavaScriptSandbox) : ScriptSandbox {
    override val terminatesIsolates: Boolean
        get() = sandbox.isFeatureSupported(JavaScriptSandbox.JS_FEATURE_ISOLATE_TERMINATION)

    override fun createIsolate(): ScriptIsolate = SandboxIsolate(sandbox.createIsolate())

    override fun close() {
        sandbox.close()
    }

    companion object {
        fun connect(context: Context): Future<ScriptSandbox> =
            MappedFuture(JavaScriptSandbox.createConnectedInstanceAsync(context), ::PlatformSandbox)
    }
}

/** [source] with [map] applied to its value. */
private class MappedFuture<A, B>(private val source: Future<A>, private val map: (A) -> B) : Future<B> {
    override fun cancel(mayInterruptIfRunning: Boolean): Boolean = source.cancel(mayInterruptIfRunning)

    override fun isCancelled(): Boolean = source.isCancelled

    override fun isDone(): Boolean = source.isDone

    override fun get(): B = map(source.get())

    override fun get(timeout: Long, unit: TimeUnit): B = map(source.get(timeout, unit))
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
