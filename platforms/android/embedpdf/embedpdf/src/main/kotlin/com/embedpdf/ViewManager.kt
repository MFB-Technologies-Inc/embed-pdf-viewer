package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_view_manager.ViewManagerEventListener
import uniffi.epdf_plugin_view_manager.ViewManagerHandle

public typealias PaneInfo = uniffi.epdf_plugin_view_manager.PaneInfo
public typealias PaneEvent = uniffi.epdf_plugin_view_manager.PaneEvent
public typealias PaneFocusChangedEvent = uniffi.epdf_plugin_view_manager.PaneFocusChangedEvent
public typealias DocumentMovedEvent = uniffi.epdf_plugin_view_manager.DocumentMovedEvent
public typealias ViewManagerEvent = uniffi.epdf_plugin_view_manager.ViewManagerEvent
public typealias ViewManagerEventPaneCreated = uniffi.epdf_plugin_view_manager.ViewManagerEvent.PaneCreated
public typealias ViewManagerEventPaneRemoved = uniffi.epdf_plugin_view_manager.ViewManagerEvent.PaneRemoved
public typealias ViewManagerEventFocusChanged = uniffi.epdf_plugin_view_manager.ViewManagerEvent.FocusChanged
public typealias ViewManagerEventDocumentMoved = uniffi.epdf_plugin_view_manager.ViewManagerEvent.DocumentMoved

/**
 * Panes over the open documents: each pane is a tab strip with one active
 * document, and the panes partition the documents. Get it with
 * [PdfViewer.viewManager]; the viewer needs `ViewerConfig(viewManager = true)`.
 * One capability for the whole viewer. It keeps the viewer alive, and after
 * `destroy` every call throws `instance-closed`. Documents are
 * [DocumentInfo]s, as every other capability takes them; panes are the ids
 * [panes] lists. No member throws for an id it doesn't know, as on the web: an
 * unknown pane is ignored. [setFocusedPane], [createPane], [splitPane] and
 * [addDocument] don't check documents or panes against what's open: a closed
 * document lands as a tab (or an unknown pane as the focus) until the open
 * documents next change.
 */
public class ViewManagerCapability internal constructor(
    private val handle: ViewManagerHandle,
    internal val viewer: PdfViewer,
) {
    /** In display order. */
    public suspend fun panes(): List<PaneInfo> = viewer.call { handle.panes() }

    public suspend fun pane(id: String): PaneInfo? = viewer.call { handle.pane(id) }

    public suspend fun paneOrder(): List<String> = viewer.call { handle.paneOrder() }

    public suspend fun focusedPaneId(): String? = viewer.call { handle.focusedPaneId() }

    /** The id of the pane that holds [holding], if any. */
    public suspend fun pane(holding: DocumentInfo): String? = viewer.call { handle.paneOfDocument(holding.id) }

    /** A new pane, focused. Each document moves in from wherever it was. */
    public suspend fun createPane(documents: List<DocumentInfo> = emptyList()): String =
        viewer.call { handle.createPane(documents.map { it.id }) }

    /** Its documents go to [movingDocumentsTo], else the focused pane, else the last one. */
    public suspend fun removePane(id: String, movingDocumentsTo: String? = null) {
        viewer.call { handle.removePane(id, movingDocumentsTo) }
    }

    /** Clamped into the order. An index below zero throws `invalid-input`. */
    public suspend fun movePane(id: String, to: Int) {
        val index = to.toWireUInt("pane index")
        viewer.call { handle.movePane(id, index) }
    }

    public suspend fun setFocusedPane(id: String?) {
        viewer.call { handle.setFocusedPane(id) }
    }

    /**
     * A new pane showing [showing], beside [from] (or the focused pane),
     * focused. Returns the new pane's id.
     */
    public suspend fun splitPane(showing: DocumentInfo, from: String? = null): String =
        viewer.call { handle.splitPane(showing.id, from) }

    public suspend fun setActiveDocument(document: DocumentInfo?, paneId: String) {
        viewer.call { handle.setActiveDocument(paneId, document?.id) }
    }

    /**
     * Adds [document] to pane [to]; a document another pane holds moves.
     * `null` [at] appends. An index below zero throws `invalid-input`.
     */
    public suspend fun addDocument(document: DocumentInfo, to: String, at: Int? = null) {
        val index = at?.toWireUInt("tab index")
        viewer.call { handle.addDocument(to, document.id, index) }
    }

    public suspend fun removeDocument(document: DocumentInfo, from: String) {
        viewer.call { handle.removeDocument(from, document.id) }
    }

    /** Reorders a tab inside pane [within]. An index below zero throws `invalid-input`. */
    public suspend fun moveDocument(document: DocumentInfo, within: String, to: Int) {
        val index = to.toWireUInt("tab index")
        viewer.call { handle.moveDocumentWithin(within, document.id, index) }
    }

    /**
     * Drags a tab from pane [from] into pane [to], where it becomes active.
     * `null` [at] appends. An index below zero throws `invalid-input`.
     */
    public suspend fun moveDocument(document: DocumentInfo, from: String, to: String, at: Int? = null) {
        val index = at?.toWireUInt("tab index")
        viewer.call { handle.moveDocumentBetween(from, to, document.id, index) }
    }

    /**
     * Pane events. Completes when the viewer is destroyed (at once if it
     * already has been). Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<ViewManagerEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : ViewManagerEventListener {
                override fun onEvent(event: ViewManagerEvent) {
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
 * The viewer's view manager. Throws `unsupported` when the viewer was built
 * without the view-manager plugin.
 */
public suspend fun PdfViewer.viewManager(): ViewManagerCapability =
    ViewManagerCapability(call { host.viewManager() }, this)
