package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_stage.StageEventListener
import uniffi.epdf_plugin_stage.StageHandle

public typealias StagePageDestination = uniffi.epdf_plugin_stage.StagePageDestination
public typealias StagePageDestinationXyz = uniffi.epdf_plugin_stage.StagePageDestination.Xyz
public typealias StagePageDestinationFit = uniffi.epdf_plugin_stage.StagePageDestination.Fit
public typealias StagePageDestinationFitH = uniffi.epdf_plugin_stage.StagePageDestination.FitH
public typealias StagePageDestinationFitV = uniffi.epdf_plugin_stage.StagePageDestination.FitV
public typealias StagePageDestinationFitR = uniffi.epdf_plugin_stage.StagePageDestination.FitR
public typealias StagePageDestinationFitB = uniffi.epdf_plugin_stage.StagePageDestination.FitB
public typealias StagePageDestinationFitBh = uniffi.epdf_plugin_stage.StagePageDestination.FitBh
public typealias StagePageDestinationFitBv = uniffi.epdf_plugin_stage.StagePageDestination.FitBv
public typealias Camera = uniffi.epdf_ffi.Camera
public typealias ZoomMode = uniffi.epdf_ffi.ZoomMode
public typealias ZoomSpec = uniffi.epdf_ffi.ZoomSpec
public typealias ZoomSpecLevel = uniffi.epdf_ffi.ZoomSpec.Level
public typealias ZoomSpecPageWidth = uniffi.epdf_ffi.ZoomSpec.PageWidth
public typealias ZoomSpecPageHeight = uniffi.epdf_ffi.ZoomSpec.PageHeight
public typealias ZoomSpecMode = uniffi.epdf_ffi.ZoomSpec.Mode
public typealias ViewAnchor = uniffi.epdf_ffi.ViewAnchor
public typealias PageFrameInsets = uniffi.epdf_ffi.PageFrameInsets
public typealias StageAlign = uniffi.epdf_ffi.StageAlign
public typealias StageAlignValue = uniffi.epdf_ffi.StageAlignValue
public typealias StageAlignValueNamed = uniffi.epdf_ffi.StageAlignValue.Named
public typealias StageAlignValueFraction = uniffi.epdf_ffi.StageAlignValue.Fraction
public typealias StageAlignment = uniffi.epdf_ffi.StageAlignment
public typealias StageAlignmentValue = uniffi.epdf_ffi.StageAlignmentValue
public typealias StageDirection = uniffi.epdf_ffi.StageDirection
public typealias StageSpreadMode = uniffi.epdf_ffi.StageSpreadMode
public typealias StageSizingMode = uniffi.epdf_ffi.StageSizingMode
public typealias StageScrollMetrics = uniffi.epdf_ffi.StageScrollMetrics
public typealias StageConfig = uniffi.epdf_plugin_stage.StageConfig
public typealias StageFlow = uniffi.epdf_plugin_stage.StageFlow
public typealias StageLayout = uniffi.epdf_plugin_stage.StageLayout
public typealias StageScrollBehavior = uniffi.epdf_plugin_stage.StageScrollBehavior
public typealias StageGridColumns = uniffi.epdf_plugin_stage.StageGridColumns
public typealias StageGridColumnsSquare = uniffi.epdf_plugin_stage.StageGridColumns.Square
public typealias StageGridColumnsAuto = uniffi.epdf_plugin_stage.StageGridColumns.Auto
public typealias StageGridColumnsCount = uniffi.epdf_plugin_stage.StageGridColumns.Count
public typealias StageGap = uniffi.epdf_plugin_stage.StageGap
public typealias StageGapWorld = uniffi.epdf_plugin_stage.StageGap.World
public typealias StageGapPx = uniffi.epdf_plugin_stage.StageGap.Px
public typealias StageArrivalAlignValue = uniffi.epdf_plugin_stage.StageArrivalAlignValue
public typealias StageArrivalAlignValueKeep = uniffi.epdf_plugin_stage.StageArrivalAlignValue.Keep
public typealias StageArrivalAlignValueAlign = uniffi.epdf_plugin_stage.StageArrivalAlignValue.Align
public typealias StageArrivalAlignment = uniffi.epdf_plugin_stage.StageArrivalAlignment
public typealias StagePartialArrivalAlignment = uniffi.epdf_plugin_stage.StagePartialArrivalAlignment
public typealias StageSettings = uniffi.epdf_plugin_stage.StageSettings
public typealias StageSettingsPatch = uniffi.epdf_plugin_stage.StageSettingsPatch
public typealias StageSettingKey = uniffi.epdf_plugin_stage.StageSettingKey
public typealias StageZoomBoxLook = uniffi.epdf_plugin_stage.StageZoomBoxLook
public typealias StageOrientation = uniffi.epdf_plugin_stage.StageOrientation
public typealias StageBoxQuery = uniffi.epdf_plugin_stage.StageBoxQuery
public typealias StageResponsiveRule = uniffi.epdf_plugin_stage.StageResponsiveRule
public typealias StagePageTransform = uniffi.epdf_plugin_stage.StagePageTransform
public typealias StageVisiblePage = uniffi.epdf_plugin_stage.StageVisiblePage
public typealias StageViewpoint = uniffi.epdf_plugin_stage.StageViewpoint
public typealias StageViewState = uniffi.epdf_plugin_stage.StageViewState
public typealias StageScrollToOptions = uniffi.epdf_plugin_stage.StageScrollToOptions
public typealias StageGoToOptions = uniffi.epdf_plugin_stage.StageGoToOptions
public typealias StageRevealWord = uniffi.epdf_plugin_stage.StageRevealWord
public typealias StageRevealAnchorValue = uniffi.epdf_plugin_stage.StageRevealAnchorValue
public typealias StageRevealAnchorValueWord = uniffi.epdf_plugin_stage.StageRevealAnchorValue.Word
public typealias StageRevealAnchorValueFraction = uniffi.epdf_plugin_stage.StageRevealAnchorValue.Fraction
public typealias StageRevealAnchor = uniffi.epdf_plugin_stage.StageRevealAnchor
public typealias StageRevealZoom = uniffi.epdf_plugin_stage.StageRevealZoom
public typealias StageRevealZoomKeep = uniffi.epdf_plugin_stage.StageRevealZoom.Keep
public typealias StageRevealZoomFit = uniffi.epdf_plugin_stage.StageRevealZoom.Fit
public typealias StageRevealZoomFitWidth = uniffi.epdf_plugin_stage.StageRevealZoom.FitWidth
public typealias StageRevealZoomFitHeight = uniffi.epdf_plugin_stage.StageRevealZoom.FitHeight
public typealias StageRevealZoomLevel = uniffi.epdf_plugin_stage.StageRevealZoom.Level
public typealias StageRevealOptions = uniffi.epdf_plugin_stage.StageRevealOptions
public typealias StagePageHit = uniffi.epdf_plugin_stage.StagePageHit
public typealias StageZoomMode = uniffi.epdf_plugin_stage.StageZoomMode
public typealias StageZoomModeMode = uniffi.epdf_plugin_stage.StageZoomMode.Mode
public typealias StageZoomModeCustom = uniffi.epdf_plugin_stage.StageZoomMode.Custom
public typealias StageZoomTarget = uniffi.epdf_plugin_stage.StageZoomTarget
public typealias StageZoomTargetLevel = uniffi.epdf_plugin_stage.StageZoomTarget.Level
public typealias StageZoomTargetSpec = uniffi.epdf_plugin_stage.StageZoomTarget.Spec

