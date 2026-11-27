namespace EmbedPDF.Models;

/// <summary>
/// What a scrolling reader shows, and the gestures it forwards. The stage
/// lays out; this model only mirrors its visible pages and camera onto the
/// home context and turns a view's gesture values into stage calls.
/// </summary>
/// <remarks>
/// <para>
/// A view feeds it plain values, in viewport units with the origin at the
/// viewport's top left and y growing downward: the viewport size and the
/// display scale (<see cref="SizeChangedAsync"/>), a drag's total
/// translation and its end velocity (<see cref="DragChangedAsync"/>,
/// <see cref="DragEndedAsync"/>), a pinch's total magnification and centre
/// (<see cref="MagnifyChanged"/>, <see cref="MagnifyEndedAsync"/>), a double
/// tap (<see cref="DoubleTappedAsync"/>), and pointer events as
/// <see cref="PointerInput"/> (<see cref="PointerDown"/> and its siblings,
/// <see cref="Tapped"/>). It draws <see cref="VisiblePages"/>, each at its
/// screen position, rendering a page at <see cref="RenderScale"/>. The
/// stage owns the scroll position: a drag pans it, and a view never sets a
/// scroll offset of its own.
/// </para>
/// <para>
/// Given an <see cref="InteractionCapability"/>, it also sends pointer
/// events to the document's pointer hub, so the active tool draws and a tap
/// reaches whatever handles taps. Given a <see cref="LinkCapability"/> too, a
/// tap on a link activates it and the outcome arrives on
/// <see cref="ObserveLinkActivations"/>; the model loads the links of each
/// page as it comes into view, so the app calls no <c>EnsureLoadedAsync</c>.
/// Without an <see cref="InteractionCapability"/> the pointer calls do
/// nothing.
/// </para>
/// <para>
/// <see cref="Error"/> is the latest failure of one of the model's calls,
/// <c>null</c> when the latest call of that kind succeeded. The kinds are: a
/// read of the stage's pages and camera, <see cref="SizeChangedAsync"/>, a
/// drag call, <see cref="DoubleTappedAsync"/>, the tool read, the link tap
/// registration in <see cref="RunAsync"/>, a page's link load, and a pointer
/// event's page lookup. A failure of any kind replaces the error. Only a
/// later success of the kind that set it clears it, so a good read of the
/// pages does not hide a failed link load, and the next good link load does.
/// The cost: a caller that polls this can miss a failure a later success
/// cleared. Observe it to see each one.
/// </para>
/// </remarks>
public sealed class StageReaderModel : ObservableModel, IDisposable
{
    /// <summary>The most pixels one page renders at: 4096 by 4096, 64 MB of RGBA.</summary>
    public const double MaxPagePixels = 4096d * 4096d;

    /// <summary>How far, in viewport units, a drag moves before it pans while <see cref="PanBeginsOnMovement"/> is on.</summary>
    internal const double PanMovementThreshold = 10;

    /// <summary>The kinds of call whose failure lands in <see cref="Error"/>.</summary>
    internal enum Call { Read, Resize, Drag, DoubleTap, ToolRead, LinkTapRegistration, LinkLoad, PageLookup }

    /// <summary>Where a zoom stands. A pinch's end or a double tap may start a zoom animation; <c>Settling</c> holds until a refresh finds the stage still.</summary>
    enum ZoomPhase { Idle, Pinching, Settling }

