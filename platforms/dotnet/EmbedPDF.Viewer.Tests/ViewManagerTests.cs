using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>ViewManagerTests.swift</c>.</summary>
public sealed class ViewManagerTests : IAsyncLifetime
{
    PdfViewer viewer = null!;
    DocumentInfo document = null!;
    ViewManagerCapability manager = null!;

    public async Task InitializeAsync()
    {
        viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { ViewManager = true });
        manager = await viewer.GetViewManagerAsync();
        document = await viewer.OpenAsync(Fixture.Data("report"));
    }

    public async Task DisposeAsync() => await viewer.DisposeAsync();

    [Fact]
    public async Task TheFirstDocumentLandsInTheDefaultPane()
    {
        var pane = Assert.Single(await manager.ListPanesAsync());
        Assert.Equal(("pane-1", document.Id), (pane.Id, pane.ActiveDocumentId));
        Assert.Equal([document.Id], pane.DocumentIds);
        Assert.Equal("pane-1", await manager.GetFocusedPaneIdAsync());
    }

    [Fact]
    public async Task SplittingIsObserved()
    {
        var colors = await viewer.OpenAsync(Fixture.Data("colors"));
        var events = manager.ObserveEvents();
        var pane = await manager.SplitPaneAsync(colors);
        var moved = await Fixture.First(events, e => e as ViewManagerEvent.DocumentMoved);
        Assert.Equal(new ViewManagerEvent.DocumentMoved(colors.Id, "pane-1", pane), moved);
        Assert.Equal(pane, await manager.GetPaneOfDocumentAsync(colors));
    }

    [Fact]
    public async Task ClosingADocumentTakesItOutOfItsPane()
    {
        var colors = await viewer.OpenAsync(Fixture.Data("colors"));
        await manager.SetActiveDocumentAsync(document, "pane-1");
        await viewer.CloseAsync(document);
        var pane = await manager.GetPaneAsync("pane-1");
        Assert.Equal([colors.Id], pane?.DocumentIds);
        Assert.Equal(colors.Id, pane?.ActiveDocumentId);
    }

    [Fact]
    public async Task DocumentsMoveBetweenPanesAsDocumentInfos()
    {
        var colors = await viewer.OpenAsync(Fixture.Data("colors"));
        var pane = await manager.CreatePaneAsync([colors]);
        await manager.AddDocumentAsync(document, pane, 0);
        Assert.Equal([document.Id, colors.Id], (await manager.GetPaneAsync(pane))?.DocumentIds);
        await manager.MoveDocumentAsync(colors, pane, 0);
        await manager.SetActiveDocumentAsync(colors, pane);
        var info = await manager.GetPaneAsync(pane);
        Assert.Equal([colors.Id, document.Id], info?.DocumentIds);
        Assert.Equal(colors.Id, info?.ActiveDocumentId);
        await manager.MoveDocumentAsync(document, pane, "pane-1");
        Assert.Equal("pane-1", await manager.GetPaneOfDocumentAsync(document));
        await manager.RemoveDocumentAsync(colors, pane);
        Assert.Null(await manager.GetPaneOfDocumentAsync(colors));
    }

    [Fact]
    public async Task AnIndexPastTheEndAppends()
    {
        var second = await manager.CreatePaneAsync();
        await manager.MovePaneAsync("pane-1", int.MaxValue);
        Assert.Equal([second, "pane-1"], await manager.GetPaneOrderAsync());
    }

    [Fact]
    public async Task ANegativeIndexIsInvalidInput()
    {
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => manager.MovePaneAsync("pane-1", -1))).Code);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => manager.AddDocumentAsync(document, "pane-1", -1))).Code);
    }

    [Fact]
    public async Task FocusAndRemovalAreObserved()
    {
        var second = await manager.CreatePaneAsync();
        var events = manager.ObserveEvents();
        await manager.SetFocusedPaneAsync("pane-1");
        Assert.Equal("pane-1", (await Fixture.First(events, e => e as ViewManagerEvent.FocusChanged)).PaneId);
        var removals = manager.ObserveEvents();
        await manager.RemovePaneAsync(second, "pane-1");
        Assert.Equal(second, (await Fixture.First(removals, e => e as ViewManagerEvent.PaneRemoved)).PaneId);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in manager.ObserveEvents(cancel.Token)) { }
        });
        Assert.Equal(0, viewer.StreamCountForTests);
        _ = await manager.CreatePaneAsync();
    }

    [Fact]
    public async Task DisposingTheViewerFinishesTheEventsAndTheCapabilityThrows()
    {
        var events = MetadataTests.Ends(manager.ObserveEvents());
        await Task.Delay(50);
        await viewer.DisposeAsync();
        Assert.True(await events);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => manager.ListPanesAsync())).Code);
    }
}