/**
 * Supplies the view a document opens at. `initialView` is asked on the
 * viewer's kernel thread when the stage first places the document: answer
 * from state you already hold, without calling into the viewer.
 */
public typealias StageInitialViewProvider = uniffi.epdf_plugin_stage.StageInitialViewProvider
public typealias StageEvent = uniffi.epdf_plugin_stage.StageEvent
public typealias StageEventPageChanged = uniffi.epdf_plugin_stage.StageEvent.PageChanged
public typealias StageEventZoomChanged = uniffi.epdf_plugin_stage.StageEvent.ZoomChanged
public typealias StageEventCameraChanged = uniffi.epdf_plugin_stage.StageEvent.CameraChanged
public typealias StageEventMotionEnded = uniffi.epdf_plugin_stage.StageEvent.MotionEnded
public typealias StageEventSettingsChanged = uniffi.epdf_plugin_stage.StageEvent.SettingsChanged
public typealias StageEventViewportChanged = uniffi.epdf_plugin_stage.StageEvent.ViewportChanged
public typealias StagePageChangedEvent = uniffi.epdf_plugin_stage.StagePageChangedEvent
public typealias StageZoomChangedEvent = uniffi.epdf_plugin_stage.StageZoomChangedEvent
public typealias StageCameraChangedEvent = uniffi.epdf_plugin_stage.StageCameraChangedEvent
public typealias StageMotionEndedEvent = uniffi.epdf_plugin_stage.StageMotionEndedEvent
public typealias StageSettingsChangedEvent = uniffi.epdf_plugin_stage.StageSettingsChangedEvent
public typealias StageViewportChangedEvent = uniffi.epdf_plugin_stage.StageViewportChangedEvent

