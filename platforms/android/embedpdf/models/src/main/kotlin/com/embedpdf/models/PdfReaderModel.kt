package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.DocumentStatus
import com.embedpdf.PageInfo
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.RenderConfig
import com.embedpdf.SearchConfig
import com.embedpdf.StageConfig
import com.embedpdf.StageVisiblePage
import com.embedpdf.ViewerConfigBuilder
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.render
import com.embedpdf.search
import com.embedpdf.selection
import com.embedpdf.stage
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.net.URL

/**
 * A reader in one model: it starts a viewer with the plugins its [features]
 * need and those [configure] adds, opens one document at a time and builds
 * the models a reader view shows.
 *
 * ```kotlin
 * val model = PdfReaderModel()
 * model.open(bytes, name = "report.pdf")
 * ```
 *
 * [open], [unlock] and [close] run one at a time, in the order they are
 * called, and none of them throws a [PdfException]: a failure is
 * [Phase.Failed]. A call made while an earlier open is still loading
 * supersedes it. The earlier open then returns without touching [phase], and
 * [phase] follows the later call. A caller cancelled while its call runs
 * sees its cancellation, and [phase] then follows whatever call comes next.
 *
 * The model owns its viewer. [close] destroys it and returns when the engine
 * has shut down, unless an open made while it waited superseded it. A model
 * dropped without [close] leaves the viewer to its own `close()` at garbage
 * collection; call [close] when the reader goes away.
 *
 * Each session's [StageReaderModel], and its [TextSelectionModel] when it
 * has one, wants its `run()` running while a view shows it.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself.
 */
