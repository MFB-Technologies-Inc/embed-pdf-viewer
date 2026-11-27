package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_redaction.RedactionEventListener
import uniffi.epdf_plugin_redaction.RedactionHandle

public typealias RedactionSettings = uniffi.epdf_plugin_redaction.RedactionSettings
public typealias RedactionTextSettings = uniffi.epdf_plugin_redaction.RedactionTextSettings

public typealias RedactionConfig = uniffi.epdf_plugin_redaction.RedactionConfig
public typealias RedactionOverlayText = uniffi.epdf_plugin_redaction.RedactionOverlayText
public typealias RedactionMark = uniffi.epdf_plugin_redaction.RedactionMark
public typealias RedactionMarkKind = uniffi.epdf_plugin_redaction.RedactionMarkKind
public typealias RedactionCollateral = uniffi.epdf_plugin_redaction.RedactionCollateral
public typealias RedactionLabelPatch = uniffi.epdf_plugin_redaction.RedactionLabelPatch
public typealias RedactionUnmarkResult = uniffi.epdf_plugin_redaction.RedactionUnmarkResult
public typealias RedactionSkipped = uniffi.epdf_plugin_redaction.RedactionSkipped
public typealias RedactionUnmarkFailure = uniffi.epdf_plugin_redaction.RedactionUnmarkFailure
public typealias RedactionApplyResult = uniffi.epdf_plugin_redaction.RedactionApplyResult
public typealias RedactionApplyScope = uniffi.epdf_plugin_redaction.RedactionApplyScope
public typealias RedactionApplyScopePages = uniffi.epdf_plugin_redaction.RedactionApplyScope.Pages
public typealias RedactionApplyScopeAnnotations = uniffi.epdf_plugin_redaction.RedactionApplyScope.Annotations
public typealias RedactionPageResult = uniffi.epdf_plugin_redaction.RedactionPageResult
public typealias RedactionPageStatus = uniffi.epdf_plugin_redaction.RedactionPageStatus
public typealias RedactionEvent = uniffi.epdf_plugin_redaction.RedactionEvent
public typealias RedactionEventApplied = uniffi.epdf_plugin_redaction.RedactionEvent.Applied
public typealias RedactionEventPendingChanged = uniffi.epdf_plugin_redaction.RedactionEvent.PendingChanged

/**
 * A document's redactions: mark regions, review them, apply them. Get one
 * with [PdfViewer.redaction]; the viewer needs
 * `ViewerConfig(interaction = true, annotation = "", redaction = RedactionConfig())`.
 * It keeps the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 *
 * [markSelection] also needs the selection plugin and [markMatches] the
 * search plugin; without it each throws `unsupported`.
 *
 * A mark, unmark, label change or apply that started lands even when the
 * caller is cancelled, and the caller then sees its cancellation rather than
 * the result (see the README's Cancellation section). Read [pending] again
 * after a cancelled one.
 */
public class RedactionCapability internal constructor(
    private val handle: RedactionHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): RedactionSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: RedactionConfig) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun canUnmark(ref: AnnotationRef): Boolean = viewer.call { handle.canUnmark(ref) }

    public suspend fun canUpdateLabel(ref: AnnotationRef): Boolean = viewer.call { handle.canUpdateLabel(ref) }

    /** Pending marks, or those on one page. */
    public suspend fun pending(page: PageRef? = null): List<RedactionMark> = viewer.call { handle.pending(page) }

    public suspend fun pendingItem(ref: AnnotationRef): RedactionMark? = viewer.call { handle.pendingItem(ref) }

    public suspend fun pendingCount(page: PageRef? = null): Int =
        viewer.call { handle.pendingCount(page) }.toIntResult("pending count")

    /** What applying [refs] (every pending mark when `null`) would also remove. */
    public suspend fun estimateCollateral(refs: List<AnnotationRef>? = null): RedactionCollateral =
        viewer.call { handle.estimateCollateral(refs) }

    /** Marks the current text selection, one mark per page. Throws `unsupported` without the selection plugin. */
    public suspend fun markSelection(): List<RedactionMark> = viewer.callCancellable { token -> handle.markSelection(token) }

    public suspend fun markArea(bounds: Rect, page: PageRef): RedactionMark =
        viewer.callCancellable { token -> handle.markArea(page, bounds, token) }

    public suspend fun markPage(page: PageRef): RedactionMark = viewer.callCancellable { token -> handle.markPage(page, token) }

    /**
     * Marks every match of [query], on [pages] or on every page when `null`.
     * Throws `unsupported` without the search plugin.
     *
     * Cancelling the calling coroutine aborts the search or stops between two
     * marks, and the marks made so far stay pending. A cancel that arrives
     * after the call started but before the search finds a hit returns an
     * empty list rather than throwing, as on every other platform, and a call
     * whose marks all complete before the cancel lands returns those marks;
     * either way the caller's cancellation comes at its next suspension. A
     * caller already cancelled before the call gets its `CancellationException`.
     */
    public suspend fun markMatches(query: SearchQuery, pages: List<PageRef>? = null): List<RedactionMark> =
        viewer.callCancellable(keepResolved = true) { token -> handle.markMatches(query, pages, token) }

    public suspend fun unmark(refs: List<AnnotationRef>): RedactionUnmarkResult =
        viewer.callCancellable { token -> handle.unmark(refs, token) }

    public suspend fun clearPending(): RedactionUnmarkResult = viewer.callCancellable { token -> handle.clearPending(token) }

    public suspend fun updateLabel(ref: AnnotationRef, patch: RedactionLabelPatch): RedactionMark =
        viewer.callCancellable { token -> handle.updateLabel(ref, patch, token) }

    /** Applies these pending marks. Irreversible. */
    public suspend fun apply(refs: List<AnnotationRef>): RedactionApplyResult =
        viewer.callCancellable { token -> handle.apply(refs, token) }

    /**
     * Applies every mark on these pages. Irreversible. Swift spells it
     * `apply(pages:)`; a second `apply` taking a `List` would clash with
     * [apply] on the JVM.
     */
    public suspend fun applyPages(pages: List<PageRef>): RedactionApplyResult =
        viewer.callCancellable { token -> handle.applyPages(pages, token) }

    /** Applies every redaction in the document. Irreversible. */
    public suspend fun applyAll(): RedactionApplyResult = viewer.callCancellable { token -> handle.applyAll(token) }

    public suspend fun canMark(): Boolean = viewer.call { handle.canMark() }

    public suspend fun canApply(): Boolean = viewer.call { handle.canApply() }

    public suspend fun isApplying(): Boolean = viewer.call { handle.isApplying() }

    public suspend fun lastResult(): RedactionApplyResult? = viewer.call { handle.lastResult() }

    /**
     * Applies and pending-mark changes. Completes when the document closes
     * (at once if it already has) or the viewer is destroyed. Each collection
     * is a new subscription.
     */
    public fun observeEvents(): Flow<RedactionEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : RedactionEventListener {
                override fun onEvent(event: RedactionEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The redaction capability for [document]. Throws `unsupported` when the
 * viewer was built without the redaction plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.redaction(document: DocumentInfo): RedactionCapability =
    RedactionCapability(call { host.redaction(document.id) }, this)
