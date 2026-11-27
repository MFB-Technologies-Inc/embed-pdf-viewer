using Xunit;

namespace EmbedPDF.Viewer.Tests;

sealed class SavedView(StageViewState? view, Exception? throws = null, Action? hook = null) : IStageInitialViewProvider
{
    public int Asked;

    public StageViewState? InitialView()
    {
        Interlocked.Increment(ref Asked);
        hook?.Invoke();
        if (throws is not null) throw throws;
        return view;
    }
}

public sealed class StageTests
{
    internal static readonly StageGoToOptions Instant = new() { Behavior = StageScrollBehavior.Instant };

    // The native host's clock always has frames, so motion tweens; the tests
    // that need a jump ask for Instant.
    internal static async Task<(PdfViewer Viewer, DocumentInfo Document, StageCapability Stage)> Opened(PdfViewerOptions? options = null)
    {
        var viewer = await PdfViewer.CreateAsync(options ?? new PdfViewerOptions { Stage = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var stage = await viewer.GetStageAsync(document);
        await stage.SetViewportSizeAsync(new PdfSize(390, 844));
        return (viewer, document, stage);
    }

    static async Task<T> First<T>(IAsyncEnumerable<StageEvent> events) where T : StageEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is T found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    [Fact]
    public async Task NativeZoomBoxTracksViewportPointsAndCancels()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var before = await stage.GetCameraAsync();
        await stage.BeginZoomDragAsync(new PdfPoint(70, 90));
        Assert.Null(await stage.GetZoomBoxAsync());
        await stage.MoveZoomDragAsync(new PdfPoint(170, 190));
        var box = Assert.IsType<PdfRect>(await stage.GetZoomBoxAsync());
        Assert.True(Math.Abs(box.X - 70) < 1e-6 && Math.Abs(box.Y - 90) < 1e-6);
        Assert.True(Math.Abs(box.Width - 100) < 1e-6 && Math.Abs(box.Height - 100) < 1e-6);
        await stage.CancelZoomDragAsync();
        Assert.Null(await stage.GetZoomBoxAsync());
        Assert.Equal(before, await stage.GetCameraAsync());
        Assert.NotEmpty(await stage.GetCurrentPagesAsync());
    }

    [Fact]
    public async Task ASizedStageShowsTheFirstPage()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var visible = await stage.GetVisiblePagesAsync();
        Assert.Equal(0, visible[0].PageIndex);
        Assert.True(visible[0].Transform.RenderScale > 0);
    }

