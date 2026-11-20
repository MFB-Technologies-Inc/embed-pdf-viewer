package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_outline.OutlineEventListener
import uniffi.epdf_plugin_outline.OutlineHandle

public typealias OutlineItemRef = uniffi.epdf_plugin_outline.OutlineItemRef
public typealias OutlineItem = uniffi.epdf_plugin_outline.OutlineItem
public typealias OutlineRow = uniffi.epdf_plugin_outline.OutlineRow
public typealias OutlineSnapshot = uniffi.epdf_plugin_outline.OutlineSnapshot
public typealias OutlinePosition = uniffi.epdf_plugin_outline.OutlinePosition
public typealias OutlinePositionStart = uniffi.epdf_plugin_outline.OutlinePosition.Start
public typealias OutlinePositionEnd = uniffi.epdf_plugin_outline.OutlinePosition.End
public typealias OutlinePositionBefore = uniffi.epdf_plugin_outline.OutlinePosition.Before
public typealias OutlinePositionAfter = uniffi.epdf_plugin_outline.OutlinePosition.After
public typealias OutlineItemInput = uniffi.epdf_plugin_outline.OutlineItemInput
public typealias OutlineItemPatch = uniffi.epdf_plugin_outline.OutlineItemPatch
public typealias OutlinePatchKey = uniffi.epdf_plugin_outline.OutlinePatchKey
public typealias OutlineCreateOptions = uniffi.epdf_plugin_outline.OutlineCreateOptions
public typealias OutlineEvent = uniffi.epdf_plugin_outline.OutlineEvent
public typealias OutlineEventChanged = uniffi.epdf_plugin_outline.OutlineEvent.Changed
public typealias OutlineEventResynced = uniffi.epdf_plugin_outline.OutlineEvent.Resynced

// The link plugin's records the outline answers with. The link capability
// itself arrives with wave C.
public typealias PageLinkTarget = uniffi.epdf_ffi.PageLinkTarget
public typealias LinkActivation = uniffi.epdf_plugin_link.LinkActivation

/**
 * Outline reads, navigation and undoable edits for one open document. Get one
 * with [PdfViewer.outline]; the viewer needs `ViewerConfig(outline = true)`.
 * After its document closes, every call throws `instance-closed`.
 */
public class OutlineCapability internal constructor(
    private val handle: OutlineHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun list(): List<OutlineItem> = viewer.call { handle.list() }

    public suspend fun item(ref: OutlineItemRef): OutlineItem? = viewer.call { handle.item(ref) }

    /** The top-level items when [parent] is `null`. */
    public suspend fun children(parent: OutlineItemRef? = null): List<OutlineItem> = viewer.call { handle.children(parent) }

    public suspend fun rows(): List<OutlineRow> = viewer.call { handle.rows() }

    public suspend fun currentItem(): OutlineItemRef? = viewer.call { handle.currentItem() }

    public suspend fun selectedItems(): List<OutlineItemRef> = viewer.call { handle.selectedItems() }

    public suspend fun select(refs: List<OutlineItemRef>) {
        viewer.call { handle.select(refs) }
    }

    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    public suspend fun isIncomplete(): Boolean = viewer.call { handle.isIncomplete() }

    public suspend fun isExpanded(ref: OutlineItemRef): Boolean = viewer.call { handle.isExpanded(ref) }

    public suspend fun canEdit(): Boolean = viewer.call { handle.canEdit() }

    public suspend fun currentViewTarget(): PageLinkTarget? = viewer.call { handle.currentViewTarget() }

    public suspend fun activate(ref: OutlineItemRef): LinkActivation = viewer.call { handle.activate(ref) }

    public suspend fun ensureLoaded() {
        viewer.callCancellable { token -> handle.ensureLoaded(token) }
    }

    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    /**
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun create(input: OutlineItemInput, options: OutlineCreateOptions = OutlineCreateOptions()): OutlineItem =
        viewer.callCancellable { token -> handle.create(input, options, token) }

    /**
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun update(ref: OutlineItemRef, patch: OutlineItemPatch): OutlineItem =
        viewer.callCancellable { token -> handle.update(ref, patch, token) }

    /**
     * Moves [refs] under [parent], the top level when `null`, at [position].
     *
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun move(refs: List<OutlineItemRef>, parent: OutlineItemRef?, position: OutlinePosition = OutlinePositionEnd) {
        viewer.callCancellable { token -> handle.moveItems(refs, parent, position, token) }
    }

    /**
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun delete(refs: List<OutlineItemRef>) {
        viewer.callCancellable { token -> handle.deleteItems(refs, token) }
    }

    public suspend fun expand(ref: OutlineItemRef) {
        viewer.call { handle.expand(ref) }
    }

    public suspend fun collapse(ref: OutlineItemRef) {
        viewer.call { handle.collapse(ref) }
    }

    public suspend fun toggle(ref: OutlineItemRef) {
        viewer.call { handle.toggle(ref) }
    }

    public suspend fun reveal(ref: OutlineItemRef) {
        viewer.call { handle.reveal(ref) }
    }

    public suspend fun expandAll() {
        viewer.call { handle.expandAll() }
    }

    public suspend fun collapseAll() {
        viewer.call { handle.collapseAll() }
    }

    /**
     * Outline changes and resyncs. Completes when the document closes (at
     * once if it already has) or the viewer is destroyed. Each collection is a
     * new subscription.
     */
    public fun observeEvents(): Flow<OutlineEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : OutlineEventListener {
                override fun onEvent(event: OutlineEvent) {
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
 * The outline capability for [document]. Throws `unsupported` when the viewer
 * was built without the outline plugin, whatever the document's state.
 */
public suspend fun PdfViewer.outline(document: DocumentInfo): OutlineCapability =
    OutlineCapability(call { host.outline(document.id) }, this)
