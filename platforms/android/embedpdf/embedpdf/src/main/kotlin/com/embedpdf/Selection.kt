package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_selection.SelectionEventListener
import uniffi.epdf_plugin_selection.SelectionHandle

public typealias SelectionSettings = uniffi.epdf_plugin_selection.SelectionSettings
public typealias SelectionSettingsPatch = uniffi.epdf_plugin_selection.SelectionSettingsPatch
public typealias SelectionHandleSettings = uniffi.epdf_plugin_selection.SelectionHandleSettings
public typealias SelectionHandleSettingsPatch = uniffi.epdf_plugin_selection.SelectionHandleSettingsPatch

public typealias SelectionTextPosition = uniffi.epdf_plugin_selection.SelectionTextPosition
public typealias SelectionTextRange = uniffi.epdf_plugin_selection.SelectionTextRange
public typealias SelectionInput = uniffi.epdf_plugin_selection.SelectionInput
public typealias SelectionInputRange = uniffi.epdf_plugin_selection.SelectionInput.Range
public typealias SelectionInputSpan = uniffi.epdf_plugin_selection.SelectionInput.Span
public typealias SelectionSegment = uniffi.epdf_plugin_selection.SelectionSegment
public typealias SelectionEndpoint = uniffi.epdf_plugin_selection.SelectionEndpoint
public typealias SelectionAnchor = uniffi.epdf_plugin_selection.SelectionAnchor
public typealias SelectionDirection = uniffi.epdf_plugin_selection.SelectionDirection
public typealias SelectionSnapshotPage = uniffi.epdf_plugin_selection.SelectionSnapshotPage
public typealias SelectionSnapshot = uniffi.epdf_plugin_selection.SelectionSnapshot
public typealias SelectionEvent = uniffi.epdf_plugin_selection.SelectionEvent
public typealias SelectionEventChanged = uniffi.epdf_plugin_selection.SelectionEvent.Changed
public typealias SelectionEventCommitted = uniffi.epdf_plugin_selection.SelectionEvent.Committed
public typealias SelectionEventCleared = uniffi.epdf_plugin_selection.SelectionEvent.Cleared
public typealias SelectionChangedEvent = uniffi.epdf_plugin_selection.SelectionChangedEvent
public typealias SelectionCommittedEvent = uniffi.epdf_plugin_selection.SelectionCommittedEvent

/**
 * A document's text selection: select by range, span, word, line or page;
 * read its geometry and its text. Get one with [PdfViewer.selection]; the
 * viewer needs `ViewerConfig(interaction = true, selection = true)`. It keeps
 * the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 *
 * Indices are PDFium character indices, the space search hits use, not
 * offsets into a Kotlin `String`.
 */
public class SelectionCapability internal constructor(
    private val handle: SelectionHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): SelectionSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: SelectionSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /** Whether selection geometry is served (`doc.text.select`). */
    public suspend fun canSelect(): Boolean = viewer.call { handle.canSelect() }

    /** Whether text extraction is served (`doc.text.copy`). */
    public suspend fun canCopy(): Boolean = viewer.call { handle.canCopy() }

    public suspend fun select(input: SelectionInput) {
        viewer.call { handle.select(input) }
    }

    /** Selects a search hit's characters. */
    public suspend fun select(hit: SearchHit) {
        select(SelectionInputSpan(page = hit.page, start = hit.start.toLong(), count = hit.count.toLong()))
    }

    public suspend fun selectAll() {
        viewer.call { handle.selectAll() }
    }

    public suspend fun selectPage(page: PageRef) {
        viewer.call { handle.selectPage(page) }
    }

    /** `false` when [point] on [page] has no selectable text. */
    public suspend fun selectWord(point: Point, page: PageRef): Boolean = viewer.call { handle.selectWordAt(page, point) }

    /** `false` when [point] on [page] has no selectable text. */
    public suspend fun selectLine(point: Point, page: PageRef): Boolean = viewer.call { handle.selectLineAt(page, point) }

    public suspend fun extend(point: Point, page: PageRef) {
        viewer.call { handle.extendTo(page, point) }
    }

    public suspend fun clear() {
        viewer.call { handle.clear() }
    }

    public suspend fun hasSelection(): Boolean = viewer.call { handle.hasSelection() }

    public suspend fun snapshot(): SelectionSnapshot = viewer.call { handle.snapshot() }

    public suspend fun range(): SelectionTextRange? = viewer.call { handle.range() }

    public suspend fun selectedPages(): List<PageRef> = viewer.call { handle.selectedPages() }

    public suspend fun segments(page: PageRef): List<SelectionSegment> = viewer.call { handle.segments(page) }

    public suspend fun rects(page: PageRef): List<Rect> = viewer.call { handle.rects(page) }

    public suspend fun anchor(): SelectionAnchor? = viewer.call { handle.anchor() }

    /**
     * The selected text, pages joined with a newline. Throws
     * `permission-denied` without `doc.text.copy`. Cancelling the calling
     * coroutine cancels the read.
     */
    public suspend fun readText(): String = viewer.callCancellable { token -> handle.readText(token) }

    /** Text for any [range], leaving the selection alone. Cancelling the calling coroutine cancels the read. */
    public suspend fun readText(range: SelectionTextRange): String =
        viewer.callCancellable { token -> handle.readTextInRange(range, token) }

    public suspend fun ensureLoaded(page: PageRef) {
        viewer.call { handle.ensureLoaded(page) }
    }

    public suspend fun isLoaded(page: PageRef): Boolean = viewer.call { handle.isLoaded(page) }

    public suspend fun isOverText(point: Point, page: PageRef): Boolean = viewer.call { handle.isOverText(page, point) }

    public suspend fun beginGesture() {
        viewer.call { handle.beginGesture() }
    }

    /** `false` off text. */
    public suspend fun beginGesture(point: Point, page: PageRef): Boolean = viewer.call { handle.beginGestureAt(page, point) }

    public suspend fun endGesture() {
        viewer.call { handle.endGesture() }
    }

    /** Whether a selecting gesture is in flight. */
    public suspend fun isSelecting(): Boolean = viewer.call { handle.isSelecting() }

    public suspend fun setHighlightVisible(visible: Boolean) {
        viewer.call { handle.setHighlightVisible(visible) }
    }

    public suspend fun isHighlightVisible(): Boolean = viewer.call { handle.isHighlightVisible() }

    /**
     * Selection changes, commits and clears. Completes when the document
     * closes (at once if it already has) or the viewer is destroyed. Each
     * collection is a new subscription.
     */
    public fun observeEvents(): Flow<SelectionEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : SelectionEventListener {
                override fun onEvent(event: SelectionEvent) {
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
 * The selection capability for [document]. Throws `unsupported` when the
 * viewer was built without the selection plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.selection(document: DocumentInfo): SelectionCapability =
    SelectionCapability(call { host.selection(document.id) }, this)
