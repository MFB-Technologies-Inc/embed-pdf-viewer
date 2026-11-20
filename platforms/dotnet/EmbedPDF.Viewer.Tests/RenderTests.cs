using Xunit;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Port of <c>RenderTests.swift</c>, <c>RenderSnapshotTests.swift</c>,
/// <c>RenderViewportTests.swift</c>, the render case of
/// <c>PluginSettingsTests.swift</c> and the rendered case of <c>PageGeometryTests.swift</c>.
/// </summary>
public sealed class RenderTests : IAsyncLifetime
{
    readonly List<PdfViewer> viewers = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var viewer in viewers) await viewer.DisposeAsync();
    }

    async Task<(PdfViewer Viewer, DocumentInfo Document, PageRef Page, RenderCapability Render)> Opened(PdfViewerOptions? options = null, string fixture = "colors")
    {
        var viewer = await PdfViewer.CreateAsync(options ?? new PdfViewerOptions { Render = new() });
        viewers.Add(viewer);
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, page, await viewer.GetRenderAsync(document));
    }

    static readonly CancellationToken Cancelled = new(true);

    static byte[] At(PixelBuffer image, int x, int y) => image.GetRow(y).Slice(x * 4, 4).ToArray();

    [Fact]
    public async Task APageRendersToPixelsThatOutliveTheViewer()
    {
        var (viewer, _, page, render) = await Opened();
        var rendered = await render.RenderPageAsync(page, new RenderPageOptions { Scale = 1 });
        Assert.True(rendered.Width > 0 && rendered.Height > 0);
        Assert.Equal(1, rendered.Scale);
        Assert.Equal(rendered.Width, rendered.Image.Width);
        await viewer.DisposeAsync();
        Assert.True(rendered.Image.Pixels.Length >= rendered.Image.Stride * rendered.Image.Height);
        Assert.Equal(PixelFormat.Rgba8, rendered.Image.Format);
        Assert.False(rendered.Image.IsPremultiplied);
    }

    /// <summary>
    /// The colors fixture is an 80 by 20 point page of four 20-point squares:
    /// red, green, blue, then (0.2, 0.4, 0.6). A red and blue swap would turn
    /// the first square blue.
    /// </summary>
    [Fact]
    public async Task RedStaysRed()
    {
        var (_, _, page, render) = await Opened();
        var rendered = await render.RenderPageAsync(page, new RenderPageOptions { Scale = 0.5 });
        Assert.Equal((40, 10), (rendered.Width, rendered.Height));
        Assert.Equal([255, 0, 0, 255], At(rendered.Image, 5, 5));
        Assert.Equal([0, 255, 0, 255], At(rendered.Image, 15, 5));
        Assert.Equal([0, 0, 255, 255], At(rendered.Image, 25, 5));
        Assert.Equal([51, 102, 153, 255], At(rendered.Image, 35, 5));
        var bgra = rendered.Image.ToFormat(PixelFormat.Bgra8);
        Assert.Equal([0, 0, 255, 255], At(bgra, 5, 5));
    }

    [Fact]
    public async Task EveryRenderCancelsBeforeItStarts()
    {
        var (_, _, page, render) = await Opened();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render.RenderPageAsync(page, new RenderPageOptions { Scale = 1 }, Cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render.RenderThumbnailAsync(page, cancellationToken: Cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render.RenderPagesAsync([page], new RenderPageOptions { Scale = 1 }, cancellationToken: Cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render.RenderSourceAsync(page, 8, cancellationToken: Cancelled));
        _ = await render.RenderSourceAsync(page, 1);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, page, render) = await Opened();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in render.ObserveEvents(cancel.Token)) { }
        });
        Assert.Equal(0, viewer.StreamCountForTests);
        // A render after the drop still answers (and emits to no one).
        _ = await render.RenderPageAsync(page, new RenderPageOptions { Scale = 0.5 });
    }

    [Fact]
    public async Task ABadScaleIsRefused()
    {
        var (_, _, page, render) = await Opened();
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => render.RenderPageAsync(page, new RenderPageOptions { Scale = 0 }))).Code);
    }

    [Fact]
    public async Task ANegativeConcurrencyIsInvalidInput()
    {
        var (_, _, page, render) = await Opened();
        var error = await Assert.ThrowsAsync<PdfException>(() => render.RenderPagesAsync([page], concurrency: -1));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal("concurrency -1 is outside 0...4294967295", error.Detail);
    }

    [Fact]
    public async Task AThumbnailAndABatchRender()
    {
        var (viewer, document, page, render) = await Opened();
        Assert.True(await render.CanRenderThumbnailAsync());
        Assert.True(await render.CanRenderAsync());
        var thumbnail = await render.RenderThumbnailAsync(page);
        // The whole page, 512 pixels on its long side.
        Assert.Equal(512, Math.Max(thumbnail.Width, thumbnail.Height));
        var again = await render.RenderSourceAsync(page, 3, thumbnail: true);
        Assert.Equal(thumbnail.Width, again.Width);
        var pages = (await viewer.GetPagesAsync(document)).Select(p => p.Page).ToList();
        var batch = await render.RenderPagesAsync(pages, new RenderPageOptions { Width = 30 });
        Assert.Equal(pages, batch.Applied.Select(a => a.Page));
        Assert.All(batch.Applied, a => Assert.Equal(30, a.Rendered.Width));
        Assert.Empty(batch.Failed);
    }

    [Fact]
    public async Task InvalidatingIsObservedAndBumpsTheEpoch()
    {
        var (_, _, page, render) = await Opened();
        var before = await render.GetRenderEpochAsync(page);
        var events = render.ObserveEvents();
        await render.InvalidateAsync([page]);
        var invalidated = await Fixture.First(events, e => e as RenderEvent.Invalidated);
        Assert.Equal([page], invalidated.Pages);
        Assert.Equal(RenderInvalidateScope.Content, invalidated.Scope);
        Assert.Equal(before + 1, await render.GetRenderEpochAsync(page));
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, page, render) = await Opened();
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => render.RenderPageAsync(page))).Code);
    }

    [Fact]
    public async Task CroppedAndRotatedRenderKeepsScaleAndColors()
    {
        var (viewer, _, page, render) = await Opened();
        var image = await render.RenderPageAsync(page, new RenderPageOptions
        {
            Rect = new PdfRect(20, 0, 40, 20), Rotation = RenderRotation.Clockwise90, Scale = 2,
        });
        Assert.Equal((40, 80, 2.0), (image.Width, image.Height, image.Scale));
        await viewer.DisposeAsync();
        Assert.Equal([0, 255, 0, 255], At(image.Image, 20, 20));
        Assert.Equal([0, 0, 255, 255], At(image.Image, 20, 60));
    }

    [Fact]
    public async Task SnapshotMetadataChangesAndOwnedPixelsSurviveClearAndClose()
    {
        var (viewer, document, page, render) = await Opened(new PdfViewerOptions { Render = new(), Interaction = true });
        // Interaction is wave B: drive the snapshot tool through the generated handle.
        using var interaction = await viewer.CallAsync(h => h.Interaction(document.Id), default);
        Assert.True(await render.CanSnapshotAsync());
        Assert.Null(await render.GetSnapshotAsync());
        await using var changes = render.ObserveSnapshotChanges().GetAsyncEnumerator();
        await interaction.ActivateTool("snapshot");
        GF.PointerSample Sample(GF.PointerPhase phase, double x, double y) =>
            new(phase, new GF.Point(x, y), new GF.Modifiers(), new GF.PointerPageHit(Ffi.ToFfi(page), new GF.Point(x, y), Scale: 1, Rotation: 90));
        interaction.DispatchPointer(Sample(GF.PointerPhase.Down, 20, 0));
        interaction.DispatchPointer(Sample(GF.PointerPhase.Up, 60, 20));
        using var snapshot = Assert.IsType<RenderSnapshot>(await render.GetSnapshotAsync());
        Assert.Equal(page, snapshot.Page);
        Assert.Equal(new PdfRect(20, 0, 40, 20), snapshot.Rect);
        Assert.Equal(snapshot.Rect, await render.GetSnapshotBoxAsync(page));
        Assert.True(await changes.MoveNextAsync());
        var captured = Assert.IsType<RenderSnapshot>(changes.Current);
        Assert.Equal(snapshot.Rect, captured.Rect);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => snapshot.GetImageAsync(Cancelled));
        var pixels = await captured.GetImageAsync();
        Assert.Equal((40, 80, 2.0), (pixels.Width, pixels.Height, pixels.Scale));
        var before = pixels.Image.Pixels.ToArray();
        await render.ClearSnapshotAsync();
        Assert.Null(await render.GetSnapshotAsync());
        Assert.True(await changes.MoveNextAsync());
        Assert.Null(changes.Current);
        await viewer.CloseAsync(document);
        Assert.False(await changes.MoveNextAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => snapshot.GetImageAsync())).Code);
        await viewer.DisposeAsync();
        Assert.Equal(before, pixels.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task RenderSourceMatchesItsSourceKey()
    {
        var (_, _, page, render) = await Opened();
        Assert.Equal(await render.GetSourceKeyAsync(page, 2), await render.GetSourceKeyAsync(page, 2, ContentInclusion.Always));
        Assert.True((await render.RenderSourceAsync(page, 2)).Width > 0);
        Assert.IsAssignableFrom<RenderViewport>(await render.ConformViewportAsync(page, 2));
        Assert.IsAssignableFrom<RenderPolicy>(await render.GetRenderPolicyAsync());
    }

    [Fact]
    public async Task AViewDemandPlansAndDisposes()
    {
        var (_, _, page, render) = await Opened();
        _ = await render.GetPaintSettingsAsync();
        var view = await render.CreateViewDemandAsync("main");
        // The page is 80 points wide: the full-page raster meets an 80-pixel
        // demand, so the tile plane stays disengaged whether or not tiles are on.
        await view.SetDemandAsync(page, new RenderPageViewDemand(80));
        var plan = await view.GetPlanAsync(page);
        Assert.False(plan.Engaged);
        Assert.Empty(plan.Paint);
        Assert.Null(await view.GetTileImageAsync(page, "no-such-key"));
        await view.MarkPaintedAsync(page, "no-such-key");
        await view.MarkUnpaintedAsync(page, "no-such-key");
        await view.ReleaseAsync(page);
        await view.DisposeAsync();
        await view.DisposeAsync();
    }

    [Fact]
    public async Task LayersLeftOutDrawWhatTheSessionMayRead()
    {
        var (_, _, page, render) = await Opened();
        var omitted = await render.GetSourceKeyAsync(page, 2);
        var always = await render.GetSourceKeyAsync(page, 2, ContentInclusion.Always, ContentInclusion.Always);
        var never = await render.GetSourceKeyAsync(page, 2, ContentInclusion.Never);
        Assert.Equal(omitted, always);
        Assert.NotEqual(omitted, never);
        // Form fields left out follow annotations turned off.
        Assert.Equal(never, await render.GetSourceKeyAsync(page, 2, ContentInclusion.Never, ContentInclusion.Never));
    }

    [Fact]
    public async Task LayersLeftOutAreOmittedWhenTheSessionMayNotReadThem()
    {
        var (_, _, page, render) = await Opened(new PdfViewerOptions { Scope = ["doc.open", "doc.render"], Render = new() });
        Assert.Equal(await render.GetSourceKeyAsync(page, 2), await render.GetSourceKeyAsync(page, 2, ContentInclusion.Never, ContentInclusion.Never));
        Assert.True((await render.RenderSourceAsync(page, 2)).Width > 0);
        Assert.Equal(PdfErrorCode.PermissionDenied, (await Assert.ThrowsAsync<PdfException>(() => render.RenderSourceAsync(page, 2, ContentInclusion.Always))).Code);
        Assert.Equal(PdfErrorCode.PermissionDenied, (await Assert.ThrowsAsync<PdfException>(() => render.RenderThumbnailAsync(page, ContentInclusion.Always))).Code);
    }

    [Fact]
    public async Task ThumbnailLeftOutIsTheRaster()
    {
        var (_, _, page, render) = await Opened();
        var omitted = await render.GetSourceKeyAsync(page, 2);
        Assert.Equal(omitted, await render.GetSourceKeyAsync(page, 2, thumbnail: false));
        Assert.NotEqual(omitted, await render.GetSourceKeyAsync(page, 2, thumbnail: true));
    }

    [Fact]
    public async Task AClosedDocumentsViewDemandThrows()
    {
        var (viewer, document, page, render) = await Opened();
        var view = await render.CreateViewDemandAsync("main");
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => view.GetPlanAsync(page))).Code);
        await view.DisposeAsync();
    }

    [Fact]
    public async Task RenderTileAndSnapshotPatchesPreserveOtherSettings()
    {
        var (_, _, _, render) = await Opened(new PdfViewerOptions { Render = new RenderConfig { FullPage = new RenderFullPageOptions { MaxWidth = 480 } } }, "report");
        var registered = await render.GetSettingsAsync();
        await render.UpdateSettingsAsync(new RenderSettingsPatch
        {
            Tiles = new RenderTilesPatch.Off(),
            SlowAfterMs = 800,
            Snapshot = new RenderSnapshotPatch { Scale = 3, BoxLook = new RenderSnapshotBox { Fill = "red", Stroke = "blue" } },
        });
        await render.UpdateSettingsAsync(new RenderSettingsPatch { Snapshot = new RenderSnapshotPatch { BoxLook = new RenderSnapshotBox { Fill = "green" } } });
        var changed = await render.GetSettingsAsync();
        Assert.Equal(480, changed.FullPage.MaxWidth);
        Assert.IsType<RenderTilesSetting.Off>(changed.Tiles);
        Assert.Equal(3, changed.Snapshot.Scale);
        Assert.Null(changed.Snapshot.BoxLook.Stroke);
        Assert.Equal(800, changed.SlowAfterMs);
        await render.ResetSettingsAsync();
        Assert.Equal(registered, await render.GetSettingsAsync());
    }

    [Fact]
    public async Task ARotatedPageReportsItsTurnAndRendersToMatch()
    {
        var (viewer, document, _, render) = await Opened(new PdfViewerOptions { Render = new(), PageEdit = true }, "report");
        using var edit = await viewer.GetPageEditAsync(document);
        var before = (await viewer.GetPagesAsync(document))[0];
        _ = await edit.SetRotationAsync(PageEditRotation.Clockwise90, [before.Page]);
        var page = (await viewer.GetPagesAsync(document))[0];
        var geometry = page.Geometry;
        Assert.Equal(90, geometry.Rotation);
        Assert.Equal(new PdfSize(before.Height, before.Width), geometry.DisplaySize);
        // report.pdf is portrait (612 by 792); turned, it renders landscape.
        var rendered = await render.RenderPageAsync(page.Page, new RenderPageOptions { Rotation = geometry.Turn, Width = 200 });
        Assert.True(rendered.Width > rendered.Height);
        var thumbnail = await render.RenderThumbnailAsync(page.Page);
        Assert.True(thumbnail.Height > thumbnail.Width);
        var turned = geometry.ToDisplay(thumbnail.Image);
        Assert.Equal((thumbnail.Height, thumbnail.Width), (turned.Width, turned.Height));
    }
}