public class PdfReaderModel(
    /** The features the model was made with. */
    public val features: Set<Feature> = Feature.entries.toSet(),
    /**
     * Changes the features' viewer configuration before a viewer starts. It
     * gets the builder the model has prepared, with the render and stage
     * plugins and the plugins [features] need, and returns the builder to
     * start the viewer with: `PdfReaderModel(configure = { it.history().outline() })`.
     * Called on the main thread once for each viewer the model starts. A
     * `configure` that turns off a plugin a feature needs makes every open
     * end in [Phase.Failed] with that capability's `unsupported` error.
     */
    private val configure: (ViewerConfigBuilder) -> ViewerConfigBuilder = { it },
) {
    /** What the reader offers beyond showing and scrolling pages. Each installs the plugins it needs. */
    public enum class Feature {
        /** Text search: [Session.search]. */
        SEARCH,

        /** Text selection: [Session.selection]. */
        SELECTION,

        /** The interaction tools, the annotation tools included: [Session.tools]. */
        TOOLS,

        /** Links: a tap on one activates it, and the outcome arrives on the reader's `linkActivations`. */
        LINKS,
    }

    /** Where the reader stands. */
    public sealed interface Phase {
        /** Nothing is open: before the first open and after [close]. */
        public data object Idle : Phase

        /** An open is under way. */
        public data object Loading : Phase

        /** The document needs a password; [unlock] takes it. */
        public data class Locked(val document: DocumentInfo) : Phase

        public data class Ready(val session: Session) : Phase

        /** The open, or what followed it, failed. Nothing is left open, and the next open starts again. */
        public data class Failed(val error: PdfException) : Phase
    }

    /**
     * One open document and the models over it. A session belongs to that
     * document: the next open, and [close], replace it. Its models stop
     * working when the document closes, so read the session from [phase]
     * each time instead of keeping one.
     */
    public class Session internal constructor(
        public val viewer: PdfViewer,
        public val document: DocumentInfo,
        /** The document's pages as they were when it opened. */
        public val pages: List<PageInfo>,
        public val reader: StageReaderModel,
        /** `null` without [Feature.SEARCH]. */
        public val search: SearchModel?,
        /** `null` without [Feature.SELECTION]. */
        public val selection: TextSelectionModel?,
        /** `null` without [Feature.TOOLS]. */
        public val tools: ToolModel?,
    ) {
        /** The [PageInfo] of a page the stage shows, or `null` for a page added after the document opened. */
        public fun pageInfo(visiblePage: StageVisiblePage): PageInfo? {
            val index = visiblePage.pageIndex.toLong()
            if (index < pages.size && pages[index.toInt()].page == visiblePage.page) return pages[index.toInt()]
            // A page edit moved the page since the document opened.
            //
            // Performance: this scans every page once per call; a map by
            // `PageRef` built with the session makes it constant.
            return pages.firstOrNull { it.page == visiblePage.page }
        }
    }

    private val _phase = MutableStateFlow<Phase>(Phase.Idle)
    private val _unlockError = MutableStateFlow<PdfException?>(null)

    public val phase: StateFlow<Phase> = _phase.asStateFlow()

    /**
     * Why the last [unlock] left the document locked: a wrong password is
     * `permission-denied`. `null` once an unlock succeeds and after the next
     * open or [close].
     */
    public val unlockError: StateFlow<PdfException?> = _unlockError.asStateFlow()

    /** The running viewer: made by the first open, destroyed by [close]. */
    internal var viewer: PdfViewer? = null
        private set

    /** Orders the opens, the unlocks and the close. */
    private val calls = SerialCalls()

    /** Bumped by each open and each close. A call that finds it changed after a suspension leaves [phase] alone. */
    private var generation = 0

    /** The newest open's or close's call, cancelled when the next supersedes it. */
    private var newest: SerialCalls.Call<Unit>? = null

    private val needsInteraction: Boolean
        get() = features.any { it == Feature.SELECTION || it == Feature.TOOLS || it == Feature.LINKS }

    /** A builder with the plugins the features need, over the render and stage plugins every reader has. */
    internal fun builder(): ViewerConfigBuilder {
        var builder = ViewerConfigBuilder().render(RenderConfig()).stage(StageConfig())
        if (needsInteraction) builder = builder.interaction()
        if (Feature.SEARCH in features) builder = builder.search(SearchConfig())
        if (Feature.SELECTION in features) builder = builder.selection()
        if (Feature.TOOLS in features) builder = builder.annotation()
        if (Feature.LINKS in features) builder = builder.link()
        return builder
    }

    private sealed interface Source {
        class Bytes(val bytes: ByteArray, val name: String?, val password: String?) : Source

        class Address(val url: URL, val name: String?) : Source
    }

    /**
     * Opens PDF bytes, closing the document that was open. Ends in
     * [Phase.Ready], in [Phase.Locked] for a protected document opened with
     * no password or a wrong one, or in [Phase.Failed], as for bytes that are
     * not a PDF.
     */
    public suspend fun open(bytes: ByteArray, name: String? = null, password: String? = null): Unit =
        load(Source.Bytes(bytes, name, password))

    /** Opens the PDF at [url], closing the document that was open. A download that fails ends in [Phase.Failed]. */
    public suspend fun open(url: URL, name: String? = null): Unit = load(Source.Address(url, name))

    /**
     * Unlocks the document [Phase.Locked] holds. A wrong password leaves it
     * locked with the reason in [unlockError]; any other failure ends in
     * [Phase.Failed]. Does nothing unless [phase] is [Phase.Locked].
     */
    public suspend fun unlock(password: String): Unit = onMain {
        if (_phase.value !is Phase.Locked) return@onMain
        val mine = generation
        awaitQuietly(calls.enqueue { applyUnlock(password, mine) })
    }

    /**
     * Closes the document and destroys the viewer. [phase] is [Phase.Idle] at
     * once, and the engine has shut down when this returns unless an open
     * made before the close ran superseded it.
     */
    public suspend fun close(): Unit = onMain {
        val mine = supersede()
        _phase.value = Phase.Idle
        enqueueNewest { applyClose(mine) }
    }

    private suspend fun load(source: Source): Unit = onMain {
        val mine = supersede()
        // Drops the session before its document closes, so no view draws from a closed document.
        _phase.value = Phase.Loading
        enqueueNewest { applyOpen(source, mine) }
    }

    /** Makes the calls before this one stale and returns this one's generation. */
    private fun supersede(): Int {
        generation += 1
        newest?.cancel()
        _unlockError.value = null
        return generation
    }

    private suspend fun enqueueNewest(work: suspend () -> Unit) {
        val call = calls.enqueue(work)
        newest = call
        awaitQuietly(call)
    }

    /** Awaits [call]; a call a later one cancelled returns normally to a caller that is still active. */
    private suspend fun awaitQuietly(call: SerialCalls.Call<Unit>) {
        try {
            call.await()
        } catch (e: CancellationException) {
            currentCoroutineContext().ensureActive()
        }
    }

    private suspend fun applyOpen(source: Source, mine: Int) {
        if (generation != mine) return
        try {
            val viewer = startedViewer()
            // The document open before, and a failed tab an earlier open left.
            viewer.closeAll()
            if (generation != mine) return
            val document = when (source) {
                is Source.Bytes -> viewer.open(source.bytes, source.name, source.password)
                is Source.Address -> viewer.open(source.url, source.name)
            }
            // The call that superseded this one closes what it opened.
            if (generation != mine) return
            settle(document, viewer, mine)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            fail(controlError(e), mine)
        }
    }

    private suspend fun applyUnlock(password: String, mine: Int) {
        // An earlier unlock may have opened the document already.
        val locked = _phase.value as? Phase.Locked ?: return
        val viewer = viewer ?: return
        if (generation != mine) return
        _unlockError.value = null
        try {
            viewer.unlock(locked.document, password)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            // A wrong password leaves the tab locked, ready for another try.
            if (e.code != PdfException.Code.PERMISSION_DENIED) {
                fail(e, mine)
            } else if (generation == mine) {
                _unlockError.value = e
            }
            return
        }
        if (generation != mine) return
        try {
            val unlocked = viewer.document(locked.document.id)
            if (unlocked == null) {
                fail(PdfException(PdfException.Code.NOT_FOUND, "the unlocked document is no longer open"), mine)
                return
            }
            if (generation != mine) return
            settle(unlocked, viewer, mine)
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            fail(e, mine)
        }
    }

    private suspend fun applyClose(mine: Int) {
        val viewer = viewer ?: return
        if (generation != mine) return
        this.viewer = null
        // Closing has no way to report a failure, so one is dropped.
        try {
            viewer.destroy()
        } catch (e: PdfException) {
            // Already destroyed.
        } finally {
            viewer.close()
        }
    }

    /** The viewer, made and started on first use. */
    private suspend fun startedViewer(): PdfViewer {
        viewer?.let { return it }
        val viewer = PdfViewer(configure(builder()).build())
        try {
            viewer.start()
        } catch (e: Throwable) {
            viewer.close()
            throw e
        }
        this.viewer = viewer
        return viewer
    }

    /** Moves to the phase [document]'s status calls for. */
    private suspend fun settle(document: DocumentInfo, viewer: PdfViewer, mine: Int) {
        when (document.status) {
            DocumentStatus.LOCKED -> _phase.value = Phase.Locked(document)
            DocumentStatus.READY -> {
                val session = session(document, viewer)
                if (generation == mine) _phase.value = Phase.Ready(session)
            }
            else -> throw PdfException(
                document.error?.code?.let { PdfException.Code(it) } ?: PdfException.Code.OPERATION_FAILED,
                document.error?.message ?: "the document did not finish opening",
            )
        }
    }

    private suspend fun session(document: DocumentInfo, viewer: PdfViewer): Session {
        // Performance: each accessor below is its own trip to the engine, made
        // one after another: up to eight per open with every feature on.
        val stage = viewer.stage(document)
        val render = viewer.render(document)
        val interaction = if (needsInteraction) viewer.interaction(document) else null
        val link = if (Feature.LINKS in features) viewer.link(document) else null
        if (Feature.TOOLS in features) {
            // Lets the built-in pan tool drag this reader, for as long as the document.
            stage.registerScrollHandler()
        }
        val search = if (Feature.SEARCH in features) SearchModel(viewer.search(document)) else null
        val selection = if (Feature.SELECTION in features) TextSelectionModel(viewer.selection(document)) else null
        // Performance: every page's `PageInfo` crosses the bridge here, at
        // each open and after each unlock: a payload that grows with the page
        // count.
        val pages = viewer.pages(document)
        return Session(
            viewer = viewer,
            document = document,
            pages = pages,
            reader = StageReaderModel(stage, render, interaction, link),
            search = search,
            selection = selection,
            tools = if (Feature.TOOLS in features) interaction?.let { ToolModel(it) } else null,
        )
    }

    /** Ends in [Phase.Failed] with nothing left open, unless a later call has taken over. */
    private suspend fun fail(error: PdfException, mine: Int) {
        if (generation != mine) return
        // A close that fails changes nothing: the next open closes every tab again.
        try {
            viewer?.closeAll()
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            // Dropped.
        }
        if (generation != mine) return
        _phase.value = Phase.Failed(error)
    }
}
