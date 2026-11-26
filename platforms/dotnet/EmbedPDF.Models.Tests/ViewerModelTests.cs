using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>ViewerModelTests.swift</c>.</summary>
public class ViewerModelTests
{
    [Fact]
    public Task ANewModelHoldsNoDocument() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync();
        var model = context.Watch(new ViewerModel(viewer));
        Assert.Null(model.ActiveDocument);
        Assert.Equal(0, model.DocumentCount);
        Assert.Null(model.Error);
    });

    [Fact]
    public Task OpensAndClosesReachTheObservingModel() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync();
        var model = context.Watch(new ViewerModel(viewer));
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        DocumentInfo? first = null;
        await Observed.ExpectAsync(model, () => model.DocumentCount, 1, async () => first = await viewer.OpenAsync(Fixture.Data()));
        // The count can reach 1 while the tab still holds its pending ticket;
        // the real id lands with a later revision.
        await Observed.ExpectAsync(model, () => model.ActiveDocument?.Id, first!.Id);
        var second = await viewer.OpenAsync(Fixture.Data("features"));
        await Observed.ExpectAsync(model, () => model.ActiveDocument?.Id, second.Id);
        Assert.Equal(2, model.DocumentCount);
        await Observed.ExpectAsync(model, () => model.DocumentCount, 1, () => viewer.CloseAsync(second));
        Assert.Equal(await viewer.GetDocumentCountAsync(), model.DocumentCount);
        stop.Cancel();
        await observing;
    });

    [Fact]
    public Task DisposingTheViewerEndsTheTaskAndEmptiesTheModel() => SingleThreadContext.Run(async context =>
    {
        var viewer = await Fixture.StartedAsync();
        var model = context.Watch(new ViewerModel(viewer));
        var observing = model.RunAsync();
        await viewer.OpenAsync(Fixture.Data());
        await Observed.ExpectAsync(model, () => model.DocumentCount, 1);
        await viewer.DisposeAsync();
        await observing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, model.DocumentCount);
        Assert.Null(model.ActiveDocument);
    });

    [Fact]
    public Task ARefreshNotifiesObserversOfEachProperty() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync();
        var model = context.Watch(new ViewerModel(viewer));
        var document = await viewer.OpenAsync(Fixture.Data());
        Assert.True(await Observed.FiresAsync(model, nameof(ViewerModel.DocumentCount), () => model.RefreshAsync()));
        Assert.True(await Observed.FiresAsync(model, nameof(ViewerModel.ActiveDocument), async () =>
        {
            await viewer.CloseAsync(document);
            await model.RefreshAsync();
        }));
    });

    [Fact]
    public Task ACancelledTaskReleasesTheModel() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync();
        await Observed.CollectedAsync(await ObserveAndCancel(context, viewer));
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> ObserveAndCancel(SingleThreadContext context, PdfViewer viewer)
    {
        var model = context.Watch(new ViewerModel(viewer));
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await Observed.ExpectAsync(model, () => model.DocumentCount, 1, () => viewer.OpenAsync(Fixture.Data()));
        stop.Cancel();
        await observing;
        return new WeakReference(model);
    }

    [Fact]
    public Task AnOlderReadCannotOverwriteANewerOne() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync();
        var model = context.Watch(new ViewerModel(viewer));
        var (reached, release) = Observed.HoldFirstRead(model.Snapshots);
        var stale = model.RefreshAsync();
        await reached;
        var document = await viewer.OpenAsync(Fixture.Data());
        await model.RefreshAsync();
        Assert.Equal(1, model.DocumentCount);
        release();
        await stale;
        Assert.Equal(1, model.DocumentCount);
        Assert.Equal(document.Id, model.ActiveDocument?.Id);
    });
}
