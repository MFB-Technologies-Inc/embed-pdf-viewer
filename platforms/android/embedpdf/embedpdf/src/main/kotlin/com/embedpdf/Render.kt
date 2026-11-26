package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_render.RenderEventListener
import uniffi.epdf_plugin_render.RenderHandle
import uniffi.epdf_plugin_render.RenderPixelFormat
import uniffi.epdf_plugin_render.RenderSnapshotImageHandle
import uniffi.epdf_plugin_render.RenderSnapshotListener
import uniffi.epdf_plugin_render.RenderViewDemandHandle
import uniffi.epdf_plugin_render.RenderedImage

public typealias RenderSettings = uniffi.epdf_plugin_render.RenderSettings
public typealias RenderSettingsPatch = uniffi.epdf_plugin_render.RenderSettingsPatch
public typealias RenderEncodingFormat = uniffi.epdf_plugin_render.RenderEncodingFormat
public typealias RenderTilesSettings = uniffi.epdf_plugin_render.RenderTilesSettings
public typealias RenderTilesPatch = uniffi.epdf_plugin_render.RenderTilesPatch
public typealias RenderTilesPatchOff = uniffi.epdf_plugin_render.RenderTilesPatch.Off
public typealias RenderTilesPatchOn = uniffi.epdf_plugin_render.RenderTilesPatch.On
public typealias RenderTilesSetting = uniffi.epdf_plugin_render.RenderTilesSetting
public typealias RenderTilesSettingOff = uniffi.epdf_plugin_render.RenderTilesSetting.Off
public typealias RenderTilesSettingOn = uniffi.epdf_plugin_render.RenderTilesSetting.On
public typealias RenderTilesOptions = uniffi.epdf_plugin_render.RenderTilesOptions
public typealias RenderPrefetchOptions = uniffi.epdf_plugin_render.RenderPrefetchOptions
public typealias RenderSnapshotSettings = uniffi.epdf_plugin_render.RenderSnapshotSettings
public typealias RenderSnapshotPatch = uniffi.epdf_plugin_render.RenderSnapshotPatch
public typealias RenderSnapshotBox = uniffi.epdf_plugin_render.RenderSnapshotBox

public typealias RenderConfig = uniffi.epdf_plugin_render.RenderConfig
public typealias RenderFullPageOptions = uniffi.epdf_plugin_render.RenderFullPageOptions
public typealias RenderQuantize = uniffi.epdf_plugin_render.RenderQuantize
public typealias RenderQuantizeExact = uniffi.epdf_plugin_render.RenderQuantize.Exact
public typealias RenderQuantizeLadder = uniffi.epdf_plugin_render.RenderQuantize.Ladder
public typealias RenderPageOptions = uniffi.epdf_plugin_render.RenderPageOptions
public typealias RenderRotation = uniffi.epdf_plugin_render.RenderRotation
public typealias RenderInvalidateScope = uniffi.epdf_plugin_render.RenderInvalidateScope
public typealias RenderBatchFailure = uniffi.epdf_plugin_render.RenderBatchFailure
public typealias RenderEvent = uniffi.epdf_plugin_render.RenderEvent
public typealias RenderEventInvalidated = uniffi.epdf_plugin_render.RenderEvent.Invalidated
public typealias RenderEventCompleted = uniffi.epdf_plugin_render.RenderEvent.Completed
public typealias RenderEventFailed = uniffi.epdf_plugin_render.RenderEvent.Failed
public typealias RenderPolicy = uniffi.epdf_plugin_render.RenderPolicy
public typealias RenderPolicyContinuous = uniffi.epdf_plugin_render.RenderPolicy.Continuous
public typealias RenderPolicyLattice = uniffi.epdf_plugin_render.RenderPolicy.Lattice
public typealias RenderLatticePolicy = uniffi.epdf_plugin_render.RenderLatticePolicy
public typealias RenderViewport = uniffi.epdf_plugin_render.RenderViewport
public typealias RenderViewportScale = uniffi.epdf_plugin_render.RenderViewport.Scale
public typealias RenderViewportWidth = uniffi.epdf_plugin_render.RenderViewport.Width
public typealias RenderViewportThumbnail = uniffi.epdf_plugin_render.RenderViewport.Thumbnail
public typealias RenderPaintSettings = uniffi.epdf_plugin_render.RenderPaintSettings
public typealias RenderPageViewDemand = uniffi.epdf_plugin_render.RenderPageViewDemand
public typealias RenderTileSource = uniffi.epdf_plugin_render.RenderTileSource
public typealias RenderTilePlan = uniffi.epdf_plugin_render.RenderTilePlan

