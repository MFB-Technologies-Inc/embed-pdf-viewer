using System.Text;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>PageEditTests.swift</c>.</summary>
public sealed class PageEditTests : IAsyncLifetime
{
    PdfViewer viewer = null!;
    DocumentInfo document = null!;
    PageEditCapability edit = null!;

    public async Task InitializeAsync()
    {
        viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { PageEdit = true });
        document = await viewer.OpenAsync(Fixture.Data("report"));
        edit = await viewer.PageEditAsync(document);
    }

    public async Task DisposeAsync() => await viewer.DisposeAsync();

    async Task<IReadOnlyList<PageInfo>> Pages() => await viewer.GetPagesAsync(document);

    static readonly CancellationToken Cancelled = new(true);

    [Fact]
    public async Task RotatingTwiceMakesAHalfTurn()
    {
        var first = (await Pages())[0].Page;
        _ = await edit.RotateAsync([first], PageEditTurn.Clockwise);
        var layout = await edit.RotateAsync([first], PageEditTurn.Clockwise);
        Assert.Equal(180, layout.Pages[0].Rotation);
        Assert.Equal(180, (await Pages())[0].Rotation);
    }

    [Fact]
    public async Task MovingAPageAfterTheLastReordersTheList()
    {
        var pages = (await Pages()).Select(p => p.Page).ToList();
        // Placement names a neighbour page, not an index.
        var layout = await edit.MovePagesAsync([pages[0]], new PageEditPlacement.After(pages[^1]));
        Assert.Equal(pages[0], layout.Pages[^1].Page);
        Assert.Equal(Enumerable.Range(0, 14), layout.Pages.Select(p => p.Index));
    }

    [Fact]
    public async Task EndMovesTheBlockLast()
    {
        var pages = (await Pages()).Select(p => p.Page).ToList();
        var layout = await edit.MovePagesAsync([pages[0], pages[1]], new PageEditPlacement.End());
        Assert.Equal([pages[0], pages[1]], layout.Pages.Select(p => p.Page).TakeLast(2));
        Assert.Equal(14, layout.Pages.Count);
    }

    [Fact]
    public async Task DeletingEveryPageThrowsAndKeepsThePages()
    {
        var all = (await Pages()).Select(p => p.Page).ToList();
        await Assert.ThrowsAsync<PdfException>(() => edit.DeletePagesAsync(all));
        Assert.Equal(14, (await Pages()).Count);
    }

    [Fact]
    public async Task AnInsertedBlankPageTakesItsNeighboursSize()
    {
        var first = (await Pages())[0];
        var insertion = await edit.InsertBlankAsync(new PageEditBlankOptions { Count = 2, Placement = new PageEditPlacement.After(first.Page) });
        Assert.Equal(2, insertion.Inserted.Count);
        Assert.Equal(16, insertion.Pages.Count);
        Assert.Equal(first.Width, insertion.Pages[1].Width);
        Assert.Equal(insertion.Inserted[0], insertion.Pages[1].Page);
    }

    [Fact]
    public async Task ABlankCountBelowZeroIsInvalidInput() =>
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => edit.InsertBlankAsync(new PageEditBlankOptions { Count = -1 }))).Code);

    [Fact]
    public async Task AnExtractOpensAsItsOwnDocument()
    {
        var pages = (await Pages()).Take(3).Select(p => p.Page).ToList();
        var data = await edit.ExtractAsync(pages);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(data, 0, 4));
        Assert.Equal(3, (await viewer.OpenAsync(data)).PageCount);
    }

    [Fact]
    public async Task PagesFromAnotherOpenDocumentInsert()
    {
        var other = await viewer.OpenAsync(Fixture.Data("colors"));
        var source = (await viewer.GetPagesAsync(other)).Select(p => p.Page).ToList();
        var insertion = await edit.InsertAsync(source, other, new PageEditPlacement.Start());
        Assert.Equal(source.Count, insertion.Inserted.Count);
        Assert.Equal(14 + source.Count, insertion.Pages.Count);
    }

    [Fact]
    public async Task PagesFromBytesInsertAndSelectionRoundTrips()
    {
        var insertion = await edit.InsertAsync(Fixture.Data("colors"), new PageEditBytesOptions { PageIndexes = [0], Placement = new PageEditPlacement.End() });
        Assert.Single(insertion.Inserted);
        Assert.Equal(insertion.Inserted[0], insertion.Pages[^1].Page);
        var duplicate = await edit.DuplicateAsync([insertion.Inserted[0]]);
        Assert.Single(duplicate.Inserted);
        await edit.SelectAsync([insertion.Inserted[0]]);
        Assert.Equal([insertion.Inserted[0]], await edit.GetSelectedAsync());
        Assert.True(await edit.CanExtractAsync());
    }

    [Fact]
    public async Task AnEditIsObservedAsPagesChanged()
    {
        var events = viewer.ObserveDocumentEvents();
        var first = (await Pages())[0].Page;
        _ = await edit.SetRotationAsync(PageEditRotation.Clockwise90, [first]);
        var changed = await Fixture.First<DocumentEvent.PagesChanged>(events);
        Assert.Equal(document.Id, changed.DocumentId);
    }

    [Fact]
    public async Task RotateCancelledBeforeItStartsLeavesTheCapabilityWorking()
    {
        var first = (await Pages())[0].Page;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => edit.RotateAsync([first], PageEditTurn.Half, Cancelled));
        Assert.True(await edit.CanEditAsync());
    }

    [Fact]
    public async Task EveryCancellableMemberCancelsBeforeItStarts()
    {
        var page = (await Pages())[0].Page;
        var other = await viewer.OpenAsync(Fixture.Data("colors"));
        var source = (await viewer.GetPagesAsync(other)).Select(p => p.Page).ToList();
        Func<Task>[] calls =
        [
            () => edit.SetRotationAsync(PageEditRotation.Clockwise90, [page], Cancelled),
            () => edit.MovePagesAsync([page], new PageEditPlacement.End(), Cancelled),
            () => edit.DeletePagesAsync([page], Cancelled),
            () => edit.InsertBlankAsync(null, Cancelled),
            () => edit.InsertAsync(Fixture.Data("colors"), null, Cancelled),
            () => edit.InsertAsync(source, other, null, Cancelled),
            () => edit.DuplicateAsync([page], null, Cancelled),
            () => edit.ExtractAsync([page], Cancelled),
        ];
        foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
        Assert.Equal(14, (await Pages()).Count);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => edit.CanEditAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }
}
