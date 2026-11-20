package com.embedpdf

import uniffi.epdf_plugin_page_edit.PageEditHandle

public typealias PageEditPlacement = uniffi.epdf_plugin_page_edit.PageEditPlacement
public typealias PageEditPlacementAfter = uniffi.epdf_plugin_page_edit.PageEditPlacement.After
public typealias PageEditPlacementBefore = uniffi.epdf_plugin_page_edit.PageEditPlacement.Before
public typealias PageEditPlacementStart = uniffi.epdf_plugin_page_edit.PageEditPlacement.Start
public typealias PageEditPlacementEnd = uniffi.epdf_plugin_page_edit.PageEditPlacement.End
public typealias PageEditTurn = uniffi.epdf_plugin_page_edit.PageEditTurn
public typealias PageEditRotation = uniffi.epdf_plugin_page_edit.PageEditRotation
public typealias PageEditBlankOptions = uniffi.epdf_plugin_page_edit.PageEditBlankOptions
public typealias PageEditBytesOptions = uniffi.epdf_plugin_page_edit.PageEditBytesOptions
public typealias PageEditLayout = uniffi.epdf_plugin_page_edit.PageEditLayout
public typealias PageEditInsertion = uniffi.epdf_plugin_page_edit.PageEditInsertion

/**
 * Structural page edits, addressed by [PageRef]: rotate, move, delete, insert
 * and extract. Get one with [PdfViewer.pageEdit]; the viewer needs
 * `ViewerConfig(pageEdit = true)`. Page-list changes arrive on
 * [PdfViewer.observeDocumentEvents] as `PagesChanged`; this capability has no
 * flow of its own. Cancelling the calling coroutine skips an edit that hasn't
 * started; one that has started lands. After its document closes, every call
 * throws `instance-closed`.
 */
public class PageEditCapability internal constructor(
    private val handle: PageEditHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun selectedPages(): List<PageRef> = viewer.call { handle.getSelected() }

    public suspend fun select(pages: List<PageRef>) {
        viewer.call { handle.select(pages) }
    }

    public suspend fun canExtract(): Boolean = viewer.call { handle.canExtract() }

    /** Whether this caller may edit pages (`doc.pages.assemble`). */
    public suspend fun canEdit(): Boolean = viewer.call { handle.canEdit() }

    /** Turns each page from its own rotation. */
    public suspend fun rotate(pages: List<PageRef>, by: PageEditTurn): PageEditLayout =
        viewer.callCancellable { token -> handle.rotateBy(pages, by, token) }

    public suspend fun setRotation(rotation: PageEditRotation, of: List<PageRef>): PageEditLayout =
        viewer.callCancellable { token -> handle.setRotation(of, rotation, token) }

    /**
     * Reorders pages as a contiguous block at [to]. `End` puts the block
     * last. A placement names a neighbour page, which must not be one of the
     * pages moved.
     */
    public suspend fun movePages(pages: List<PageRef>, to: PageEditPlacement): PageEditLayout =
        viewer.callCancellable { token -> handle.movePages(pages, to, token) }

    /** The engine refuses deleting every page. */
    public suspend fun deletePages(pages: List<PageRef>): PageEditLayout =
        viewer.callCancellable { token -> handle.deletePages(pages, token) }

    public suspend fun insertBlank(options: PageEditBlankOptions = PageEditBlankOptions()): PageEditInsertion =
        viewer.callCancellable { token -> handle.insertBlank(options, token) }

    /** Pages of another PDF, given as its bytes. */
    public suspend fun insert(bytes: ByteArray, options: PageEditBytesOptions = PageEditBytesOptions()): PageEditInsertion =
        viewer.callCancellable { token -> handle.insertFromBytes(bytes, options, token) }

    /** Pages of another open document. */
    public suspend fun insert(pages: List<PageRef>, from: DocumentInfo, at: PageEditPlacement? = null): PageEditInsertion =
        viewer.callCancellable { token -> handle.insertFromDocument(from.id, pages, at, token) }

    /** Copies of pages; by default right after the last of them. */
    public suspend fun duplicate(pages: List<PageRef>, at: PageEditPlacement? = null): PageEditInsertion =
        viewer.callCancellable { token -> handle.duplicate(pages, at, token) }

    /** A new PDF from a page subset. */
    public suspend fun extract(pages: List<PageRef>): ByteArray =
        viewer.callCancellable { token -> handle.extract(pages, token) }
}

/**
 * The page-edit capability for [document]. Throws `unsupported` when the
 * viewer was built without the page-edit plugin, whatever the document's state.
 */
public suspend fun PdfViewer.pageEdit(document: DocumentInfo): PageEditCapability =
    PageEditCapability(call { host.pageEdit(document.id) }, this)
