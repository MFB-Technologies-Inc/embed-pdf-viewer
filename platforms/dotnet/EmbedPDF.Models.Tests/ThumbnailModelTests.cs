namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>ThumbnailModelTests.swift</c>.</summary>
public class ThumbnailModelTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, IReadOnlyList<PageInfo> Pages, RenderCapability Render)> OpenedAsync()
    {
        var viewer = await Fixture.StartedAsync(b => b.Render(new RenderConfig()).PageEdit());
        var document = await viewer.OpenAsync(Fixture.Data());
        return (viewer, document, await viewer.GetPagesAsync(document), await viewer.GetRenderAsync(document));
    }

    /// <summary>
    /// Holds the model's first render, before it reaches the engine, until
    /// <c>release</c> runs or the render is cancelled. <c>reached</c>
    /// completes once the render is held.
    /// </summary>
    static (Task Reached, Action Release) HoldFirstRender(ThumbnailModel model)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = false;
        model.RenderStarted = async token =>
        {
            if (held) return;
            held = true;
            reached.TrySetResult();
            // Ends when the gate opens, and also when the render is cancelled.
            await gate.Task.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        };
        return (reached.Task, () => gate.TrySetResult());
    }

    [Fact]
    public Task ALoadedThumbnailIsTurnedToThePageAsShown() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, unturned, render) = await OpenedAsync();
        // report.pdf is portrait (612 by 792) with no rotation, so the test
        // turns its first page a quarter turn.
        Assert.True(unturned[0].Height > unturned[0].Width);
        using (var edit = await viewer.GetPageEditAsync(document))
        {
            await edit.SetRotationAsync(PageEditRotation.Clockwise90, [unturned[0].Page]);
        }
        var pages = await viewer.GetPagesAsync(document);
        Assert.Equal(90, pages[0].Rotation);
        using var model = context.Watch(new ThumbnailModel(render, pages));
        Assert.Null(model.GetImage(pages[0].Page));
        await model.LoadAsync(pages[0].Page);
        var turned = model.GetImage(pages[0].Page)!;
        Assert.True(turned.Width > turned.Height);
        await model.LoadAsync(pages[1].Page);
        var upright = model.GetImage(pages[1].Page)!;
        Assert.True(upright.Height > upright.Width);
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AnInvalidationMakesTheNextLoadRenderAgain() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        await model.LoadAsync(page);
        Assert.Equal(1, model.RenderCount);
        // The revision is the same, so the cached thumbnail answers.
        await model.LoadAsync(page);
        Assert.Equal(1, model.RenderCount);
        await Observed.ExpectAsync(model, () => model.GetRevision(page), 1L,
            () => render.InvalidateAsync([page], RenderInvalidateScope.Annotations));
        await model.LoadAsync(page);
        Assert.Equal(2, model.RenderCount);
        Assert.NotNull(model.GetImage(page));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task TwoLoadsOfOnePageShareOneRender() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        await Task.WhenAll(model.LoadAsync(page), model.LoadAsync(page));
        Assert.Equal(1, model.RenderCount);
        Assert.NotNull(model.GetImage(page));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ALoadCancelledInFlightLeavesNoImageAndNoError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        var (reached, release) = HoldFirstRender(model);
        using var cancel = new CancellationTokenSource();
        var load = model.LoadAsync(page, cancel.Token);
        await reached;
        Assert.Equal(1, model.RenderCount);
        Assert.Equal(1, model.WaiterCount(page));
        // The gate stays shut, so the load returns only because its
        // cancellation cancelled the held render.
        cancel.Cancel();
        await load.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(model.GetImage(page));
        Assert.Null(model.Error);
        Assert.Equal(0, model.WaiterCount(page));
        // The page loads once someone waits for it again.
        release();
        await model.LoadAsync(page);
        Assert.NotNull(model.GetImage(page));
        Assert.Equal(2, model.RenderCount);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ACancelledWaiterLeavesTheRenderToTheOtherWaiter() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        var (reached, release) = HoldFirstRender(model);
        using var cancel = new CancellationTokenSource();
        var first = model.LoadAsync(page, cancel.Token);
        await reached;
        var second = model.LoadAsync(page);
        while (model.WaiterCount(page) < 2) await Task.Yield();
        cancel.Cancel();
        while (model.WaiterCount(page) > 1) await Task.Yield();
        release();
        await second;
        await first;
        Assert.NotNull(model.GetImage(page));
        Assert.Null(model.Error);
        Assert.Equal(1, model.RenderCount);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AnInvalidationInFlightSupersedesTheRender() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        var (reached, _) = HoldFirstRender(model);
        var stale = model.LoadAsync(page);
        await reached;
        await Observed.ExpectAsync(model, () => model.GetRevision(page), 1L,
            () => render.InvalidateAsync([page], RenderInvalidateScope.Annotations));
        // The gate stays shut: this load cancels the held render and starts
        // one for the new revision.
        await model.LoadAsync(page);
        await stale;
        Assert.Equal(2, model.RenderCount);
        Assert.NotNull(model.GetImage(page));
        Assert.Null(model.Error);
        // The image is cached at the new revision, so nothing renders again.
        await model.LoadAsync(page);
        Assert.Equal(2, model.RenderCount);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AFailedLoadKeepsTheEarlierImageAndSetsTheError() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, document, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages));
        var page = pages[0].Page;
        await model.LoadAsync(page);
        var earlier = model.GetImage(page);
        Assert.NotNull(earlier);
        await Observed.ExpectAsync(model, () => model.GetRevision(page), 1L,
            () => render.InvalidateAsync([page], RenderInvalidateScope.Annotations));
        await viewer.CloseAsync(document);
        // The page is invalidated, so this load renders, and the closed
        // document refuses it.
        await model.LoadAsync(page);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        Assert.Same(earlier, model.GetImage(page));
        await model.LoadAsync(pages[1].Page);
        Assert.Null(model.GetImage(pages[1].Page));
        Assert.Equal(3, model.RenderCount);
        // Each load of a failed page tries again.
        await model.LoadAsync(page);
        Assert.Equal(4, model.RenderCount);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task APageOutsideTheModelFailsWithoutRendering() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages.Skip(1)));
        await model.LoadAsync(pages[0].Page);
        Assert.Equal(0, model.RenderCount);
        Assert.Equal(PdfErrorCode.NotFound, model.Error?.Code);
        Assert.Null(model.GetImage(pages[0].Page));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task TheCacheDropsThePageLoadedLongestAgo() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var model = context.Watch(new ThumbnailModel(render, pages)) ;
        model.CacheLimit = 2;
        await model.LoadAsync(pages[0].Page);
        await model.LoadAsync(pages[1].Page);
        // A cached load still counts as a use, so page 1 is now the oldest.
        await model.LoadAsync(pages[0].Page);
        await model.LoadAsync(pages[2].Page);
        Assert.NotNull(model.GetImage(pages[0].Page));
        Assert.Null(model.GetImage(pages[1].Page));
        Assert.NotNull(model.GetImage(pages[2].Page));
        await viewer.DisposeAsync();
    });
}