/**
 * A rendered page. [image] owns its pixels: it outlives the document and the
 * viewer.
 *
 * @property scale device pixels per PDF point across the rendered region's
 *   rotated width.
 */
public class RenderedPage internal constructor(
    public val width: Int,
    public val height: Int,
    public val scale: Double,
    public val image: PageImage,
) {
    internal companion object {
        /** Takes the engine's pixels without copying them; the engine hands each image over once. */
        fun from(rendered: RenderedImage): RenderedPage {
            val image = PageImage.from(rendered)
            return RenderedPage(image.width, image.height, rendered.scale, image)
        }
    }
}

/** One page of a batch. */
public class RenderedPageResult internal constructor(public val page: PageRef, public val rendered: RenderedPage)

/** A batch, best effort per page, each list in input order. */
public class RenderBatchResult internal constructor(
    public val applied: List<RenderedPageResult>,
    public val failed: List<RenderBatchFailure>,
)

/**
 * A capture's immediate metadata and separate image wait. Keep the viewer
 * alive until the image resolves; returned pixels outlive it.
 */
public class RenderSnapshot internal constructor(
    snapshot: uniffi.epdf_plugin_render.RenderSnapshot,
    private val viewer: PdfViewer,
) {
    public val page: PageRef = snapshot.page
    public val rect: Rect = snapshot.rect
    private val handle: RenderSnapshotImageHandle = snapshot.image

    /**
     * Cancelling the calling coroutine ends only its wait. Clearing the
     * snapshot or closing the document cancels a pending capture for every
     * reader.
     */
    public suspend fun image(): RenderedPage =
        RenderedPage.from(viewer.callCancellable { token -> handle.image(token) })
}

/**
 * A document's page rasters. Get one with [PdfViewer.render]; the viewer
 * needs `ViewerConfig(render = RenderConfig())`. It keeps the viewer alive.
 * After its document closes, every call throws `instance-closed`.
 *
 * A picture's layers, `includeAnnotations` and `includeFormFields`, default to
 * [ContentInclusion.WHEN_PERMITTED]: annotations when the session may read
 * them (`doc.annotate.read`), form fields when it may read them
 * (`doc.forms.read`) and annotations are not [ContentInclusion.NEVER]. That is
 * the engine's rule, `resolve_page_layers` in
 * `crates/plugins/render/src/model.rs`. A call that renders throws
 * `permission-denied` for [ContentInclusion.ALWAYS] on a layer the session may
 * not read; [renderEpoch], [sourceKey] and [RenderViewDemand.setDemand] leave
 * that layer out instead.
 */
