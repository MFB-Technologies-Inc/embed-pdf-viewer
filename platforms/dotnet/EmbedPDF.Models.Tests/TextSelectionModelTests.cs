using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>TextSelectionModelTests.swift</c>.</summary>
public class TextSelectionModelTests
{
    static readonly PageRef MissingPage = new(int.MaxValue);

    static async Task<(PdfViewer Viewer, DocumentInfo Document)> OpenedDocumentAsync()
    {
        var viewer = await Fixture.StartedAsync(b => b.Interaction().Selection());
        return (viewer, await viewer.OpenAsync(Fixture.Data()));
    }

    /// <summary>
    /// The viewer, its first page and selection. A test finds its points
    /// first and then makes the model: in .NET a selection event can reach the
    /// model after the call that caused it returns, so the probes' own select
    /// and clear would otherwise land on the model late.
    /// </summary>
    static async Task<(PdfViewer Viewer, PageInfo Page, SelectionCapability Selection)> OpenedAsync()
    {
        var (viewer, document) = await OpenedDocumentAsync();
        var page = (await viewer.GetPagesAsync(document))[0];
        return (viewer, page, await viewer.GetSelectionAsync(document));
    }

    static async Task<PdfPoint> PointOverTextAsync(PageInfo page, SelectionCapability selection)
    {
        await selection.EnsureLoadedAsync(page.Page);
        await selection.SelectPageAsync(page.Page);
        var first = (await selection.GetRectsAsync(page.Page))[0];
        await selection.ClearAsync();
        return new PdfPoint(first.X + first.Width / 2, first.Y + first.Height / 2);
    }

    static async Task<PdfPoint> PointOffTextAsync(PageInfo page, SelectionCapability selection)
    {
        await selection.EnsureLoadedAsync(page.Page);
        await selection.SelectPageAsync(page.Page);
        var rects = await selection.GetRectsAsync(page.Page);
        await selection.ClearAsync();
        PdfPoint[] candidates = [new(2, 2), new(page.Width - 2, 2), new(2, page.Height - 2), new(page.Width - 2, page.Height - 2)];
        foreach (var point in candidates)
        {
            var inRect = rects.Any(r => point.X >= r.X && point.X <= r.X + r.Width && point.Y >= r.Y && point.Y <= r.Y + r.Height);
            if (!inRect && !await selection.IsOverTextAsync(page.Page, point)) return point;
        }
        throw new InvalidOperationException("no point off text on the page");
    }

    static async Task<IReadOnlyList<PdfRect>> SelectionRectsAsync(PdfPoint start, PdfPoint end, PageInfo page, SelectionCapability selection)
    {
        await selection.ClearAsync();
        await selection.EnsureLoadedAsync(page.Page);
        Assert.True(await selection.BeginGestureAtAsync(page.Page, start));
        await selection.ExtendToAsync(page.Page, end);
        await selection.EndGestureAsync();
        return await selection.GetRectsAsync(page.Page);
    }