    [Fact]
    public async Task NavigationIsObserved()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var events = stage.ObserveEvents();
        await stage.GoToPageIndexAsync(5, Instant);
        var changed = await First<StageEvent.PageChanged>(events);
        Assert.Equal(5, changed.PageIndex);
        Assert.Equal(5, await stage.GetCurrentPageIndexAsync());
    }

    [Fact]
    public async Task FitWidthThenZoomIn()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        await stage.FitWidthAsync();
        Assert.Equal(new StageZoomMode.Mode(ZoomMode.FitWidth), await stage.GetZoomModeAsync());
        var fitted = await stage.GetZoomLevelAsync();
        await stage.ZoomInAsync();
        Assert.True(await stage.GetZoomLevelAsync() > fitted);
    }

    [Fact]
    public async Task APanGestureMovesTheCamera()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var before = await stage.GetCameraAsync();
        await stage.BeginGestureAsync();
        for (var i = 0; i < 20; i++) stage.PanBy(0, -10);
        await stage.EndGestureAsync();
        Assert.NotEqual(before.Y, (await stage.GetCameraAsync()).Y);
    }

    [Fact]
    public async Task ASavedViewRestoresOnTheNextOpen()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        await stage.GoToPageIndexAsync(7, Instant);
        var saved = await stage.GetViewStateAsync();
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var next = await viewer.GetStageAsync(second);
        _ = await next.ProvideInitialViewAsync(new SavedView(saved), priority: 10);
        await next.SetViewportSizeAsync(new PdfSize(390, 844));
        Assert.Equal(7, await next.GetCurrentPageIndexAsync());
    }

    [Fact]
    public async Task AFlingMovesThenSettles()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var events = stage.ObserveEvents();
        await stage.FlingAsync(0, -3000);
        Assert.True(await stage.IsMovingAsync());
        _ = await First<StageEvent.MotionEnded>(events);
        Assert.False(await stage.IsMovingAsync());
    }

    /// <summary>A click with the zoom tool: a box that never reaches its threshold.</summary>
    static async Task<(double Before, double After)> ClickZoom(Func<StageCapability, Task> end)
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var before = await stage.GetZoomLevelAsync();
        await stage.BeginZoomDragAsync(new PdfPoint(195, 400));
        await end(stage);
        return (before, await stage.GetZoomLevelAsync());
    }

    [Fact]
    public async Task EndZoomDragLeftOutZoomsIn()
    {
        var omitted = await ClickZoom(s => s.EndZoomDragAsync());
        var zoomIn = await ClickZoom(s => s.EndZoomDragAsync(zoomOut: false));
        var zoomOut = await ClickZoom(s => s.EndZoomDragAsync(zoomOut: true));
        Assert.True(omitted.After > omitted.Before);
        Assert.Equal(zoomIn.After, omitted.After);
        Assert.True(zoomOut.After < zoomOut.Before);
    }

    /// <summary>The camera inside a gesture after dragging past the top, where only an elastic gesture leaves the clamp.</summary>
    static async Task<Camera> Overscrolled(Func<StageCapability, Task> begin)
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        await begin(stage);
        for (var i = 0; i < 20; i++) stage.PanBy(0, 30);
        var camera = await stage.GetCameraAsync();
        await stage.EndGestureAsync();
        return camera;
    }

    [Fact]
    public async Task BeginGestureLeftOutIsNotElastic()
    {
        var omitted = await Overscrolled(s => s.BeginGestureAsync());
        var clamped = await Overscrolled(s => s.BeginGestureAsync(elastic: false));
        var elastic = await Overscrolled(s => s.BeginGestureAsync(elastic: true));
        Assert.Equal(clamped, omitted);
        Assert.NotEqual(clamped, elastic);
    }

    /// <summary>The cursor over the gap beside the first page, with the pointer tool.</summary>
    static async Task<string> GapCursor(Func<StageCapability, Task<Registration>> register)
    {
        var (viewer, document, stage) = await Opened(new PdfViewerOptions { Interaction = true, Stage = new() });
        await using var _v = viewer;
        using var interaction = await viewer.GetInteractionAsync(document);
        var registration = await register(stage);
        var lens = await stage.GetLensIdAsync();
        // A move nobody captured hovers; no page is a gap.
        interaction.DispatchPointer(new PointerSample(PointerPhase.Move, new PdfPoint(2, 2), new PointerModifiers())
        {
            PointerType = PointerKind.Mouse,
            Source = lens,
        });
        var cursor = await interaction.GetCursorAsync();
        registration.Unregister();
        return cursor;
    }

    [Fact]
    public async Task RegisterScrollHandlerLeftOutPansOverGaps()
    {
        Assert.Equal("grab", await GapCursor(s => s.RegisterScrollHandlerAsync()));
        Assert.Equal("grab", await GapCursor(s => s.RegisterScrollHandlerAsync(panFallback: true)));
        Assert.NotEqual("grab", await GapCursor(s => s.RegisterScrollHandlerAsync(panFallback: false)));
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, stage) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => stage.GetZoomLevelAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        using var cancel = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in stage.ObserveEvents(cancel.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(20);
        cancel.Cancel();
        await reading;
        await stage.ZoomInAsync();
    }

    [Fact]
    public async Task AWheelPansTheView()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var before = await stage.GetCameraAsync();
        // Signed like PanBy: a negative dy moves down the document, where there is room.
        Assert.True(await stage.WheelByAsync(0, -40, 0));
        Assert.True(await stage.WheelByAsync(0, -40, 16));
        for (var i = 0; i < 500 && await stage.IsMovingAsync(); i++) await Task.Delay(10);
        Assert.NotEqual(before, await stage.GetCameraAsync());
    }

    [Fact]
    public async Task AThrowingProviderLeavesTheDocumentPlaced()
    {
        var (viewer, document, _) = await Opened();
        await using var _v = viewer;
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var next = await viewer.GetStageAsync(second);
        var provider = new SavedView(null, new InvalidOperationException("no saved view"));
        _ = await next.ProvideInitialViewAsync(provider, priority: 10);
        await next.SetViewportSizeAsync(new PdfSize(390, 844));
        Assert.Equal(0, await next.GetCurrentPageIndexAsync());
        Assert.True(provider.Asked > 0);
        _ = document;
    }

    [Fact]
    public async Task DisposeFromAProviderThrowsInvalidOperation()
    {
        var (viewer, _, _) = await Opened();
        await using var _v = viewer;
        Exception? seen = null;
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var next = await viewer.GetStageAsync(second);
        _ = await next.ProvideInitialViewAsync(new SavedView(null, hook: () =>
        {
            try
            {
                viewer.Dispose();
            }
            catch (Exception error)
            {
                seen = error;
            }
        }));
        await next.SetViewportSizeAsync(new PdfSize(390, 844));
        Assert.IsType<InvalidOperationException>(seen);
        Assert.Equal(0, await next.GetCurrentPageIndexAsync());
    }

    [Fact]
    public async Task FireAndForgetCallsAfterCloseDoNothing()
    {
        var (viewer, document, stage) = await Opened();
        await viewer.CloseAsync(document);
        stage.SetCamera(new Camera(0, 0, 1));
        stage.PanBy(1, 1);
        stage.ScrollBy(new StageScrollToOptions { Top = 10 });
        stage.ZoomAround(new PdfPoint(1, 1), 2);
        await viewer.DisposeAsync();
        stage.PanBy(1, 1);
        stage.ScrollBy(new StageScrollToOptions { Top = 10 });
    }

    /// <summary>A PDF whose page tree is empty, with a valid cross-reference table.</summary>
    static byte[] NoPages()
    {
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [] /Count 0 >>"];
        var text = new System.Text.StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(text.ToString());
    }

    /// <summary>
    /// The engine refuses camera writes in paged flow over no pages
    /// (<c>crates/plugins/stage/tests/api.rs</c>,
    /// <c>paged_flow_over_no_pages_refuses_camera_writes_as_the_typescript_does</c>).
    /// A fire-and-forget call has no task to fail, so the refusal arrives on
    /// <see cref="PdfViewer.ObserveReportedErrors"/>.
    /// </summary>
    [Fact]
    public async Task AFailingFireAndForgetCallArrivesOnReportedErrors()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Stage = new() });
        var document = await viewer.OpenAsync(NoPages());
        using var stage = await viewer.GetStageAsync(document);
        await stage.SetViewportSizeAsync(new PdfSize(390, 844));
        await stage.PlaceInitialAsync();
        await stage.UpdateSettingsAsync(new StageSettingsPatch { Flow = StageFlow.Paged });
        var errors = viewer.ObserveReportedErrors();
        stage.SetCamera(new Camera(1, 2, 1));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var reader = errors.GetAsyncEnumerator(timeout.Token);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(("TypeError", "Cannot read properties of undefined (reading 'x')"), (reader.Current.Name, reader.Current.Detail));
    }

    [Fact]
    public async Task AnInvalidFireAndForgetArgumentThrowsAtOnce()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var error = Assert.Throws<PdfException>(() => stage.ScrollBy(new StageScrollToOptions { Behavior = (StageScrollBehavior)9 }));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
    }

    [Fact]
    public async Task SettingsRoundTripThroughThePatch()
    {
        var (viewer, _, stage) = await Opened();
        await using var _v = viewer;
        var registered = await stage.GetSettingsAsync();
        await stage.UpdateSettingsAsync(new StageSettingsPatch
        {
            Layout = StageLayout.Horizontal,
            Gap = new StageGap.Px(12),
            Columns = new StageGridColumns.Count(3),
            Zoom = new ZoomSpec.Mode(ZoomMode.FitPage),
            FitAlign = new StageAlignment(StageAlign.Start, StageAlign.End),
        });
        var changed = await stage.GetSettingsAsync();
        Assert.Equal(StageLayout.Horizontal, changed.Layout);
        Assert.Equal(new StageGap.Px(12), changed.Gap);
        Assert.Equal(new StageGridColumns.Count(3), changed.Columns);
        Assert.Equal(new ZoomSpec.Mode(ZoomMode.FitPage), changed.Zoom);
        Assert.Equal(new StageAlignment(StageAlign.Start, StageAlign.End), changed.FitAlign);
        await stage.ResetSettingsAsync();
        Assert.Equal(registered, await stage.GetSettingsAsync());
        var state = await stage.GetViewStateAsync();
        await stage.ApplyViewStateAsync(state);
        Assert.Equal(state.Anchor.PageIndex, (await stage.GetViewStateAsync()).Anchor.PageIndex);
    }

    [Fact]
    public async Task CoordinatesRoundTripWithoutRounding()
    {
        var (viewer, document, stage) = await Opened();
        await using var _v = viewer;
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var point = new PdfPoint(10.25, 20.125);
        var viewport = Assert.IsType<PdfPoint>(await stage.PageToViewportAsync(page, point));
        var back = Assert.IsType<PdfPoint>(await stage.ViewportToPageAsync(page, viewport));
        Assert.True(Math.Abs(back.X - point.X) < 1e-9 && Math.Abs(back.Y - point.Y) < 1e-9);
        var hit = Assert.IsType<StagePageHit>(await stage.GetPageAtAsync(viewport));
        Assert.Equal(page, hit.Page);
        var world = await stage.ViewportToWorldAsync(viewport);
        Assert.Equal(viewport.X, (await stage.WorldToViewportAsync(world)).X, 9);
    }

    [Fact]
    public async Task ADestinationIsReachable()
    {
        var (viewer, document, stage) = await Opened();
        await using var _v = viewer;
        var page = (await viewer.GetPagesAsync(document))[4].Page;
        await stage.GoToDestinationAsync(new PageDestination.Fit(page), StageScrollBehavior.Instant);
        Assert.Equal(4, await stage.GetCurrentPageIndexAsync());
        Assert.NotNull(await stage.GetCurrentDestinationAsync());
    }

    public static TheoryData<object> Unions => new()
    {
        new ZoomSpec.Level(1.5), new ZoomSpec.PageWidth(0.5), new ZoomSpec.PageHeight(0.25), new ZoomSpec.Mode(ZoomMode.FitAll),
        new StageGap.World(3), new StageGap.Px(4),
        new StageGridColumns.Count(2), new StageGridColumns.Square(), new StageGridColumns.Auto(),
        new StageRevealZoom.Level(2), new StageRevealZoom.Keep(), new StageRevealZoom.Fit(), new StageRevealZoom.FitWidth(), new StageRevealZoom.FitHeight(),
        new StageZoomTarget.Level(2), new StageZoomTarget.Spec(new ZoomSpec.Mode(ZoomMode.Automatic)),
        new StageAlignValue.Named(StageAlign.Center), new StageAlignValue.Fraction(0.3),
        new StageArrivalAlignValue.Keep(), new StageArrivalAlignValue.Align(new StageAlignValue.Fraction(0.1)),
        new StageRevealAnchorValue.Word(StageRevealWord.End), new StageRevealAnchorValue.Fraction(0.7),
    };

    [Theory]
    [MemberData(nameof(Unions))]
    public void EveryUnionCaseRoundTrips(object value)
    {
        object back = value switch
        {
            ZoomSpec v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageGap v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageGridColumns v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageRevealZoom v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageZoomTarget v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageAlignValue v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageArrivalAlignValue v => Ffi.ToPublic(Ffi.ToFfi(v)),
            StageRevealAnchorValue v => Ffi.ToPublic(Ffi.ToFfi(v)),
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(value, back);
    }
}

public sealed partial class IntegerSurfaceTests
{
    [Fact]
    public async Task StagePageIndexesAreInt()
    {
        var (viewer, _, stage) = await StageTests.Opened();
        await using var _v = viewer;
        Assert.Equal(14, await stage.GetPageCountAsync());
        await stage.GoToPageIndexAsync(3, StageTests.Instant);
        foreach (var value in OutsideUInt32)
        {
            await ExpectInvalidInput(() => stage.GoToPageIndexAsync(value, StageTests.Instant));
            await ExpectInvalidInput(() => stage.RevealIndexAsync(value));
        }
        Assert.Equal(3, await stage.GetCurrentPageIndexAsync());
        await stage.GoToPageIndexAsync(0, StageTests.Instant);
        Assert.Equal(0, await stage.GetCurrentPageIndexAsync());
        await stage.RevealIndexAsync(0);
    }
}