/**
 * A document's viewport lens: camera, zoom, scroll, layout and navigation.
 * Get one with [PdfViewer.stage]; the viewer needs
 * `ViewerConfig(stage = StageConfig())`. It keeps the viewer alive. After its
 * document closes, every call throws `instance-closed`.
 *
 * [setCamera], [panBy], [scrollBy] and [zoomAround] return at once and run in
 * order; their errors arrive on [PdfViewer.observeReportedErrors].
 */
public class StageCapability internal constructor(
    private val handle: StageHandle,
    internal val viewer: PdfViewer,
) {
    // ── read ──

    public suspend fun pageCount(): Int = viewer.call { handle.pageCount() }.toIntResult("page count")

    public suspend fun currentDestination(): StagePageDestination? = viewer.call { handle.currentDestination() }

    public suspend fun goToDestination(destination: StagePageDestination, behavior: StageScrollBehavior? = null) {
        viewer.call { handle.goToDestination(destination, behavior) }
    }

    public suspend fun canZoomIn(): Boolean = viewer.call { handle.canZoomIn() }

    public suspend fun canZoomOut(): Boolean = viewer.call { handle.canZoomOut() }

    public suspend fun camera(): Camera = viewer.call { handle.camera() }

    public suspend fun viewportSize(): Size = viewer.call { handle.viewportSize() }

    public suspend fun scrollMetrics(): StageScrollMetrics = viewer.call { handle.scrollMetrics() }

    public suspend fun visiblePages(): List<StageVisiblePage> = viewer.call { handle.visiblePages() }

    public suspend fun isPageVisible(page: PageRef): Boolean = viewer.call { handle.isPageVisible(page) }

    public suspend fun currentPageIndex(): Int = viewer.call { handle.currentPageIndex() }.toIntResult("current page index")

    public suspend fun currentPage(): PageRef? = viewer.call { handle.currentPage() }

    public suspend fun currentItemPages(): List<PageInfo> = viewer.call { handle.currentItemPages() }

    /**
     * Pages navigation steps past: the current item when it fits across the
     * viewport, otherwise the current page alone.
     */
    public suspend fun currentPages(): List<PageInfo> = viewer.call { handle.currentPages() }

    /** Starts the zoom tool's box at [point], in viewport points. */
    public suspend fun beginZoomDrag(point: Point) {
        viewer.call { handle.beginZoomDrag(point) }
    }

    public suspend fun moveZoomDrag(point: Point) {
        viewer.call { handle.moveZoomDrag(point) }
    }

    /**
     * Zooms into the box, or one step for a click (out with [zoomOut]).
     * Leaving [zoomOut] out is `false`, as in the engine
     * (`StageCapability::end_zoom_drag` in
     * `crates/plugins/stage/src/navigation/zoom_drag.rs`).
     */
    public suspend fun endZoomDrag(zoomOut: Boolean = false) {
        viewer.call { handle.endZoomDrag(zoomOut) }
    }

    public suspend fun cancelZoomDrag() {
        viewer.call { handle.cancelZoomDrag() }
    }

    public suspend fun zoomBox(): Rect? = viewer.call { handle.zoomBox() }

    public suspend fun pageFrame(page: PageRef): StageVisiblePage? = viewer.call { handle.pageFrame(page) }

    /** The page under a viewport point, or `null` over a gap. */
    public suspend fun page(point: Point): StagePageHit? = viewer.call { handle.pageAt(point) }

    public suspend fun viewportToPage(page: PageRef, point: Point): Point? = viewer.call { handle.viewportToPage(page, point) }

    public suspend fun pageToWorld(page: PageRef, point: Point): Point? = viewer.call { handle.pageToWorld(page, point) }

    public suspend fun pageRectToViewport(page: PageRef, rect: Rect): Rect? =
        viewer.call { handle.pageRectToViewport(page, rect) }

    public suspend fun worldToViewport(point: Point): Point = viewer.call { handle.worldToViewport(point) }

    public suspend fun viewportToWorld(point: Point): Point = viewer.call { handle.viewportToWorld(point) }

    public suspend fun pageToViewport(page: PageRef, point: Point): Point? = viewer.call { handle.pageToViewport(page, point) }

    public suspend fun viewRotation(): Double = viewer.call { handle.viewRotation() }

    public suspend fun zoomLevel(): Double = viewer.call { handle.zoomLevel() }

    public suspend fun zoomMode(): StageZoomMode = viewer.call { handle.zoomMode() }

    public suspend fun viewpoint(): StageViewpoint = viewer.call { handle.viewpoint() }

    // ── motion ──

    public suspend fun fling(velocityX: Double, velocityY: Double) {
        viewer.call { handle.fling(velocityX, velocityY) }
    }

    public suspend fun isMoving(): Boolean = viewer.call { handle.isMoving() }

    public suspend fun stopMotion() {
        viewer.call { handle.stopMotion() }
    }

    /** Returns at once, from any thread. Dropped after the viewer shut down. */
    public fun setCamera(camera: Camera) {
        if (viewer.isReleased) return
        handle.setCamera(camera)
    }

    /** Returns at once, from any thread. Dropped after the viewer shut down. */
    public fun panBy(dx: Double, dy: Double) {
        if (viewer.isReleased) return
        handle.panBy(dx, dy)
    }

    public suspend fun scrollTo(options: StageScrollToOptions) {
        viewer.call { handle.scrollTo(options) }
    }

    /** Returns at once, from any thread. Dropped after the viewer shut down. */
    public fun scrollBy(options: StageScrollToOptions) {
        if (viewer.isReleased) return
        handle.scrollBy(options)
    }

    /** Returns at once, from any thread. Dropped after the viewer shut down. */
    public fun zoomAround(point: Point, factor: Double) {
        if (viewer.isReleased) return
        handle.zoomAround(point, factor)
    }

    /**
     * Opens a direct-manipulation bracket. [elastic] turns on rubber-band
     * overscroll for it; leaving it out is `false`, as in the engine
     * (`StageCapability::begin_gesture` in
     * `crates/plugins/stage/src/camera/gestures.rs`).
     */
    public suspend fun beginGesture(elastic: Boolean = false) {
        viewer.call { handle.beginGesture(elastic) }
    }

    public suspend fun endGesture() {
        viewer.call { handle.endGesture() }
    }

    // ── navigation ──

    /** An index below zero or past `UInt` throws `invalid-input`. */
    public suspend fun goToPageIndex(index: Int, options: StageGoToOptions? = null) {
        val wire = index.toWireUInt("page index")
        viewer.call { handle.goToPageIndex(wire, options) }
    }

    public suspend fun goToPage(page: PageRef, options: StageGoToOptions? = null) {
        viewer.call { handle.goToPage(page, options) }
    }

    public suspend fun goToFirstPage(options: StageGoToOptions? = null) {
        viewer.call { handle.goToFirstPage(options) }
    }

    public suspend fun goToLastPage(options: StageGoToOptions? = null) {
        viewer.call { handle.goToLastPage(options) }
    }

    public suspend fun canGoNext(): Boolean = viewer.call { handle.canGoNext() }

    public suspend fun canGoPrevious(): Boolean = viewer.call { handle.canGoPrevious() }

    public suspend fun nextPage(options: StageGoToOptions? = null) {
        viewer.call { handle.nextPage(options) }
    }

    public suspend fun previousPage(options: StageGoToOptions? = null) {
        viewer.call { handle.previousPage(options) }
    }

    public suspend fun resetView() {
        viewer.call { handle.resetView() }
    }

    public suspend fun reveal(page: PageRef, options: StageRevealOptions? = null) {
        viewer.call { handle.reveal(page, options) }
    }

    /** An index below zero or past `UInt` throws `invalid-input`. */
    public suspend fun revealIndex(index: Int, options: StageRevealOptions? = null) {
        val wire = index.toWireUInt("page index")
        viewer.call { handle.revealIndex(wire, options) }
    }

    // ── settings ──

    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): StageSettings = viewer.call { handle.settings() }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: StageSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    public suspend fun setResponsiveRules(rules: List<StageResponsiveRule>) {
        viewer.call { handle.setResponsiveRules(rules) }
    }

    public suspend fun matchesRule(name: String): Boolean = viewer.call { handle.matchesRule(name) }

    public suspend fun activeRules(): List<String> = viewer.call { handle.activeRules() }

    public suspend fun applyViewState(view: StageViewState) {
        viewer.call { handle.applyViewState(view) }
    }

    // ── zoom ──

    public suspend fun doubleTapZoom(point: Point) {
        viewer.call { handle.doubleTapZoom(point) }
    }

    public suspend fun zoomIn() {
        viewer.call { handle.zoomIn() }
    }

    public suspend fun zoomOut() {
        viewer.call { handle.zoomOut() }
    }

    public suspend fun zoomTo(target: StageZoomTarget, around: Point? = null) {
        viewer.call { handle.zoomTo(target, around) }
    }

    public suspend fun zoomBy(factor: Double, around: Point? = null) {
        viewer.call { handle.zoomBy(factor, around) }
    }

    public suspend fun fitWidth() {
        viewer.call { handle.fitWidth() }
    }

    public suspend fun fitPage() {
        viewer.call { handle.fitPage() }
    }

    public suspend fun fitAll() {
        viewer.call { handle.fitAll() }
    }

    public suspend fun fitAutomatic() {
        viewer.call { handle.fitAutomatic() }
    }

    public suspend fun setViewRotation(degrees: Double) {
        viewer.call { handle.setViewRotation(degrees) }
    }

    public suspend fun rotateViewBy(delta: Double) {
        viewer.call { handle.rotateViewBy(delta) }
    }

    // ── lifecycle ──

    public suspend fun setViewportSize(size: Size) {
        viewer.call { handle.setViewportSize(size) }
    }

    public suspend fun setDevicePixelRatio(ratio: Double) {
        viewer.call { handle.setDevicePixelRatio(ratio) }
    }

    public suspend fun refit() {
        viewer.call { handle.refit() }
    }

    public suspend fun viewState(): StageViewState = viewer.call { handle.viewState() }

    public suspend fun lensId(): String = viewer.call { handle.lensId() }

    /**
     * [provider] is asked on the viewer's kernel thread when the stage first
     * places this document. Higher [priority] wins.
     */
    public suspend fun provideInitialView(provider: StageInitialViewProvider, priority: Double = 0.0): Registration =
        Registration(viewer.call { handle.provideInitialView(priority, provider) })

    public suspend fun placeInitial() {
        viewer.call { handle.placeInitial() }
    }

    /**
     * Lets a pan tool drag this lens: registers the stage's pan-scroll
     * handler with the document's pointer hub, scoped to [lensId].
     * [panFallback] lets any other tool pan over the gaps between pages;
     * leaving it out is `true`, as in the engine (`create_scroll_handler` in
     * `crates/plugins/stage/src/scroll_handler.rs`). Throws `unsupported`
     * without `ViewerConfig(interaction = true)`.
     */
    public suspend fun registerScrollHandler(panFallback: Boolean = true): Registration =
        Registration(viewer.call { handle.registerScrollHandler(panFallback) })

    /**
     * Page, zoom, camera, motion, settings and viewport events. Completes when
     * the document closes (at once if it already has) or the viewer is
     * destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<StageEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : StageEventListener {
                override fun onEvent(event: StageEvent) {
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
 * The stage capability for [document]. Throws `unsupported` when the viewer
 * was built without the stage plugin, whatever the document's state.
 */
public suspend fun PdfViewer.stage(document: DocumentInfo): StageCapability =
    StageCapability(call { host.stage(document.id) }, this)
