package com.embedpdf

import android.net.Uri
import kotlinx.coroutines.flow.Flow
import uniffi.epdf_viewer_host.DocumentEventListener
import uniffi.epdf_viewer_host.OpenDocumentOptions
import uniffi.epdf_viewer_host.ViewerHost
import java.net.URL
import java.util.concurrent.atomic.AtomicBoolean

/**
 * A viewer: the kernel, its plugins and the documents open in it.
 *
 * Every call is a `suspend` function that throws [PdfException], or the
 * caller's own `CancellationException` when its coroutine is cancelled. Call
 * [start] before anything else and [destroy] when done; [close] is the
 * blocking, best-effort release for `use {}`.
 *
 * @param documentFetcher the transport for [open] with a URL or a `Uri`;
 *   [HttpDocumentFetcher] by default, [ContentResolverDocumentFetcher] for
 *   `content:` sources. A fetcher that
 *   throws fails that open and is reported on [observeReportedErrors]; it
 *   never reaches the kernel's thread.
 * @throws PdfException when the config is refused.
 */
public class PdfViewer(
    config: ViewerConfig,
    documentFetcher: DocumentFetcher = HttpDocumentFetcher(),
) : AutoCloseable {
    private val revisionHub = stateRevisionHub()
    private val errorHub = reportedErrorHub()
    internal val registry: StreamRegistry = StreamRegistry()
    private val released = AtomicBoolean(false)

    /** Whether [close] released the native handle. */
    internal val isReleased: Boolean get() = released.get()

    /**
     * Why PDF JavaScript is off although the config enables it: its
     * [JavaScriptSandboxEngineFactory] has no sandbox on this device. Reported
     * once, by [reportUnavailableJavaScript].
     */
    private val javaScriptUnavailable: String? = config.actions?.javascript
        ?.takeIf { it.enabled }
        ?.let { (it.engine as? JavaScriptSandboxEngineFactory)?.unavailableReason }
    private val javaScriptReported = AtomicBoolean(false)

    internal val host: ViewerHost = try {
        ViewerHost.newWithFetcher(
            withoutUnavailableJavaScript(config),
            StateRelay(revisionHub, errorHub),
            GuardedFetcher(documentFetcher, errorHub),
        )
    } catch (e: Exception) {
        throw PdfException.from(e)
    }

    /** `PdfViewer(config, documentFetcher)` for a config described by a builder. */
    public constructor(builder: ViewerConfigBuilder, documentFetcher: DocumentFetcher = HttpDocumentFetcher()) :
        this(builder.build(), documentFetcher)

    // ── streams ──

    /**
     * Revisions of the viewer's state. Each collection is its own subscription,
     * installed when collection starts: read an initial snapshot after it
     * starts, and each revision invalidates that snapshot. Revisions coalesce,
     * cancellation affects this collector only, and the flow completes when the
     * viewer is destroyed.
     */
    public fun observeStateRevisions(): Flow<Long> = revisionHub.flow()

    /**
     * The errors the kernel reports rather than throws: a listener that failed,
     * a foreign handler that threw (`HostError`), a [DocumentFetcher] that
     * threw. Each collection is its own subscription, installed when collection
     * starts, and sees only errors reported after that, so start collecting
     * before the call that might report one. Each collector keeps the newest
     * 16, cancellation affects this collector only, and the flow completes when
     * the viewer is destroyed.
     */
    public fun observeReportedErrors(): Flow<PdfException> = errorHub.flow()

    /**
     * Opens, closes, locks and failures of documents, in order. Each collection
     * subscribes when it starts and completes when the viewer is destroyed.
     */
    public fun observeDocumentEvents(): Flow<DocumentEvent> = registry.stream { channel ->
        host.subscribeDocuments(
            object : DocumentEventListener {
                override fun onEvent(event: DocumentEvent) {
                    channel.trySend(event)
                }
            },
        )
    }

    // ── lifecycle ──

    public suspend fun start() {
        call { host.start() }
    }

    /**
     * Destroys the kernel and its plugins and completes every flow. A second
     * call throws `instance-closed`, as does every call after it. Cancelling
     * the caller still tears the viewer down.
     */
    public suspend fun destroy() {
        var alreadyClosed = false
        try {
            call { host.destroyViewer() }
        } catch (e: PdfException) {
            // Already destroyed or closed: the teardown already ran.
            alreadyClosed = e.code == PdfException.Code.INSTANCE_CLOSED
            throw e
        } finally {
            // Also when the caller is cancelled: the viewer is torn down
            // either way, and its flows complete.
            if (!alreadyClosed) tearDown()
        }
    }

    private fun tearDown() {
        host.shutdown()
        finishStreams()
    }

    /**
     * Stops the kernel thread without waiting for plugins to tear down, completes
     * every flow and releases the native handle. Safe to call more than once and
     * after [destroy]; every later call throws `instance-closed`.
     */
    override fun close() {
        if (!released.compareAndSet(false, true)) return
        host.shutdown()
        finishStreams()
        host.close()
    }

    // ── opening ──

    /**
     * Bytes that are not a PDF fail the open (`operation-failed`, "failed to
     * open base document") and leave a `FAILED` tab. A wrong [password] does
     * not fail it: the document opens `LOCKED`, and its `Locked` event says a
     * password was given.
     */
    public suspend fun open(
        bytes: ByteArray,
        name: String? = null,
        password: String? = null,
        activate: Boolean = true,
    ): DocumentInfo = measured("open") {
        call { host.openDocument(bytes, OpenDocumentOptions(name, password, activate)) }
    }.also { reportUnavailableJavaScript() }

    /**
     * Opens a URL through the kernel's loading tab using the viewer's
     * [DocumentFetcher]. Cancelling the calling coroutine closes the tab and
     * cancels its transport before the cancellation propagates, also when the
     * document finished opening as the cancel arrived. HTTP failures
     * and non-PDF responses leave a failed tab that [retry] can reopen.
     */
    public suspend fun open(
        url: URL,
        name: String? = null,
        password: String? = null,
        activate: Boolean = true,
    ): DocumentInfo = openUrl(url.toString(), name, password, activate)

    /**
     * [open] with a URL, for an Android [Uri]. A `content:` Uri needs a
     * [ContentResolverDocumentFetcher] as the viewer's fetcher; the default
     * fetcher fails it.
     */
    public suspend fun open(
        uri: Uri,
        name: String? = null,
        password: String? = null,
        activate: Boolean = true,
    ): DocumentInfo = openUrl(uri.toString(), name, password, activate)

    private suspend fun openUrl(url: String, name: String?, password: String?, activate: Boolean): DocumentInfo =
        measured("open") {
            callCancellable(closeDiscarded) { token ->
                host.openUrl(url, OpenDocumentOptions(name, password, activate), token)
            }
        }.also { reportUnavailableJavaScript() }

    /**
     * Restores a `saveLayer` artifact over its original PDF base. Cancelling the
     * calling coroutine closes its loading tab. The engine validates the
     * artifact and base.
     */
    public suspend fun open(
        base: ByteArray,
        layer: ByteArray,
        name: String? = null,
        password: String? = null,
        activate: Boolean = true,
    ): DocumentInfo = measured("open") {
        callCancellable(closeDiscarded) { token ->
            host.openLayerDocument(base, layer, OpenDocumentOptions(name, password, activate), token)
        }
    }.also { reportUnavailableJavaScript() }

    /**
     * Retries a failed source in its tab position without activating it. Use
     * the returned ready identity: the engine may replace the pending tab id.
     * Close its loading tab to cancel; coroutine cancellation does not cancel a
     * retry.
     */
    public suspend fun retry(document: DocumentInfo): DocumentInfo = call { host.retryDocument(document.id) }

    /**
     * Unlocks a document that opened `LOCKED` (password-protected and opened
     * without one or with a wrong one). Throws `permission-denied` for a wrong
     * password, with the engine's own message ("incorrect document password");
     * the tab stays locked.
     */
    public suspend fun unlock(document: DocumentInfo, password: String) {
        call { host.unlockDocument(document.id, password) }
    }

    // ── tabs ──

    public suspend fun rename(document: DocumentInfo, name: String) {
        call { host.renameDocument(document.id, name) }
    }

    public suspend fun close(document: DocumentInfo) {
        call { host.closeDocument(document.id) }
    }

    public suspend fun closeAll() {
        call { host.closeAllDocuments() }
    }

    /** Includes loading, failed and locked tabs. */
    public suspend fun documents(): List<DocumentInfo> = call { host.documents() }

    /** Includes loading, failed and locked tabs; `null` selects the active tab. */
    public suspend fun document(id: String? = null): DocumentInfo? = call { host.document(id) }

    public suspend fun hasDocument(id: String): Boolean = call { host.hasDocument(id) }

    public suspend fun documentCount(): Int = call { host.documentCount() }.toIntResult("document count")

    public suspend fun activeDocument(): DocumentInfo? = call { host.activeDocument() }

    public suspend fun activeDocumentId(): String? = call { host.activeDocumentId() }

    public suspend fun setActive(document: DocumentInfo) {
        call { host.setActiveDocument(document.id) }
    }

    public suspend fun documentOrder(): List<String> = call { host.documentOrder() }

    /** Must be a permutation of the current order, or throws `invalid-input`. */
    public suspend fun setDocumentOrder(ids: List<String>) {
        call { host.setDocumentOrder(ids) }
    }

    /** Clamped into the order. */
    public suspend fun move(document: DocumentInfo, toIndex: Int) {
        call { host.moveDocument(document.id, toIndex.toLong()) }
    }

    public suspend fun swap(first: DocumentInfo, second: DocumentInfo) {
        call { host.swapDocuments(first.id, second.id) }
    }

    // ── pages ──

    /** Throws `not-found` for a document the viewer doesn't have. */
    public suspend fun pages(document: DocumentInfo): List<PageInfo> = measured("pages") {
        call { host.pages(document.id) }
    }

    /**
     * Uses the active document when [document] is `null`. A missing or closed
     * ref returns `null`, rather than the `not-found` of [pages].
     */
    public suspend fun page(ref: PageRef, document: DocumentInfo? = null): PageInfo? =
        call { host.page(ref, document?.id) }

    /** `null` for an index past the last page, or below zero. */
    public suspend fun page(index: Int, document: DocumentInfo? = null): PageInfo? =
        call { host.pageAt(index.toLong(), document?.id) }

    /** -1 for a missing page or document. */
    public suspend fun index(page: PageRef, document: DocumentInfo? = null): Int =
        call { host.pageIndex(page, document?.id) }.toIntResult("page index")

    /** The page-list revision, or -1 when the document is absent. */
    public suspend fun pageRevision(document: DocumentInfo? = null): Long = call { host.pageRevision(document?.id) }

    // ── permissions and saving ──

    public suspend fun allows(permission: DocumentPermission, document: DocumentInfo? = null): Boolean =
        call { host.allows(permission.value, document?.id) }

    public suspend fun canDownload(document: DocumentInfo? = null): Boolean = call { host.canDownload(document?.id) }

    /**
     * The complete document as PDF bytes, for the app to write wherever it
     * keeps files. [SaveMode.INCREMENTAL] (the default) appends the edits after
     * the original bytes and keeps every signature valid; [SaveMode.REWRITE]
     * writes a fresh, smaller file without the revision history, and a signed
     * document refuses it with `permission-denied`. Needs `doc.download` in
     * `ViewerConfig.scope`.
     */
    public suspend fun save(document: DocumentInfo, mode: SaveMode = SaveMode.INCREMENTAL): ByteArray =
        measured("save") {
            callCancellable { token -> host.save(document.id, mode, token) }
        }

    /**
     * An editing-layer artifact, which needs its original base to restore with
     * `open(base, layer)`. Use [save] for a standalone PDF. Needs `doc.download`.
     */
    public suspend fun saveLayer(document: DocumentInfo): ByteArray =
        callCancellable { token -> host.saveLayer(document.id, token) }

    // ── settings ──

    /**
     * Presentation hints for native chrome. CSS strings are returned for the
     * app to interpret; they do not paint UI or change engine authorization.
     */
    public suspend fun settings(): ViewerSettings = call { host.viewerSettings() }

    /**
     * Omitted fields stay unchanged; `clear` writes `null`. Observe changes
     * through [observeStateRevisions]. Engine scope is fixed when the viewer is
     * created.
     */
    public suspend fun updateSettings(patch: ViewerSettingsPatch) {
        call { host.updateViewerSettings(patch) }
    }

    public suspend fun resetSettings() {
        call { host.resetViewerSettings() }
    }

    // ── internals ──

    /** [bridged], refusing with `instance-closed` once [close] released the handle. */
    internal suspend fun <T> call(block: suspend () -> T): T = bridged {
        if (released.get()) throw closedException()
        block()
    }

    /** [bridgedCancellable], refusing with `instance-closed` once [close] released the handle. */
    internal suspend fun <T> callCancellable(
        onDiscard: (suspend (T) -> Unit)? = null,
        block: suspend (uniffi.epdf_ffi.CancelToken) -> T,
    ): T {
        if (released.get()) throw closedException()
        return bridgedCancellable(onDiscard, block)
    }

    /** Closes a document an open made after its caller was cancelled. */
    private val closeDiscarded: suspend (DocumentInfo) -> Unit = { document -> host.closeDocument(document.id) }

    /**
     * Sends the one report that PDF JavaScript is off, the first time a
     * document opens or [actions] is asked for, so it reaches collectors that
     * subscribed after the viewer was built.
     */
    internal fun reportUnavailableJavaScript() {
        val reason = javaScriptUnavailable ?: return
        if (!javaScriptReported.compareAndSet(false, true)) return
        errorHub.send(PdfException(PdfException.Code.UNSUPPORTED, reason, "NotSupportedError"))
    }

    /** [config] with JavaScript disabled when its sandbox factory has no sandbox here. */
    private fun withoutUnavailableJavaScript(config: ViewerConfig): ViewerConfig {
        if (javaScriptUnavailable == null) return config
        val actions = config.actions ?: return config
        val javascript = actions.javascript ?: return config
        return config.copy(actions = actions.copy(javascript = javascript.copy(enabled = false, engine = null)))
    }

    private fun closedException() =
        PdfException(PdfException.Code.INSTANCE_CLOSED, "The viewer has shut down.", "Closed")

    private fun finishStreams() {
        registry.finishAll()
        revisionHub.finish()
        errorHub.finish()
    }
}

/**
 * Keeps a [DocumentFetcher] that throws away from the kernel's thread: the
 * open it was serving fails with the exception's message, and the exception is
 * reported.
 */
private class GuardedFetcher(
    private val fetcher: DocumentFetcher,
    private val errors: BroadcastHub<PdfException>,
) : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        val cancellation = try {
            fetcher.fetch(url, completion)
        } catch (e: Exception) {
            errors.send(PdfException.from(e))
            completion.failWith(HostFailure(e))
            return EmptyCancellation
        }
        return GuardedCancellation(cancellation, errors)
    }
}

private class GuardedCancellation(
    private val cancellation: DocumentFetchCancellation,
    private val errors: BroadcastHub<PdfException>,
) : DocumentFetchCancellation {
    override fun cancel() {
        try {
            cancellation.cancel()
        } catch (e: Exception) {
            errors.send(PdfException.from(e))
        }
    }
}