public class RenderCapability internal constructor(
    private val handle: RenderHandle,
    internal val viewer: PdfViewer,
) {
    /**
     * Current settings shared by every document using this plugin
     * registration. Encoding preferences are stored; page images stay raw
     * pixels and this host provides no encoder.
     */
    public suspend fun settings(): RenderSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: RenderSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun canSnapshot(): Boolean = viewer.call { handle.canSnapshot() }

    /** Reads the existing capture without starting a render. */
    public suspend fun snapshot(): RenderSnapshot? = viewer.call { handle.snapshot() }?.let { RenderSnapshot(it, viewer) }

    public suspend fun clearSnapshot() {
        viewer.call { handle.clearSnapshot() }
    }

    /** The capture tool's dragged or held box in page points. */
    public suspend fun snapshotBox(page: PageRef): Rect? = viewer.call { handle.snapshotBox(page) }

    /**
     * Captures, and clears as `null`. Completes when the document closes. Each
     * collection is a new subscription.
     */
    public fun observeSnapshotChanges(): Flow<RenderSnapshot?> = viewer.registry.stream { channel ->
        handle.subscribeSnapshots(
            object : RenderSnapshotListener {
                override fun onSnapshot(snapshot: uniffi.epdf_plugin_render.RenderSnapshot?) {
                    channel.trySend(snapshot?.let { RenderSnapshot(it, viewer) })
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }

    /** Whether page rasters would be served to this session (`doc.render`). */
    public suspend fun canRender(): Boolean = viewer.call { handle.canRender() }

    /**
     * Whether the pages' thumbnails would be served to this session
     * (`doc.render.thumbnail`, which `doc.render` includes). A session that
     * may not render pages may still have them.
     */
    public suspend fun canRenderThumbnail(): Boolean = viewer.call { handle.canRenderThumbnail() }

    /**
     * Renders one page at exactly the size asked for: `width` device pixels,
     * or `scale` device pixels a point (1 when neither is given). Throws
     * `invalid-input` for a size that is not a positive number and `not-found`
     * for a page not in this document. Cancelling the calling coroutine
     * cancels the render.
     */
    public suspend fun renderPage(page: PageRef, options: RenderPageOptions = RenderPageOptions()): RenderedPage =
        measured("renderPage") {
            RenderedPage.from(viewer.callCancellable { token -> handle.renderPage(page, options, token) })
        }

    /**
     * The page's thumbnail: the whole page, 512 pixels on its long side,
     * sharing the raster cache. Needs only `doc.render.thumbnail`; throws
     * `permission-denied` without it, otherwise like [renderPage].
     */
    public suspend fun renderThumbnail(
        page: PageRef,
        includeAnnotations: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        includeFormFields: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
    ): RenderedPage = RenderedPage.from(
        viewer.callCancellable { token ->
            handle.renderThumbnail(page, includeAnnotations.engineValue, includeFormFields.engineValue, token)
        },
    )

    /**
     * Renders several pages, [concurrency] at a time. The default, 4, is the
     * engine's (`DEFAULT_BATCH_CONCURRENCY` in
     * `crates/plugins/render/src/controller.rs`), and the engine runs 0 as 1.
     * A page that fails is in `failed`; cancelling the calling coroutine
     * cancels the whole batch. A [concurrency] below zero throws
     * `invalid-input`.
     */
    public suspend fun renderPages(
        pages: List<PageRef>,
        options: RenderPageOptions = RenderPageOptions(),
        concurrency: Int = 4,
    ): RenderBatchResult {
        val wire = concurrency.toWireUInt("concurrency")
        val batch = viewer.callCancellable { token -> handle.renderPages(pages, options, wire, token) }
        return RenderBatchResult(
            batch.applied.map { RenderedPageResult(it.page, RenderedPage.from(it.image)) },
            batch.failed,
        )
    }

    /** The version of the raster these options would produce. It goes up when the page's pixels change. */
    public suspend fun renderEpoch(
        page: PageRef,
        includeAnnotations: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        includeFormFields: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
    ): Long = viewer.call {
        handle.renderEpoch(page, includeAnnotations.engineValue, includeFormFields.engineValue)
    }.toLongResult("render epoch")

    /** Declares that page pixels changed: every page when [pages] is `null`, `CONTENT` when [scope] is. */
    public suspend fun invalidate(pages: List<PageRef>? = null, scope: RenderInvalidateScope? = null) {
        viewer.call { handle.invalidate(pages, scope) }
    }

    public suspend fun renderPolicy(): RenderPolicy = viewer.call { handle.renderPolicy() }

    /**
     * Renders at the scale a view shows the page (a stage page transform's
     * `renderScale`), conformed through the policy's render points. With
     * [thumbnail], the page's thumbnail instead ([scale] is ignored), which
     * needs only `doc.render.thumbnail`. Leaving [thumbnail] out is `false`,
     * as in the engine (`RenderHandle::render_source` in
     * `crates/plugins/render/src/ffi.rs`).
     */
    public suspend fun renderSource(
        page: PageRef,
        scale: Double,
        includeAnnotations: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        includeFormFields: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        thumbnail: Boolean = false,
    ): RenderedPage = RenderedPage.from(
        viewer.callCancellable { token ->
            handle.renderSource(page, scale, includeAnnotations.engineValue, includeFormFields.engineValue, thumbnail, token)
        },
    )

    /**
     * The identity of the raster [renderSource] would produce, the thumbnail's
     * with [thumbnail]. Leaving [thumbnail] out is `false`, as in the engine
     * (`RenderHandle::source_key` in `crates/plugins/render/src/ffi.rs`).
     */
    public suspend fun sourceKey(
        page: PageRef,
        scale: Double,
        includeAnnotations: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        includeFormFields: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        thumbnail: Boolean = false,
    ): String = viewer.call {
        handle.sourceKey(page, scale, includeAnnotations.engineValue, includeFormFields.engineValue, thumbnail)
    }

    public suspend fun conformViewport(page: PageRef, scale: Double): RenderViewport =
        viewer.call { handle.conformViewport(page, scale) }

    public suspend fun paintSettings(): RenderPaintSettings = viewer.call { handle.paintSettings() }

    /** A view's tile surface. Pair with [RenderViewDemand.dispose]. */
    public suspend fun viewDemand(viewId: String): RenderViewDemand =
        RenderViewDemand(viewer.call { handle.createViewDemand(viewId) }, viewer)

    /**
     * Invalidations, and rasters resolving or failing. Completes when the
     * document closes (at once if it already has) or the viewer is destroyed.
     * Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<RenderEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : RenderEventListener {
                override fun onEvent(event: RenderEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/** One view's tile plane over a document's pages. */
public class RenderViewDemand internal constructor(
    private val handle: RenderViewDemandHandle,
    private val viewer: PdfViewer,
) {
    /** What this view wants for a page right now. The layers follow [RenderCapability]'s rule. */
    public suspend fun setDemand(
        page: PageRef,
        demand: RenderPageViewDemand,
        includeAnnotations: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
        includeFormFields: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
    ) {
        viewer.call { handle.setDemand(page, demand, includeAnnotations.engineValue, includeFormFields.engineValue) }
    }

    public suspend fun plan(page: PageRef): RenderTilePlan = viewer.call { handle.plan(page) }

    /** `null` for a key the current plan doesn't paint. */
    public suspend fun tileImage(page: PageRef, key: String): RenderedPage? =
        viewer.call { handle.tileImage(page, key) }?.let(RenderedPage::from)

    public suspend fun markPainted(page: PageRef, key: String) {
        viewer.call { handle.markPainted(page, key) }
    }

    public suspend fun markUnpainted(page: PageRef, key: String) {
        viewer.call { handle.markUnpainted(page, key) }
    }

    public suspend fun release(page: PageRef) {
        viewer.call { handle.release(page) }
    }

    public suspend fun dispose() {
        viewer.call { handle.dispose() }
    }
}

/**
 * The render capability for [document]. Throws `unsupported` when the viewer
 * was built without the render plugin, whatever the document's state.
 */
public suspend fun PdfViewer.render(document: DocumentInfo): RenderCapability =
    RenderCapability(call { host.render(document.id) }, this)

/** The engine's one pixel format, checked so a format added later fails loudly rather than drawing wrong colors. */
internal fun RenderPixelFormat.checkRgba8() {
    when (this) {
        RenderPixelFormat.RGBA8 -> Unit
    }
}