    /// <summary>Holds the first rects refresh between its reads and its write until <c>release</c> runs.</summary>
    static (Task Reached, Action Release) HoldFirstRectsRead(TextSelectionModel model)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = false;
        model.RectsReadReturned = async () =>
        {
            if (held) return;
            held = true;
            reached.TrySetResult();
            await gate.Task;
        };
        return (reached.Task, () => gate.TrySetResult());
    }

    static bool HasRects(TextSelectionModel model, PageRef page) => model.Rects.TryGetValue(page, out var rects) && rects.Count > 0;

    [Fact]
    public Task DraggingAcrossTextSelectsIt() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var start = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        await Observed.ExpectAsync(model, () => model.IsSelecting, true, () =>
        {
            model.DragBegan(start, page.Page);
            return Task.CompletedTask;
        });
        model.DragChanged(new PdfPoint(start.X + 120, start.Y), page.Page);
        Assert.True(await model.DragEndedAsync());
        Assert.False(model.IsSelecting);
        var engineRects = await selection.GetRectsAsync(page.Page);
        Assert.NotEmpty(engineRects);
        Assert.Equal(engineRects, model.Rects[page.Page]);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task PointsQueuedBehindOneExtensionCollapseIntoIt() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var start = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        await Observed.ExpectAsync(model, () => model.IsSelecting, true, () =>
        {
            model.DragBegan(start, page.Page);
            return Task.CompletedTask;
        });
        var end = new PdfPoint(start.X + 160, start.Y);
        model.DragChanged(new PdfPoint(start.X + 20, start.Y), page.Page);
        model.DragChanged(new PdfPoint(start.X + 40, start.Y), page.Page);
        model.DragChanged(end, page.Page);
        await model.DragEndedAsync();
        Assert.Equal(1, model.ExtensionCount);
        var collapsed = model.Rects[page.Page];
        Assert.Equal(await SelectionRectsAsync(start, end, page, selection), collapsed);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ChangesAndEndsMadeBeforeTheBeginLandsRunAfterIt() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var start = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        // No suspension between the three calls, so the begin has not reached
        // the engine when the change and the end are made.
        model.DragBegan(start, page.Page);
        model.DragChanged(new PdfPoint(start.X + 120, start.Y), page.Page);
        Assert.True(await model.DragEndedAsync());
        Assert.False(model.IsSelecting);
        Assert.False(await selection.IsSelectingAsync());
        Assert.Equal(1, model.ExtensionCount);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ADragThatStartsOffTextSelectsNothing() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var margin = await PointOffTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        model.DragBegan(margin, page.Page);
        model.DragChanged(new PdfPoint(300, 300), page.Page);
        await model.DragEndedAsync();
        Assert.Empty(model.Rects);
        Assert.Equal(0, model.ExtensionCount);
        Assert.False(model.IsSelecting);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task DragEndedReportsWhetherTextWasUnderTheStart() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var margin = await PointOffTextAsync(page, selection);
        var text = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        model.DragBegan(margin, page.Page);
        Assert.False(await model.DragEndedAsync());
        model.DragBegan(text, page.Page);
        Assert.True(await model.DragEndedAsync());
        // An end with no begin before it closes nothing.
        Assert.False(await model.DragEndedAsync());
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task RectsAreFinalAfterTheEndEvenWhenAnEventRefreshInterleaves() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document) = await OpenedDocumentAsync();
        var page = (await viewer.GetPagesAsync(document))[0];
        var selection = await viewer.GetSelectionAsync(document);
        var start = await PointOverTextAsync(page, selection);
        // A select-all spans only the pages whose text is loaded; each page
        // still loading joins it later with one more changed event, hundreds
        // of milliseconds after the call returns. With every page loaded the
        // select-all sends its events before it returns.
        foreach (var each in await viewer.GetPagesAsync(document)) await selection.EnsureLoadedAsync(each.Page);
        var model = context.Watch(new TextSelectionModel(selection));
        // Subscribed after the model, so every event this stream gets also
        // reaches the model.
        using var counting = new CancellationTokenSource();
        var events = selection.ObserveEvents(counting.Token).GetAsyncEnumerator();
        var followed = model.EventsFollowed;
        var (reached, release) = HoldFirstRectsRead(model);
        model.DragBegan(start, page.Page);
        // The begin's refresh has read the caret's rects and not applied them.
        await reached;
        await selection.SelectAllAsync();
        // Every event the begin and the select-all sent is buffered in the
        // stream by now. Count them without waiting for more.
        var sent = 0;
        Task<bool> waiting;
        while (true)
        {
            var next = events.MoveNextAsync();
            if (!next.IsCompleted)
            {
                waiting = next.AsTask();
                break;
            }
            if (!next.Result)
            {
                waiting = Task.FromResult(false);
                break;
            }
            sent++;
        }
        Assert.True(sent > 0);
        counting.Cancel();
        try { await waiting; }
        catch (OperationCanceledException) { }
        await events.DisposeAsync();
        // In .NET an event can reach the model after the call that sent it
        // returns, so wait until the model has taken each one: every refresh
        // they queue is then behind the begin and ahead of the end.
        while (model.EventsFollowed - followed < sent) await Task.Delay(5);
        release();
        await model.DragEndedAsync();
        var pages = await selection.GetSelectedPagesAsync();
        Assert.NotEmpty(pages);
        foreach (var selected in pages)
        {
            Assert.Equal(await selection.GetRectsAsync(selected), model.Rects[selected]);
        }
        Assert.Equal(pages.Count, model.Rects.Count);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ANewDragClearsTheLastError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var start = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        // No page of the document has this object number, so selecting the
        // word throws.
        model.DragBegan(start, MissingPage, SelectionStart.Word);
        await model.DragEndedAsync();
        Assert.NotNull(model.Error);
        model.DragBegan(start, page.Page);
        Assert.True(await model.DragEndedAsync());
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ClearingClearsTheLastError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var start = await PointOverTextAsync(page, selection);
        var model = context.Watch(new TextSelectionModel(selection));
        model.DragBegan(start, MissingPage, SelectionStart.Word);
        await model.DragEndedAsync();
        Assert.NotNull(model.Error);
        await model.ClearAsync();
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task SelectionsMadeElsewhereReachTheModelUntilClose() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var model = context.Watch(new TextSelectionModel(selection));
        await Observed.ExpectAsync(model, () => HasRects(model, page.Page), true, async () =>
        {
            await selection.EnsureLoadedAsync(page.Page);
            await selection.SelectPageAsync(page.Page);
        });
        await Observed.ExpectAsync(model, () => model.Rects.Count == 0, true, () => model.ClearAsync());
        Assert.False(await selection.HasSelectionAsync());
        await viewer.DisposeAsync();
        // The disposal ends the stream, which ends the loop.
        await model.ObservationTask.WaitAsync(TimeSpan.FromSeconds(10));
    });

    [Fact]
    public Task ASelectionMadeRightAfterInitReachesTheModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var model = context.Watch(new TextSelectionModel(selection));
        // Sent before the model's own loop has had a turn to run.
        await selection.EnsureLoadedAsync(page.Page);
        await selection.SelectPageAsync(page.Page);
        await Observed.ExpectAsync(model, () => HasRects(model, page.Page), true);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ASelectionClearedElsewhereEmptiesTheRects() => SingleThreadContext.Run(async context =>
    {
        var (viewer, page, selection) = await OpenedAsync();
        var model = context.Watch(new TextSelectionModel(selection));
        await Observed.ExpectAsync(model, () => HasRects(model, page.Page), true, async () =>
        {
            await selection.EnsureLoadedAsync(page.Page);
            await selection.SelectPageAsync(page.Page);
        });
        await Observed.ExpectAsync(model, () => model.Rects.Count == 0, true, () => selection.ClearAsync());
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ClosingTheDocumentEndsTheObservation() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, document) = await OpenedDocumentAsync();
        using var model = context.Watch(new TextSelectionModel(await viewer.GetSelectionAsync(document)));
        await viewer.CloseAsync(document);
        await model.ObservationTask.WaitAsync(TimeSpan.FromSeconds(10));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AReleasedModelStopsObservingWhileTheDocumentStaysOpen() => SingleThreadContext.RunWatched(async context =>
    {
        var (viewer, document) = await OpenedDocumentAsync();
        var page = (await viewer.GetPagesAsync(document))[0];
        var selection = await viewer.GetSelectionAsync(document);
        var (released, observation) = await ObserveOnce(context, selection, page.Page);
        // The document is still open, so the loop ends only if the release
        // cancelled it.
        await Observed.CollectedAsync(released);
        await observation.WaitAsync(TimeSpan.FromSeconds(10));
        await viewer.DisposeAsync();
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<(WeakReference, Task)> ObserveOnce(SingleThreadContext context, SelectionCapability selection, PageRef page)
    {
        var model = context.Watch(new TextSelectionModel(selection));
        // The model is observing by the time it is released.
        await Observed.ExpectAsync(model, () => HasRects(model, page), true, async () =>
        {
            await selection.EnsureLoadedAsync(page);
            await selection.SelectPageAsync(page);
        });
        return (new WeakReference(model), model.ObservationTask);
    }
}
