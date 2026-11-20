package com.embedpdf

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import uniffi.epdf_plugin_actions.checkScriptEngine

/** One JavaScript realm. Called on the viewer's kernel thread, which waits for each answer. */
public typealias ScriptEngine = uniffi.epdf_plugin_actions.ScriptEngine

/**
 * Makes one realm per document (and per detached realm). The
 * `embedpdf-javascript` artifact's `JavaScriptSandboxEngineFactory` is one.
 */
public typealias ScriptEngineFactory = uniffi.epdf_plugin_actions.ScriptEngineFactory
public typealias ScriptEvaluation = uniffi.epdf_plugin_actions.ScriptEvaluation
public typealias ScriptEvaluationValue = uniffi.epdf_plugin_actions.ScriptEvaluation.Value
public typealias ScriptEvaluationThrew = uniffi.epdf_plugin_actions.ScriptEvaluation.Threw
public typealias ScriptEvaluationTimedOut = uniffi.epdf_plugin_actions.ScriptEvaluation.TimedOut
public typealias ScriptCheck = uniffi.epdf_plugin_actions.ScriptCheck

/** A host's check that its script engine works. */
public object ScriptEngineCheck {
    /**
     * Runs [source] once in a fresh realm from [factory], outside any
     * document, with the default budget, on [Dispatchers.IO].
     */
    public suspend fun run(factory: ScriptEngineFactory, source: String): ScriptCheck = withContext(Dispatchers.IO) {
        try {
            checkScriptEngine(factory, source)
        } catch (e: Exception) {
            throw PdfException.from(e)
        }
    }
}

/**
 * Marks the hooks a script engine module uses to talk to [PdfViewer]. An app
 * never needs them; an engine module opts in with
 * `@OptIn(EmbedPdfEngineIntegration::class)`.
 */
@RequiresOptIn(
    message = "This hook is for script engine modules, not apps.",
    level = RequiresOptIn.Level.ERROR,
)
@Retention(AnnotationRetention.BINARY)
@Target(AnnotationTarget.CLASS)
public annotation class EmbedPdfEngineIntegration

/**
 * A [ScriptEngineFactory] that can say it doesn't work on this device, and
 * can report trouble found later.
 *
 * A viewer whose config enables JavaScript with a factory whose
 * [unavailableReason] isn't `null` runs with JavaScript disabled, and reports
 * the reason once through [PdfViewer.observeReportedErrors] (`unsupported`).
 */
@EmbedPdfEngineIntegration
public interface ScriptEngineAvailability {
    /** Why scripts can't run on this device, or `null` when they can. Read once, when a viewer is built. */
    public val unavailableReason: String?

    /**
     * Called when a viewer is built with this factory. [reporter] sends to that
     * viewer's [PdfViewer.observeReportedErrors]. The viewer holds [reporter]
     * strongly, so a factory that keeps it weakly lets the viewer go.
     */
    public fun attach(reporter: ScriptEngineReporter) {}
}

/**
 * Reports to one viewer's [PdfViewer.observeReportedErrors]. A report sent
 * before the viewer's first document opens (or [PdfViewer.actions] is first
 * called) is held until then, so a collector subscribed before that receives
 * it. Callable from any thread.
 */
@EmbedPdfEngineIntegration
public fun interface ScriptEngineReporter {
    public fun report(error: PdfException)
}

/** Holds reports until [release], then sends them and every later one to [hub]. */
@OptIn(EmbedPdfEngineIntegration::class)
internal class HeldReports(private val hub: BroadcastHub<PdfException>) : ScriptEngineReporter {
    private val lock = Any()
    private val held = mutableListOf<PdfException>()
    private var released = false

    override fun report(error: PdfException) {
        synchronized(lock) {
            if (!released) {
                held.add(error)
                return
            }
        }
        hub.send(error)
    }

    fun release() {
        val pending = synchronized(lock) {
            if (released) return
            released = true
            held.toList().also { held.clear() }
        }
        for (error in pending) hub.send(error)
    }
}
