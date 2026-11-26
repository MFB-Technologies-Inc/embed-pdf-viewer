package com.embedpdf

// Entry points named after what a caller wants to do, over capabilities
// named after the engine part that does it. Each one holds only the
// capability it forwards to, which it hands back for everything else, and
// every member is a one-line forward with the capability member's own name,
// signature and errors. `TaskEntryPointTest` keeps the two sides in step.

/**
 * Moves through a document's pages: the current page, the page count, and
 * going to a page. Get one with [PdfViewer.pageNavigator], which needs what
 * [PdfViewer.stage] needs and throws what it throws. For everything else
 * (camera, scrolling, layout, coordinates, gestures) use [stage].
 */
public class PageNavigator internal constructor(
    /** The capability every member forwards to. */
    public val stage: StageCapability,
) {
    public suspend fun pageCount(): Int = stage.pageCount()
    public suspend fun currentPageIndex(): Int = stage.currentPageIndex()
    public suspend fun currentPage(): PageRef? = stage.currentPage()
    public suspend fun canGoNext(): Boolean = stage.canGoNext()
    public suspend fun canGoPrevious(): Boolean = stage.canGoPrevious()

    /** An index below zero or past `UInt` throws `invalid-input`. */
    public suspend fun goToPageIndex(index: Int, options: StageGoToOptions? = null): Unit = stage.goToPageIndex(index, options)
    public suspend fun goToPage(page: PageRef, options: StageGoToOptions? = null): Unit = stage.goToPage(page, options)
    public suspend fun goToFirstPage(options: StageGoToOptions? = null): Unit = stage.goToFirstPage(options)
    public suspend fun goToLastPage(options: StageGoToOptions? = null): Unit = stage.goToLastPage(options)
    public suspend fun nextPage(options: StageGoToOptions? = null): Unit = stage.nextPage(options)
    public suspend fun previousPage(options: StageGoToOptions? = null): Unit = stage.previousPage(options)
}

/**
 * Zooms a document's view in and out and fits its pages to the viewport.
 * Get one with [PdfViewer.pageZoom], which needs what [PdfViewer.stage] needs
 * and throws what it throws. For everything else (zoom by a factor, around a
 * point, the zoom tool's box, rotation) use [stage].
 */
public class PageZoom internal constructor(
    /** The capability every member forwards to. */
    public val stage: StageCapability,
) {
    public suspend fun zoomLevel(): Double = stage.zoomLevel()
    public suspend fun zoomMode(): StageZoomMode = stage.zoomMode()
    public suspend fun canZoomIn(): Boolean = stage.canZoomIn()
    public suspend fun canZoomOut(): Boolean = stage.canZoomOut()
    public suspend fun zoomIn(): Unit = stage.zoomIn()
    public suspend fun zoomOut(): Unit = stage.zoomOut()
    public suspend fun zoomTo(target: StageZoomTarget, around: Point? = null): Unit = stage.zoomTo(target, around)
    public suspend fun fitWidth(): Unit = stage.fitWidth()
    public suspend fun fitPage(): Unit = stage.fitPage()
}

/**
 * Picks the tool pointer input goes to: lists the tools, reads the active
 * one, and switches. Get one with [PdfViewer.toolSwitcher], which needs what
 * [PdfViewer.interaction] needs and throws what it throws. For everything
 * else (registering tools and handlers, cursors, pointer samples, settings)
 * use [interaction].
 */
public class ToolSwitcher internal constructor(
    /** The capability every member forwards to. */
    public val interaction: InteractionCapability,
) {
    public suspend fun tools(): List<InteractionTool> = interaction.tools()
    public suspend fun activeToolId(): ToolId = interaction.activeToolId()
    public suspend fun defaultToolId(): ToolId = interaction.defaultToolId()
    public suspend fun hasTool(id: ToolId): Boolean = interaction.hasTool(id)

    /** Throws `not-found` for an id nobody registered. */
    public suspend fun activateTool(id: ToolId): Unit = interaction.activateTool(id)
    public suspend fun activateDefaultTool(): Unit = interaction.activateDefaultTool()
    public suspend fun pushTool(id: ToolId): Unit = interaction.pushTool(id)
    public suspend fun popTool(): Unit = interaction.popTool()
}

public suspend fun PdfViewer.pageNavigator(document: DocumentInfo): PageNavigator = PageNavigator(stage(document))

public suspend fun PdfViewer.pageZoom(document: DocumentInfo): PageZoom = PageZoom(stage(document))

public suspend fun PdfViewer.toolSwitcher(document: DocumentInfo): ToolSwitcher = ToolSwitcher(interaction(document))
