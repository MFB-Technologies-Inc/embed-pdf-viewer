using GF = uniffi.epdf_ffi;
using GSt = uniffi.epdf_plugin_stage;

namespace EmbedPDF;

/// <summary>
/// A document's viewport lens: camera, zoom, scroll, layout and navigation.
/// Get one with <see cref="PdfViewer.GetStageAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Stage"/>. After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// Everything is in viewport points (the view's own coordinates, top-left
/// origin) or page points, as <see cref="double"/>, never rounded. A view
/// drives the stage with <see cref="SetViewportSizeAsync"/>, the scroll
/// position (<see cref="ScrollToAsync"/>, <see cref="GetScrollMetricsAsync"/>)
/// and pointer samples through <see cref="InteractionCapability.DispatchPointer"/>
/// once <see cref="RegisterScrollHandlerAsync"/> has run.
/// <see cref="SetCamera"/>, <see cref="PanBy"/>, <see cref="ScrollBy"/> and
/// <see cref="ZoomAround"/> return at once and run in order; their errors
/// arrive on <see cref="PdfViewer.ObserveReportedErrors"/>.
/// </remarks>
public sealed class StageCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GSt.StageHandle handle;

    internal StageCapability(PdfViewer viewer, GSt.StageHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    // Read

    /// <summary>The document's page count.</summary>
    public async Task<int> GetPageCountAsync(CancellationToken cancellationToken = default) =>
        WireInteger.ToInt(await viewer.Run(() => handle.PageCount(), cancellationToken).ConfigureAwait(false), "page count");

    /// <summary>Where the view is, as a destination a link or a bookmark could hold.</summary>
    public async Task<PageDestination?> GetCurrentDestinationAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.CurrentDestination(), cancellationToken).ConfigureAwait(false) is { } d ? Ffi.ToPublic(d) : null;

    /// <summary>Goes to a destination; <paramref name="behavior"/> <see langword="null"/> is the settings'.</summary>
    public Task GoToDestinationAsync(PageDestination destination, StageScrollBehavior? behavior = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return viewer.Run(() => handle.GoToDestination(Ffi.ToStageFfi(destination), Ffi.ToFfiOrNull(behavior)), cancellationToken);
    }

    /// <summary>Whether <see cref="ZoomInAsync"/> would change the zoom.</summary>
    public Task<bool> CanZoomInAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanZoomIn(), cancellationToken);

    /// <summary>Whether <see cref="ZoomOutAsync"/> would change the zoom.</summary>
    public Task<bool> CanZoomOutAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanZoomOut(), cancellationToken);

    /// <summary>The camera.</summary>
    public async Task<Camera> GetCameraAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Camera(), cancellationToken).ConfigureAwait(false));

    /// <summary>The viewport size in viewport points.</summary>
    public async Task<PdfSize> GetViewportSizeAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ViewportSize(), cancellationToken).ConfigureAwait(false));

    /// <summary>The stage as a scroll view sees it.</summary>
    public async Task<StageScrollMetrics> GetScrollMetricsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ScrollMetrics(), cancellationToken).ConfigureAwait(false));

    /// <summary>The pages at least partly in the viewport, in order.</summary>
    public async Task<IReadOnlyList<StageVisiblePage>> GetVisiblePagesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.VisiblePages(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Whether any of <paramref name="page"/> is in the viewport.</summary>
    public Task<bool> IsPageVisibleAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.IsPageVisible(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>The current page's index.</summary>
    public async Task<int> GetCurrentPageIndexAsync(CancellationToken cancellationToken = default) =>
        WireInteger.ToInt(await viewer.Run(() => handle.CurrentPageIndex(), cancellationToken).ConfigureAwait(false), "current page index");

    /// <summary>The current page, if the document has one.</summary>
    public async Task<PageRef?> GetCurrentPageAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.CurrentPage(), cancellationToken).ConfigureAwait(false) is { } page ? Ffi.ToPublic(page) : null;

    /// <summary>The pages of the current item (a spread or a page).</summary>
    public async Task<IReadOnlyList<PageInfo>> GetCurrentItemPagesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.CurrentItemPages(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Pages navigation steps past: the current item when it fits across the viewport, otherwise the current page alone.</summary>
    public async Task<IReadOnlyList<PageInfo>> GetCurrentPagesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.CurrentPages(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Starts the zoom tool's box at a viewport point.</summary>
    public Task BeginZoomDragAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.BeginZoomDrag(Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Moves the zoom tool's box corner.</summary>
    public Task MoveZoomDragAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.MoveZoomDrag(Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Zooms into the box, or one step for a click (out with <paramref name="zoomOut"/>).</summary>
    public Task EndZoomDragAsync(bool zoomOut = false, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.EndZoomDrag(zoomOut), cancellationToken);

    /// <summary>Drops the zoom tool's box without zooming.</summary>
    public Task CancelZoomDragAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CancelZoomDrag(), cancellationToken);

    /// <summary>The zoom tool's box in viewport points, once it passes its threshold.</summary>
    public async Task<PdfRect?> GetZoomBoxAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.ZoomBox(), cancellationToken).ConfigureAwait(false));

    /// <summary>Where <paramref name="page"/> is laid out, visible or not.</summary>
    public async Task<StageVisiblePage?> GetPageFrameAsync(PageRef page, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PageFrame(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false) is { } frame ? Ffi.ToPublic(frame) : null;

    /// <summary>The page under a viewport point.</summary>
    public async Task<StagePageHit?> GetPageAtAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PageAt(Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } hit ? Ffi.ToPublic(hit) : null;

    /// <summary>A viewport point in <paramref name="page"/>'s space.</summary>
    public async Task<PdfPoint?> ViewportToPageAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.ViewportToPage(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } p ? Ffi.ToPublic(p) : null;

    /// <summary>A page point in world space.</summary>
    public async Task<PdfPoint?> PageToWorldAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PageToWorld(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } p ? Ffi.ToPublic(p) : null;

    /// <summary>A page rectangle in viewport points.</summary>
    public async Task<PdfRect?> PageRectToViewportAsync(PageRef page, PdfRect rect, CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.PageRectToViewport(Ffi.ToFfi(page), Ffi.ToFfi(rect)), cancellationToken).ConfigureAwait(false));

    /// <summary>A world point in viewport points.</summary>
    public async Task<PdfPoint> WorldToViewportAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.WorldToViewport(Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false));

    /// <summary>A viewport point in world space.</summary>
    public async Task<PdfPoint> ViewportToWorldAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ViewportToWorld(Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false));

    /// <summary>A page point in viewport points.</summary>
    public async Task<PdfPoint?> PageToViewportAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PageToViewport(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } p ? Ffi.ToPublic(p) : null;

    /// <summary>The view rotation in degrees.</summary>
    public Task<double> GetViewRotationAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ViewRotation(), cancellationToken);

    /// <summary>The zoom level.</summary>
    public Task<double> GetZoomLevelAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ZoomLevel(), cancellationToken);

    /// <summary>How the current zoom was reached.</summary>
    public async Task<StageZoomMode> GetZoomModeAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ZoomMode(), cancellationToken).ConfigureAwait(false));

    /// <summary>The anchor and zoom of the current view.</summary>
    public async Task<StageViewpoint> GetViewpointAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Viewpoint(), cancellationToken).ConfigureAwait(false));

    // Motion

    /// <summary>Starts a fling at a velocity in viewport points per second.</summary>
    public Task FlingAsync(double velocityX, double velocityY, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.Fling(velocityX, velocityY), cancellationToken);

    /// <summary>Whether a tween or a fling is running.</summary>
    public Task<bool> IsMovingAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsMoving(), cancellationToken);

    /// <summary>Stops a tween or a fling where it is.</summary>
    public Task StopMotionAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.StopMotion(), cancellationToken);

    /// <summary>Sets the camera. Returns at once; after the document closes it does nothing.</summary>
    public void SetCamera(Camera camera)
    {
        var ffi = Ffi.ToFfi(camera);
        Fire(() => handle.SetCamera(ffi));
    }

    /// <summary>Pans by a delta in viewport points. Returns at once; after the document closes it does nothing.</summary>
    public void PanBy(double dx, double dy) => Fire(() => handle.PanBy(dx, dy));

    /// <summary>Scrolls to a position in viewport points.</summary>
    public Task ScrollToAsync(StageScrollToOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return viewer.Run(() => handle.ScrollTo(Ffi.ToFfi(options)), cancellationToken);
    }

    /// <summary>Scrolls by a delta in viewport points. Returns at once; an undefined behavior throws <see cref="PdfErrorCode.InvalidInput"/> here.</summary>
    public void ScrollBy(StageScrollToOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var ffi = Ffi.ToFfi(options);
        Fire(() => handle.ScrollBy(ffi));
    }

    /// <summary>Zooms by <paramref name="factor"/> keeping a viewport point fixed. Returns at once.</summary>
    public void ZoomAround(PdfPoint point, double factor)
    {
        var ffi = Ffi.ToFfi(point);
        Fire(() => handle.ZoomAround(ffi, factor));
    }

    /// <summary>
    /// Pans by a wheel's delta (viewport points, signed like <see cref="PanBy"/>)
    /// at <paramref name="timeMs"/> on a clock that only goes forward.
    /// <see langword="false"/>: the view should leave this wheel to the scroll
    /// view around it (<see cref="StageSettings.ScrollChaining"/>).
    /// </summary>
    public Task<bool> WheelByAsync(double dx, double dy, double timeMs, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.WheelBy(dx, dy, timeMs), cancellationToken);

    /// <summary>Opens a direct-manipulation bracket; <paramref name="elastic"/> turns on rubber-band overscroll for it.</summary>
    public Task BeginGestureAsync(bool elastic = false, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.BeginGesture(elastic), cancellationToken);

    /// <summary>Closes the bracket and settles.</summary>
    public Task EndGestureAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.EndGesture(), cancellationToken);

    // Navigation

    /// <summary>Goes to a page by index. An index below zero throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task GoToPageIndexAsync(int index, StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.GoToPageIndex(WireInteger.ToUInt32(index, "page index"), Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Goes to a page.</summary>
    public Task GoToPageAsync(PageRef page, StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.GoToPage(Ffi.ToFfi(page), Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Goes to the first page.</summary>
    public Task GoToFirstPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.GoToFirstPage(Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Goes to the last page.</summary>
    public Task GoToLastPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.GoToLastPage(Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Whether <see cref="NextPageAsync"/> would move.</summary>
    public Task<bool> CanGoNextAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanGoNext(), cancellationToken);

    /// <summary>Whether <see cref="PreviousPageAsync"/> would move.</summary>
    public Task<bool> CanGoPreviousAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanGoPrevious(), cancellationToken);

    /// <summary>Steps past the current pages.</summary>
    public Task NextPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.NextPage(Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Steps back past the current pages.</summary>
    public Task PreviousPageAsync(StageGoToOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.PreviousPage(Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Back to the initial placement.</summary>
    public Task ResetViewAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetView(), cancellationToken);

    /// <summary>Shows <paramref name="page"/>, or a rectangle on it.</summary>
    public Task RevealAsync(PageRef page, StageRevealOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.Reveal(Ffi.ToFfi(page), Ffi.ToFfiOrNull(options)), cancellationToken);

    /// <summary>Shows a page by index. An index below zero throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task RevealIndexAsync(int index, StageRevealOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.RevealIndex(WireInteger.ToUInt32(index, "page index"), Ffi.ToFfiOrNull(options)), cancellationToken);

    // Settings

    /// <summary>The current settings.</summary>
    public async Task<StageSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(StageSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Replaces the responsive rules. A null rule throws <see cref="ArgumentException"/>.</summary>
    public Task SetResponsiveRulesAsync(IEnumerable<StageResponsiveRule> rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var array = Arguments.NoNullElements(rules, nameof(rules));
        return viewer.Run(() => handle.SetResponsiveRules([.. array.Select(Ffi.ToFfi)]), cancellationToken);
    }

    /// <summary>Whether the named rule matches the viewport now.</summary>
    public Task<bool> MatchesRuleAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return viewer.Run(() => handle.MatchesRule(name), cancellationToken);
    }

    /// <summary>The names of the rules that match now.</summary>
    public async Task<IReadOnlyList<string>> GetActiveRulesAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.ActiveRules(), cancellationToken).ConfigureAwait(false);

    /// <summary>Restores a saved view.</summary>
    public Task ApplyViewStateAsync(StageViewState view, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        return viewer.Run(() => handle.ApplyViewState(Ffi.ToFfi(view)), cancellationToken);
    }

    // Zoom

    /// <summary>A double tap at a viewport point: zooms in around it, or back out.</summary>
    public Task DoubleTapZoomAsync(PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.DoubleTapZoom(Ffi.ToFfi(point)), cancellationToken);

    /// <summary>One step in.</summary>
    public Task ZoomInAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ZoomIn(), cancellationToken);

    /// <summary>One step out.</summary>
    public Task ZoomOutAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ZoomOut(), cancellationToken);

    /// <summary>Zooms to a target, around a viewport point or the zoom alignment.</summary>
    public Task ZoomToAsync(StageZoomTarget target, PdfPoint? around = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return viewer.Run(() => handle.ZoomTo(Ffi.ToFfi(target), around is { } p ? Ffi.ToFfi(p) : null), cancellationToken);
    }

    /// <summary>Multiplies the zoom by <paramref name="factor"/>.</summary>
    public Task ZoomByAsync(double factor, PdfPoint? around = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.ZoomBy(factor, around is { } p ? Ffi.ToFfi(p) : null), cancellationToken);

    /// <summary>Fits the page width.</summary>
    public Task FitWidthAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FitWidth(), cancellationToken);

    /// <summary>Fits the whole page.</summary>
    public Task FitPageAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FitPage(), cancellationToken);

    /// <summary>Fits every page.</summary>
    public Task FitAllAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FitAll(), cancellationToken);

    /// <summary>Fits automatically.</summary>
    public Task FitAutomaticAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FitAutomatic(), cancellationToken);

    /// <summary>Sets the view rotation in degrees.</summary>
    public Task SetViewRotationAsync(double degrees, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SetViewRotation(degrees), cancellationToken);

    /// <summary>Turns the view by <paramref name="delta"/> degrees.</summary>
    public Task RotateViewByAsync(double delta, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.RotateViewBy(delta), cancellationToken);

    // Lifecycle

    /// <summary>Tells the stage the viewport's size in viewport points. The first one places the document.</summary>
    public Task SetViewportSizeAsync(PdfSize size, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SetViewportSize(Ffi.ToFfi(size)), cancellationToken);

    /// <summary>Device pixels per viewport point.</summary>
    public Task SetDevicePixelRatioAsync(double ratio, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SetDevicePixelRatio(ratio), cancellationToken);

    /// <summary>Reapplies a named fit.</summary>
    public Task RefitAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Refit(), cancellationToken);

    /// <summary>The view to save.</summary>
    public async Task<StageViewState> GetViewStateAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ViewState(), cancellationToken).ConfigureAwait(false));

    /// <summary>This lens's id: the <see cref="PointerSample.Source"/> its samples carry.</summary>
    public Task<string> GetLensIdAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.LensId(), cancellationToken);

    /// <summary>
    /// Registers a provider asked, on the kernel thread, for the view this
    /// document opens at when the stage first places it. The highest
    /// <paramref name="priority"/> answers. The viewer keeps the provider
    /// until the registration is unregistered or the document closes, and with
    /// it anything it captures: a provider that captures the viewer or this
    /// capability keeps the viewer alive until then.
    /// </summary>
    public async Task<Registration> ProvideInitialViewAsync(IStageInitialViewProvider provider, double priority = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var adapter = new ProviderAdapter(provider, viewer.Mark);
        return new(await viewer.Run(() => handle.ProvideInitialView(priority, adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Places the document now, if it has not been placed.</summary>
    public Task PlaceInitialAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.PlaceInitial(), cancellationToken);

    /// <summary>
    /// Lets a pan tool drag this lens: registers the stage's pan-scroll handler
    /// with the pointer hub, scoped to <see cref="GetLensIdAsync"/>.
    /// <paramref name="panFallback"/> lets any other tool pan over the gaps
    /// between pages. Throws <see cref="PdfErrorCode.Unsupported"/> without
    /// <see cref="PdfViewerOptions.Interaction"/>.
    /// </summary>
    public async Task<Registration> RegisterScrollHandlerAsync(bool panFallback = true, CancellationToken cancellationToken = default) =>
        new(await viewer.Run(() => handle.RegisterScrollHandler(panFallback), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Stage events, subscribed when this returns. Ends when the document
    /// closes (at once if it already has), when the viewer is disposed, or
    /// when <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<StageEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    static void Fire(Action call)
    {
        try
        {
            call();
        }
        catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
        {
            // The document or the viewer has gone: the call has nowhere to go.
        }
    }

    sealed class Relay() : EventRelay<GSt.StageEvent, StageEvent>(Ffi.ToPublic), GSt.StageEventListener
    {
        public void OnEvent(GSt.StageEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    /// <summary>Called on the kernel thread, marked; a throw is the kernel's host error.</summary>
    sealed class ProviderAdapter(IStageInitialViewProvider app, KernelMark mark) : GSt.StageInitialViewProvider
    {
        public GSt.StageViewState? InitialView() =>
            KernelCallback.RunOrFail(mark, () => app.InitialView() is { } view ? Ffi.ToFfi(view) : null);
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's stage. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Stage"/>.</summary>
    public async Task<StageCapability> GetStageAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Stage(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