    readonly InteractionCapability? interaction;
    readonly LinkCapability? link;
    /// <summary>Sends the pointer calls to the hub. <c>null</c> without an <see cref="InteractionCapability"/>.</summary>
    readonly StagePointerForwarder? pointer;
    readonly LinkActivationFeed linkFeed = new();
    /// <summary>The drag calls, each run after every earlier one, so an end never reaches the stage before the begin it closes.</summary>
    readonly SerialCalls dragCalls = new();
    /// <summary>The link loads, one set of newly visible pages after another, off the stage event loop.</summary>
    readonly SerialCalls linkLoads = new();
    /// <summary>The pages whose links have loaded or are loading. A page whose load failed leaves it.</summary>
    readonly HashSet<PageRef> linkPages = [];
    IReadOnlyList<StageVisiblePage> visiblePages = [];
    Camera? camera;
    int pageIndex;
    PdfException? error;
    Call? failedCall;
    bool isZooming;
    ToolId? activeToolId;
    bool forwardsDrags;
    bool isPanEnabled = true;
    double lastTranslationX;
    double lastTranslationY;
    double lastMagnification = 1;
    ZoomPhase zoomPhase = ZoomPhase.Idle;
    /// <summary>
    /// Bumped when a zoom start begins and when its stage call returns. A
    /// refresh judges <c>Settling</c> only if no start was in flight when it
    /// began and this is unchanged when its read lands.
    /// </summary>
    int zoomEpoch;
    /// <summary>Zoom starts whose stage call has not returned.</summary>
    int zoomStartsInFlight;
    /// <summary>
    /// Numbers each read of the pages as it starts. A read applies its pages
    /// and camera unless a read that started after it has already landed, so
    /// a read from before a resize cannot land after the resize's own read and
    /// put the old pages back, while the newest read to land is always shown.
    /// Swift's model has no such guard; the .NET event path reorders more,
    /// which makes the race real.
    /// </summary>
    long readVersion;
    /// <summary>The number of the newest read that has landed.</summary>
    long landedVersion;

