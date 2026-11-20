using GR = uniffi.epdf_plugin_render;

namespace EmbedPDF;

/// <summary>
/// A document's page rasters. Get one with <see cref="PdfViewer.GetRenderAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Render"/>. After its document
/// closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// A picture's layers, annotations and form fields, default to
/// <see cref="ContentInclusion.WhenPermitted"/>: annotations when the session
/// may read them (<c>doc.annotate.read</c>), form fields when it may read them
/// (<c>doc.forms.read</c>) and annotations are not
/// <see cref="ContentInclusion.Never"/>. That is the engine's rule. A call
/// that renders throws <see cref="PdfErrorCode.PermissionDenied"/> for
/// <see cref="ContentInclusion.Always"/> on a layer the session may not read;
/// <see cref="GetRenderEpochAsync"/>, <see cref="GetSourceKeyAsync"/> and
/// <see cref="RenderViewDemand.SetDemandAsync"/> leave that layer out instead.
/// </remarks>
public sealed class RenderCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GR.RenderHandle handle;

    internal RenderCapability(PdfViewer viewer, GR.RenderHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration. Encoding preferences are stored; page images stay raw pixels.</summary>
    public async Task<RenderSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields. Clear flags remove nullable values; omission preserves them.</summary>
    public Task UpdateSettingsAsync(RenderSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(patch);
            return handle.UpdateSettings(ffi);
        }, cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether the snapshot tool may capture.</summary>
    public Task<bool> CanSnapshotAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanSnapshot(), cancellationToken);

    /// <summary>The existing capture, read without starting a render.</summary>
    public async Task<RenderSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.Snapshot(), cancellationToken).ConfigureAwait(false) is { } snapshot ? new RenderSnapshot(viewer, snapshot) : null;

    /// <summary>Drops the capture.</summary>
    public Task ClearSnapshotAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ClearSnapshot(), cancellationToken);

    /// <summary>The capture tool's dragged or held box on <paramref name="page"/>, in page points.</summary>
    public async Task<PdfRect?> GetSnapshotBoxAsync(PageRef page, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.SnapshotBox(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Captures, and clears as <see langword="null"/>, subscribed when this
    /// returns. Ends when the document closes, when the viewer is disposed or
    /// when <paramref name="cancellationToken"/> fires. Enumerate it once.
    /// </summary>
    public IAsyncEnumerable<RenderSnapshot?> ObserveSnapshotChanges(CancellationToken cancellationToken = default)
    {
        var relay = new SnapshotRelay(viewer);
        return viewer.Stream(relay, _ => handle.SubscribeSnapshots(relay), cancellationToken);
    }

    /// <summary>Whether page rasters would be served to this session (<c>doc.render</c>).</summary>
    public Task<bool> CanRenderAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRender(), cancellationToken);

    /// <summary>Whether thumbnails would be served (<c>doc.render.thumbnail</c>, which <c>doc.render</c> includes).</summary>
    public Task<bool> CanRenderThumbnailAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRenderThumbnail(), cancellationToken);

    /// <summary>
    /// Renders one page at exactly the size asked for: <see cref="RenderPageOptions.Width"/>
    /// device pixels, or <see cref="RenderPageOptions.Scale"/> device pixels a
    /// point (1 when neither is given). Throws <see cref="PdfErrorCode.InvalidInput"/>
    /// for a size that is not a positive number, <see cref="PdfErrorCode.NotFound"/>
    /// for a page not in this document, and <see cref="OperationCanceledException"/>
    /// when the token fires.
    /// </summary>
    public Task<RenderedPage> RenderPageAsync(PageRef page, RenderPageOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Telemetry.Measured("renderPage", async () =>
        {
            var ffiPage = Ffi.ToFfi(page);
            var ffi = Ffi.ToFfi(options ?? new RenderPageOptions());
            return Ffi.ToPublic(await viewer.RunCancellable(token => handle.RenderPage(ffiPage, ffi, token), cancellationToken).ConfigureAwait(false));
        });
    }

    /// <summary>
    /// The page's thumbnail: the whole page, 512 pixels on its long side,
    /// sharing the raster cache. Needs only <c>doc.render.thumbnail</c>;
    /// otherwise like <see cref="RenderPageAsync"/>.
    /// </summary>
    public async Task<RenderedPage> RenderThumbnailAsync(
        PageRef page, ContentInclusion includeAnnotations = ContentInclusion.WhenPermitted,
        ContentInclusion includeFormFields = ContentInclusion.WhenPermitted, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        var (annotations, fields) = (includeAnnotations.ToEngine(), includeFormFields.ToEngine());
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.RenderThumbnail(ffi, annotations, fields, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Renders several pages, <paramref name="concurrency"/> at a time. The
    /// default, 4, is the engine's, and the engine runs 0 as 1. A page that
    /// fails is in <see cref="RenderBatchResult.Failed"/>; cancelling throws
    /// for the whole batch. A negative <paramref name="concurrency"/> throws
    /// <see cref="PdfErrorCode.InvalidInput"/>.
    /// </summary>
    public async Task<RenderBatchResult> RenderPagesAsync(
        IEnumerable<PageRef> pages, RenderPageOptions? options = null, int concurrency = 4, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var wire = WireInteger.ToUInt32(concurrency, "concurrency");
        var ffiPages = Ffi.ToFfi(pages);
        var ffi = Ffi.ToFfi(options ?? new RenderPageOptions());
        var batch = await viewer.RunCancellable(token => handle.RenderPages(ffiPages, ffi, wire, token), cancellationToken).ConfigureAwait(false);
        return new(
            [.. batch.Applied.Select(entry => new RenderedPageResult(Ffi.ToPublic(entry.Page), Ffi.ToPublic(entry.Image)))],
            [.. batch.Failed.Select(failure => new RenderBatchFailure(Ffi.ToPublic(failure.Page), Ffi.ToPublic(failure.Error)))]);
    }

    /// <summary>The version of the raster these layers would produce. It goes up when the page's pixels change.</summary>
    public async Task<long> GetRenderEpochAsync(
        PageRef page, ContentInclusion includeAnnotations = ContentInclusion.WhenPermitted,
        ContentInclusion includeFormFields = ContentInclusion.WhenPermitted, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        var (annotations, fields) = (includeAnnotations.ToEngine(), includeFormFields.ToEngine());
        return WireInteger.ToLong(await viewer.Run(() => handle.RenderEpoch(ffi, annotations, fields), cancellationToken).ConfigureAwait(false), "render epoch");
    }

    /// <summary>Declares that page pixels changed: every page when <paramref name="pages"/> is <see langword="null"/>, <see cref="RenderInvalidateScope.Content"/> when <paramref name="scope"/> is.</summary>
    public Task InvalidateAsync(IEnumerable<PageRef>? pages = null, RenderInvalidateScope? scope = null, CancellationToken cancellationToken = default)
    {
        return viewer.Run(() =>
        {
            var ffi = pages is null ? null : Ffi.ToFfi(pages);
            GR.RenderInvalidateScope? ffiScope = scope is { } s ? Ffi.ToFfi(s) : null;
            return handle.Invalidate(ffi, ffiScope);
        }, cancellationToken);
    }

    /// <summary>How render sizes are chosen.</summary>
    public async Task<RenderPolicy> GetRenderPolicyAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.RenderPolicy(), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Renders at the scale a view shows the page, conformed through the
    /// policy's render points. With <paramref name="thumbnail"/>, the page's
    /// thumbnail instead (<paramref name="scale"/> is ignored), which needs
    /// only <c>doc.render.thumbnail</c>.
    /// </summary>
    public async Task<RenderedPage> RenderSourceAsync(
        PageRef page, double scale, ContentInclusion includeAnnotations = ContentInclusion.WhenPermitted,
        ContentInclusion includeFormFields = ContentInclusion.WhenPermitted, bool thumbnail = false, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        var (annotations, fields) = (includeAnnotations.ToEngine(), includeFormFields.ToEngine());
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.RenderSource(ffi, scale, annotations, fields, thumbnail, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The identity of the raster <see cref="RenderSourceAsync"/> would produce, the thumbnail's with <paramref name="thumbnail"/>.</summary>
    public Task<string> GetSourceKeyAsync(
        PageRef page, double scale, ContentInclusion includeAnnotations = ContentInclusion.WhenPermitted,
        ContentInclusion includeFormFields = ContentInclusion.WhenPermitted, bool thumbnail = false, CancellationToken cancellationToken = default)
    {
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(page);
            var (annotations, fields) = (includeAnnotations.ToEngine(), includeFormFields.ToEngine());
            return handle.SourceKey(ffi, scale, annotations, fields, thumbnail);
        }, cancellationToken);
    }

    /// <summary>The size a view showing <paramref name="page"/> at <paramref name="scale"/> should render.</summary>
    public async Task<RenderViewport> ConformViewportAsync(PageRef page, double scale, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        return Ffi.ToPublic(await viewer.Run(() => handle.ConformViewport(ffi, scale), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Paint settings a view reads.</summary>
    public async Task<RenderPaintSettings> GetPaintSettingsAsync(CancellationToken cancellationToken = default) =>
        new((await viewer.Run(() => handle.PaintSettings(), cancellationToken).ConfigureAwait(false)).Tiles);

    /// <summary>A view's tile surface. Dispose it when the view goes away.</summary>
    public async Task<RenderViewDemand> CreateViewDemandAsync(string viewId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewId);
        return new(viewer, await viewer.Run(() => handle.CreateViewDemand(viewId), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Invalidations, and rasters resolving or failing, subscribed when this
    /// returns. Ends when the document closes (at once if it already has),
    /// when the viewer is disposed, or when <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<RenderEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GR.RenderEvent, RenderEvent>(Ffi.ToPublic), GR.RenderEventListener
    {
        public void OnEvent(GR.RenderEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    sealed class SnapshotRelay(PdfViewer viewer)
        : EventRelay<GR.RenderSnapshot?, RenderSnapshot?>(snapshot => snapshot is null ? null : new RenderSnapshot(viewer, snapshot)), GR.RenderSnapshotListener
    {
        public void OnSnapshot(GR.RenderSnapshot? snapshot) => Deliver(snapshot);

        public void OnEnd() => Finish();
    }
}

/// <summary>
/// A capture's immediate metadata and a separate wait for its pixels. Keep the
/// viewer alive until the image resolves; the returned pixels outlive it.
/// </summary>
public sealed class RenderSnapshot : IDisposable
{
    readonly PdfViewer viewer;
    readonly GR.RenderSnapshotImageHandle image;

    internal RenderSnapshot(PdfViewer viewer, GR.RenderSnapshot snapshot)
    {
        this.viewer = viewer;
        Page = Ffi.ToPublic(snapshot.Page);
        Rect = Ffi.ToPublic(snapshot.Rect);
        image = snapshot.Image;
    }

    /// <summary>The captured page.</summary>
    public PageRef Page { get; }

    /// <summary>The captured region, in page points.</summary>
    public PdfRect Rect { get; }

    /// <summary>
    /// The capture's pixels. Cancelling the token ends only this wait;
    /// clearing the snapshot or closing the document cancels a pending capture
    /// for every reader.
    /// </summary>
    public async Task<RenderedPage> GetImageAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.RunCancellable(token => image.Image(token), cancellationToken).ConfigureAwait(false));

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => image.Dispose();
}

/// <summary>One view's tile plane over a document's pages. Dispose it when the view goes away.</summary>
public sealed class RenderViewDemand : IAsyncDisposable
{
    readonly PdfViewer viewer;
    readonly GR.RenderViewDemandHandle handle;

    internal RenderViewDemand(PdfViewer viewer, GR.RenderViewDemandHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>What this view wants for a page right now. The layers follow <see cref="RenderCapability"/>'s rule.</summary>
    public Task SetDemandAsync(
        PageRef page, RenderPageViewDemand demand, ContentInclusion includeAnnotations = ContentInclusion.WhenPermitted,
        ContentInclusion includeFormFields = ContentInclusion.WhenPermitted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demand);
        return viewer.Run(() =>
        {
            var (ffiPage, ffi) = (Ffi.ToFfi(page), Ffi.ToFfi(demand));
            var (annotations, fields) = (includeAnnotations.ToEngine(), includeFormFields.ToEngine());
            return handle.SetDemand(ffiPage, ffi, annotations, fields);
        }, cancellationToken);
    }

    /// <summary>What to paint for a page.</summary>
    public async Task<RenderTilePlan> GetPlanAsync(PageRef page, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(page);
        return Ffi.ToPublic(await viewer.Run(() => handle.Plan(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A planned tile's pixels; <see langword="null"/> for a key the current plan doesn't paint.</summary>
    public async Task<RenderedPage?> GetTileImageAsync(PageRef page, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var ffi = Ffi.ToFfi(page);
        return await viewer.Run(() => handle.TileImage(ffi, key), cancellationToken).ConfigureAwait(false) is { } image ? Ffi.ToPublic(image) : null;
    }

    /// <summary>The view drew this tile.</summary>
    public Task MarkPaintedAsync(PageRef page, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(page);
            return handle.MarkPainted(ffi, key);
        }, cancellationToken);
    }

    /// <summary>The view no longer shows this tile.</summary>
    public Task MarkUnpaintedAsync(PageRef page, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(page);
            return handle.MarkUnpainted(ffi, key);
        }, cancellationToken);
    }

    /// <summary>The view no longer shows the page.</summary>
    public Task ReleaseAsync(PageRef page, CancellationToken cancellationToken = default)
    {
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(page);
            return handle.Release(ffi);
        }, cancellationToken);
    }

    /// <summary>
    /// Releases the view's demand in the engine, then the native handle. A
    /// second call, or one after the document or viewer closed, does nothing.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await viewer.Run(() => handle.DisposeDemand(), default).ConfigureAwait(false);
        }
        catch (PdfException error) when (error.Code == PdfErrorCode.InstanceClosed)
        {
            // Already gone with its document or viewer.
        }
        handle.Dispose();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's render capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Render"/>.</summary>
    public async Task<RenderCapability> GetRenderAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Render(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