    /// <summary>Creates a reader model.</summary>
    /// <param name="stage">The stage that lays the pages out. The model does not dispose it.</param>
    /// <param name="render">The capability that renders them. The model does not dispose it.</param>
    /// <param name="interaction">
    /// The document's pointer hub. With it the model follows the active tool
    /// and the pointer calls reach the hub; without it they do nothing.
    /// </param>
    /// <param name="link">
    /// The document's links. With it <see cref="RunAsync"/> lets taps on the
    /// hub follow links and <see cref="ObserveLinkActivations"/> reports them,
    /// and the model loads each page's links when the page first comes into
    /// view. A tap that lands before its page's load has finished finds no link.
    /// </param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public StageReaderModel(
        StageCapability stage, RenderCapability render, InteractionCapability? interaction = null,
        LinkCapability? link = null, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(render);
        Stage = stage;
        Render = render;
        this.interaction = interaction;
        this.link = link;
        Revisions = new RenderRevisions(render, Context);
        if (interaction is not null)
        {
            pointer = new StagePointerForwarder(stage, interaction)
            {
                LookedUp = failure =>
                {
                    if (failure is null) Succeeded(Call.PageLookup);
                    else Failed(Call.PageLookup, failure);
                },
            };
        }
    }

    /// <summary>The pages in view, each with its place on screen, replaced as a whole when they change.</summary>
    public IReadOnlyList<StageVisiblePage> VisiblePages => visiblePages;

    /// <summary>The stage's camera; <c>null</c> before the first read.</summary>
    public Camera? Camera { get => camera; private set => Set(ref camera, value); }

    /// <summary>The stage's current page as a zero-based display position.</summary>
    public int PageIndex { get => pageIndex; private set => Set(ref pageIndex, value); }

    /// <summary>The latest failure; see the remarks on the type.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    /// <summary>
    /// Whether the zoom is changing: a pinch is under way, or the zoom
    /// animation a pinch's end or a double tap starts hasn't settled. Pages
    /// render at banded scales while it is, and at the stage's own scale
    /// otherwise (<see cref="RenderScale"/>). A pan or a fling moves the
    /// camera without changing the scale, so it leaves this false.
    /// </summary>
    public bool IsZooming { get => isZooming; private set => Set(ref isZooming, value); }

    /// <summary>
    /// The hub's active tool, read when <see cref="RunAsync"/> starts and
    /// followed from the hub's tool changes while it runs. <c>null</c> before
    /// the first read, after a failed one, and always without an
    /// <see cref="InteractionCapability"/>.
    /// </summary>
    public ToolId? ActiveToolId { get => activeToolId; private set => Set(ref activeToolId, value); }

    /// <summary>
    /// Whether a drag belongs to the active tool rather than to the pan: true
    /// when the model has an <see cref="InteractionCapability"/> and
    /// <see cref="ActiveToolId"/> is not the hub's default tool. A view reads
    /// it when a drag starts: true sends the drag to <see cref="PointerDown"/>,
    /// <see cref="PointerMoved"/> and <see cref="PointerUp"/> instead of
    /// <see cref="DragChangedAsync"/>. It replaces only the pan:
    /// <see cref="IsPanEnabled"/> does not gate it, and a tap goes to
    /// <see cref="Tapped"/> either way.
    /// </summary>
    public bool ForwardsDrags { get => forwardsDrags; private set => Set(ref forwardsDrags, value); }

    /// <summary>
    /// Whether a drag pans the stage. Turn it off while a page overlay needs
    /// every touch. Turning it off mid-drag ends the open stage gesture without
    /// a fling. It gates the pan only. Set from another thread, the write is
    /// posted to the home context and lands after this returns.
    /// </summary>
    public bool IsPanEnabled
    {
        get => isPanEnabled;
        set => Home.Run(() =>
        {
            var was = isPanEnabled;
            if (!Set(ref isPanEnabled, value, nameof(IsPanEnabled))) return;
            // Joins the drag chain before returning, so a drag call made after
            // this setter runs after the end.
            if (was && !value) _ = dragCalls.Enqueue(() => ApplyDragEndAsync(0, 0));
        });
    }

    /// <summary>
    /// Whether a pan begins only once the drag has moved more than 10
    /// viewport units from where it landed. Until then its changes open no
    /// stage gesture, and a drag that ends before then flings nothing. False
    /// by default, so a touch opens the gesture where it lands and stops a
    /// fling under it. Turn it on when something above the reader delays its
    /// drag until the touch moves or lifts, as a long-press gesture does. The
    /// pan begins from the point where the drag crosses the threshold, so the
    /// page does not jump. The cost: with it on, the first 10 units of a drag
    /// do not scroll, and a still touch or a tap does not stop a fling.
    /// </summary>
    public bool PanBeginsOnMovement { get; set; }

    /// <summary>Per-page render revisions. The counter observes by itself, whether or not <see cref="RunAsync"/> runs.</summary>
    public RenderRevisions Revisions { get; }

    /// <summary>The stage the model drives.</summary>
    public StageCapability Stage { get; }

    /// <summary>The capability that renders the pages.</summary>
    public RenderCapability Render { get; }

    internal bool GestureOpen { get; private set; }

    /// <summary>Runs in a double tap after the start is marked and before the stage call. Tests hold it so a refresh can land first.</summary>
    internal Func<Task>? ZoomStartReached { get; set; }

    /// <summary>Runs in a refresh after its moving read returns and before the read is applied. Tests hold it so a double tap can run first.</summary>
    internal Func<Task>? ZoomReadReturned { get; set; }

    /// <summary>Runs as each pointer sample is handed to the hub. Tests record them.</summary>
    internal Action<PointerSample>? PointerSampleDispatched
    {
        get => pointer?.Dispatched;
        set { if (pointer is not null) pointer.Dispatched = value; }
    }

    /// <summary>
    /// The scale to render a page at: the stage's render scale, capped so the
    /// whole page stays within <see cref="MaxPagePixels"/>. Past the cap a deep
    /// zoom is soft rather than hundreds of megabytes a page. Sizes in page
    /// points. While zooming the scale is first rounded up to a width band
    /// (<see cref="PageRenderResolution.BandedScale"/>), so a pinch restarts a
    /// page's render only when it crosses a band.
    /// </summary>
    public static double RenderScale(double scale, double contentWidth, double contentHeight, bool zooming = false)
    {
        var wanted = zooming ? PageRenderResolution.BandedScale(scale, contentWidth) : scale;
        var area = contentWidth * contentHeight;
        return area > 0 ? Math.Min(wanted, Math.Sqrt(MaxPagePixels / area)) : wanted;
    }

    /// <summary>A revision that changes when the engine invalidates this page's render.</summary>
    public long GetRenderRevision(PageRef page) => Revisions.GetRevision(page);

    /// <summary>
    /// Link activations the document reports while <see cref="RunAsync"/>
    /// runs: the outcome of a tap that lands on a link, and also of an
    /// activation the app makes itself through <see cref="LinkCapability"/>.
    /// A <see cref="LinkActivation"/> naming a URI is the app's to open. Never
    /// yields without a <see cref="LinkCapability"/>.
    /// </summary>
    /// <remarks>
    /// Each call is a new stream, subscribed before this returns, and every
    /// stream open when an activation arrives gets it. A stream gets no
    /// activation from before it was made. It ends when its enumeration ends,
    /// <paramref name="cancellationToken"/> is cancelled, or the model is disposed.
    /// </remarks>
    public IAsyncEnumerable<LinkActivation> ObserveLinkActivations(CancellationToken cancellationToken = default) =>
        linkFeed.Subscribe(cancellationToken);

    /// <summary>
    /// Runs for the view's lifetime, projecting stage events, until the
    /// document closes or <paramref name="cancellationToken"/> is cancelled,
    /// and returns without throwing. With an <see cref="InteractionCapability"/>
    /// it also follows the active tool, and with a <see cref="LinkCapability"/>
    /// as well it registers the link tap handler, once, and keeps the
    /// registration until it returns.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        using var group = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = group.Token;
        // Subscribed before the first read and before the handler registers,
        // so no tool change and no activation falls between.
        var stageEvents = Stage.ObserveEvents(token);
        var toolEvents = interaction?.ObserveEvents(token);
        var linkEvents = link?.ObserveEvents(token);
        var linkTaps = await RegisterLinkTapsAsync();
        try
        {
            var loops = new List<Task> { CapabilityObservation.Follow(stageEvents, ReceiveStageAsync, token) };
            if (toolEvents is not null)
            {
                loops.Add(FollowToolAsync(toolEvents, token));
            }
            if (linkEvents is not null)
            {
                loops.Add(CapabilityObservation.Follow(linkEvents, item =>
                {
                    if (item is LinkEvent.Activated activated) linkFeed.Yield(activated.Activation);
                    return Task.CompletedTask;
                }, token));
            }
            await RefreshAsync();
            await Task.WhenAny(loops);
            group.Cancel();
            await Task.WhenAll(loops);
        }
        finally
        {
            linkTaps?.Unregister();
        }
    }

    /// <summary>Sends a pointer down at <paramref name="input"/> to the hub, with the page the stage finds under it. Returns at once; the pointer calls reach the hub in the order they are made.</summary>
    public void PointerDown(PointerInput input) => Home.Run(() => pointer?.Down(input));

    /// <summary>
    /// Sends a pointer move. Moves that arrive while an earlier pointer call
    /// is still on its way collapse into the newest, so the hub sees fewer
    /// samples than a fast drag makes and always the latest position. A
    /// collapsed move reaches the hub before the up or cancel that follows it
    /// and never joins another gesture.
    /// </summary>
    public void PointerMoved(PointerInput input) => Home.Run(() => pointer?.Move(input));

    /// <summary>Sends a pointer up, after any move still waiting.</summary>
    public void PointerUp(PointerInput input) => Home.Run(() => pointer?.Up(input));

    /// <summary>
    /// Sends a pointer cancel at the last location, so the tool drops the
    /// gesture instead of committing it. A move of this gesture still waiting
    /// is dropped; an earlier gesture's samples are not. Call it when the
    /// system takes a drag away without ending it.
    /// </summary>
    public void PointerCancelled() => Home.Run(() => pointer?.Cancel());

    /// <summary>
    /// Sends a down and an up at <paramref name="input"/>, each with a click
    /// count of 1: a tap for the active tool, and for a link under the default
    /// tool. A pointer gesture <see cref="PointerDown"/> opened and nothing
    /// closed is cancelled first, as <see cref="PointerCancelled"/> does.
    /// </summary>
    public void Tapped(PointerInput input) => Home.Run(() => pointer?.Tap(input));

    /// <summary>Returns once every pointer call made so far has been handed to the hub.</summary>
    internal async Task PointerCallsFinishedAsync()
    {
        await Home.SwitchTo();
        if (pointer is not null) await pointer.FinishedAsync();
    }

    /// <summary>Returns once every link load started so far has finished.</summary>
    internal async Task LinkLoadsFinishedAsync()
    {
        await Home.SwitchTo();
        await linkLoads.Enqueue(() => { });
    }

    /// <summary>
    /// Tells the stage the viewport's size, in viewport units, and how many
    /// device pixels make one unit, then rereads the pages.
    /// </summary>
    public async Task SizeChangedAsync(double width, double height, double displayScale)
    {
        await Home.SwitchTo();
        try
        {
            await Stage.SetDevicePixelRatioAsync(displayScale);
            await Stage.SetViewportSizeAsync(new PdfSize(width, height));
            PageIndex = await Stage.GetCurrentPageIndexAsync();
            Succeeded(Call.Resize);
        }
        catch (Exception failure)
        {
            Failed(Call.Resize, failure);
        }
        await RefreshAsync();
    }

    /// <summary>
    /// A drag moved: its total translation since it began. A finger moving up
    /// scrolls down. Runs after every earlier drag call has finished. Does
    /// nothing to the stage while <see cref="IsPanEnabled"/> is false.
    /// </summary>
    public async Task DragChangedAsync(double translationX, double translationY)
    {
        await Home.SwitchTo();
        await dragCalls.Enqueue(() => ApplyDragChangeAsync(translationX, translationY));
    }

    /// <summary>
    /// A drag ended with a velocity in viewport units per second. Runs after
    /// every earlier drag call has finished. Skips the fling while
    /// <see cref="IsPanEnabled"/> is false, and for a drag that never began a
    /// pan under <see cref="PanBeginsOnMovement"/>.
    /// </summary>
    public async Task DragEndedAsync(double velocityX, double velocityY)
    {
        await Home.SwitchTo();
        await dragCalls.Enqueue(() => ApplyDragEndAsync(velocityX, velocityY));
    }

    /// <summary>
    /// Ends a pan the gesture system took away without ending it: closes the
    /// open stage gesture with no fling, so the next drag begins a gesture of
    /// its own. Runs after every earlier drag call has finished, and does
    /// nothing to the stage's gesture when none is open.
    /// </summary>
    public async Task DragCancelledAsync()
    {
        await Home.SwitchTo();
        await dragCalls.Enqueue(() => ApplyDragEndAsync(0, 0));
    }

    /// <summary>
    /// A pinch changed: its total magnification since it began, around
    /// (<paramref name="x"/>, <paramref name="y"/>) in the viewport. A pinch's
    /// first finger may already have opened a pointer gesture
    /// (<see cref="PointerDown"/>). The pinch cancels it at the hub, so the
    /// tool commits nothing, and the pointer calls for the rest of that touch
    /// send nothing; the next <see cref="PointerDown"/> or <see cref="Tapped"/>
    /// sends again.
    /// </summary>
    public void MagnifyChanged(double magnification, double x, double y) => Home.Run(() =>
    {
        pointer?.Interrupt();
        zoomPhase = ZoomPhase.Pinching;
        IsZooming = true;
        var factor = magnification / lastMagnification;
        lastMagnification = magnification;
        Stage.ZoomAround(new PdfPoint(x, y), factor);
    });

    /// <summary>A pinch ended. <see cref="IsZooming"/> turns false once the stage stops moving.</summary>
    public async Task MagnifyEndedAsync()
    {
        await Home.SwitchTo();
        zoomPhase = ZoomPhase.Settling;
        zoomEpoch++;
        lastMagnification = 1;
        await RefreshAsync();
    }

    /// <summary>A double tap at (<paramref name="x"/>, <paramref name="y"/>) in the viewport: the stage's zoom step around it.</summary>
    public async Task DoubleTappedAsync(double x, double y)
    {
        await Home.SwitchTo();
        if (zoomPhase != ZoomPhase.Pinching) zoomPhase = ZoomPhase.Settling;
        zoomEpoch++;
        zoomStartsInFlight++;
        if (ZoomStartReached is { } reached) await reached();
        try
        {
            await Stage.DoubleTapZoomAsync(new PdfPoint(x, y));
            Succeeded(Call.DoubleTap);
        }
        catch (Exception failure)
        {
            Failed(Call.DoubleTap, failure);
        }
        zoomStartsInFlight--;
        zoomEpoch++;
        await RefreshAsync();
    }

    /// <summary>Ends the render revision subscription and every link activation stream.</summary>
    public void Dispose()
    {
        Revisions.Dispose();
        linkFeed.Complete();
    }

    /// <summary>Records a failed call of one kind in <see cref="Error"/>.</summary>
    internal void Failed(Call call, Exception failure)
    {
        Error = ModelErrors.Control(failure);
        failedCall = call;
    }

    /// <summary>Clears <see cref="Error"/> when the call that set it was of this kind.</summary>
    void Succeeded(Call call)
    {
        if (failedCall != call) return;
        failedCall = null;
        Error = null;
    }

    async Task<Registration?> RegisterLinkTapsAsync()
    {
        if (link is null) return null;
        try
        {
            var registration = await link.RegisterTapHandlerAsync();
            Succeeded(Call.LinkTapRegistration);
            return registration;
        }
        catch (Exception failure)
        {
            Failed(Call.LinkTapRegistration, failure);
            return null;
        }
    }

    async Task FollowToolAsync(IAsyncEnumerable<InteractionEvent> events, CancellationToken token)
    {
        await RefreshToolAsync();
        await CapabilityObservation.Follow(events,
            item => item is InteractionEvent.ToolChanged ? RefreshToolAsync() : Task.CompletedTask, token);
    }

    /// <summary>Reads the active and the default tool. A failed read leaves no tool and no forwarding.</summary>
    async Task RefreshToolAsync()
    {
        if (interaction is null) return;
        try
        {
            var active = await interaction.GetActiveToolIdAsync();
            var fallback = await interaction.GetDefaultToolIdAsync();
            ActiveToolId = active;
            ForwardsDrags = active != fallback;
            Succeeded(Call.ToolRead);
        }
        catch (Exception failure)
        {
            ActiveToolId = null;
            ForwardsDrags = false;
            Failed(Call.ToolRead, failure);
        }
    }

    async Task ReceiveStageAsync(StageEvent item)
    {
        switch (item)
        {
            case StageEvent.CameraChanged changed:
                // The event carries the camera; only the pages it shows are read back.
                Camera = changed.Camera;
                // Performance: one visible-pages round trip per camera event,
                // and a pan, a pinch or a fling emits one per step. A read
                // slower than the steps queues them and the pages trail the camera.
                try
                {
                    var request = ++readVersion;
                    var pages = await Stage.GetVisiblePagesAsync();
                    if (Lands(request)) Show(pages);
                    Succeeded(Call.Read);
                }
                catch (Exception failure)
                {
                    Failed(Call.Read, failure);
                }
                break;
            case StageEvent.MotionEnded or StageEvent.ViewportChanged or StageEvent.ZoomChanged or StageEvent.SettingsChanged:
                await RefreshAsync();
                break;
            case StageEvent.PageChanged page:
                PageIndex = page.PageIndex;
                break;
        }
    }

    async Task ApplyDragChangeAsync(double translationX, double translationY)
    {
        if (!isPanEnabled) return;
        double originX = 0, originY = 0;
        if (PanBeginsOnMovement && !GestureOpen)
        {
            // A touch that has not moved past the threshold may be a tap.
            // Opening the gesture here would stop whatever motion that tap has
            // just started.
            var distance = Math.Sqrt(translationX * translationX + translationY * translationY);
            if (distance <= PanMovementThreshold) return;
            // The pan starts where the drag crossed the threshold, so the page
            // does not jump by the distance the finger had already moved.
            var crossing = PanMovementThreshold / distance;
            originX = translationX * crossing;
            originY = translationY * crossing;
        }
        try
        {
            if (!GestureOpen)
            {
                GestureOpen = true;
                lastTranslationX = originX;
                lastTranslationY = originY;
                await Stage.BeginGestureAsync(elastic: true);
            }
            // PanBy follows the finger: a finger moving up scrolls down.
            Stage.PanBy(translationX - lastTranslationX, translationY - lastTranslationY);
            lastTranslationX = translationX;
            lastTranslationY = translationY;
            Succeeded(Call.Drag);
        }
        catch (Exception failure)
        {
            Failed(Call.Drag, failure);
        }
        // The pan's camera event brings the camera and pages back.
    }

    async Task ApplyDragEndAsync(double velocityX, double velocityY)
    {
        // A drag that never passed the threshold panned nothing, so it flings
        // nothing: a tap's lift can carry a velocity.
        var panned = GestureOpen || !PanBeginsOnMovement;
        try
        {
            if (GestureOpen)
            {
                GestureOpen = false;
                await Stage.EndGestureAsync();
            }
            if ((velocityX != 0 || velocityY != 0) && isPanEnabled && panned)
            {
                await Stage.FlingAsync(velocityX, velocityY);
            }
            PageIndex = await Stage.GetCurrentPageIndexAsync();
            Succeeded(Call.Drag);
        }
        catch (Exception failure)
        {
            Failed(Call.Drag, failure);
        }
        await RefreshAsync();
    }

    /// <summary>
    /// Reads the pages, the camera and, after a pinch's end or a double tap,
    /// whether the zoom animation still runs. A failed read ends the
    /// animation, so <see cref="IsZooming"/> can't stay set. Only a refresh
    /// that began after the last zoom start returned may end the animation;
    /// that start's own refresh always does, so the animation still ends.
    /// </summary>
    async Task RefreshAsync()
    {
        var epoch = zoomEpoch;
        var mayJudge = zoomStartsInFlight == 0;
        try
        {
            // Performance: two round trips in sequence, three while a zoom
            // settles. One stage read that returns the pages, the camera and
            // whether the stage moves makes it one.
            var request = ++readVersion;
            var pages = await Stage.GetVisiblePagesAsync();
            var current = await Stage.GetCameraAsync();
            if (Lands(request))
            {
                Show(pages);
                Camera = current;
            }
            if (zoomPhase == ZoomPhase.Settling && mayJudge)
            {
                var moving = await Stage.IsMovingAsync();
                if (ZoomReadReturned is { } returned) await returned();
                // A pinch may have started while the read was out.
                if (epoch == zoomEpoch && zoomPhase == ZoomPhase.Settling && !moving) zoomPhase = ZoomPhase.Idle;
            }
            Succeeded(Call.Read);
        }
        catch (Exception failure)
        {
            if (mayJudge && epoch == zoomEpoch && zoomPhase == ZoomPhase.Settling) zoomPhase = ZoomPhase.Idle;
            Failed(Call.Read, failure);
        }
        IsZooming = zoomPhase != ZoomPhase.Idle;
    }

    /// <summary>Whether read <paramref name="request"/> may apply: no read that started after it has landed.</summary>
    bool Lands(long request)
    {
        if (request < landedVersion) return false;
        landedVersion = request;
        return true;
    }

    /// <summary>Takes the stage's visible pages and, with a <see cref="LinkCapability"/>, starts loading the links of those that were not in view before.</summary>
    void Show(IReadOnlyList<StageVisiblePage> pages)
    {
        var shown = link is null ? null : visiblePages.Select(page => page.Page).ToHashSet();
        if (!visiblePages.SequenceEqual(pages))
        {
            visiblePages = pages;
            Raise(nameof(VisiblePages));
        }
        if (link is null || shown is null) return;
        var fresh = pages.Select(page => page.Page).Where(page => !shown.Contains(page) && linkPages.Add(page)).ToList();
        if (fresh.Count == 0) return;
        _ = linkLoads.Enqueue(() => LoadLinksAsync(fresh, link));
    }

    /// <summary>A failed load is in <see cref="Error"/>, and its page loads again the next time it comes into view.</summary>
    async Task LoadLinksAsync(List<PageRef> pages, LinkCapability links)
    {
        foreach (var page in pages)
        {
            // Performance: one bridge call per page the first time it comes
            // into view. With the annotation plugin installed the call returns
            // at once, because that plugin's model already holds the links.
            try
            {
                await links.EnsureLoadedAsync(page);
                Succeeded(Call.LinkLoad);
            }
            catch (Exception failure)
            {
                linkPages.Remove(page);
                Failed(Call.LinkLoad, failure);
            }
        }
    }
}
