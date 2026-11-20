using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>OutlineTests.swift</c>.</summary>
public sealed class OutlineTests
{
    [Fact]
    public async Task NestedOutlineEditsPersistAndPatchClearingIsExplicit()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Outline = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var outline = await viewer.OutlineAsync(document);
        await outline.EnsureLoadedAsync();
        Assert.Equal(ResourceStatus.Ready, await outline.GetStatusAsync());
        Assert.False(await outline.IsIncompleteAsync());
        var events = outline.ObserveEvents();
        var parent = await outline.CreateAsync(new OutlineItemInput { Title = "Parent", ClearTarget = true }, new OutlineCreateOptions { Select = true });
        var created = await Fixture.First(events, e => e as OutlineEvent.Changed);
        Assert.Contains(parent.ItemRef, created.Items);
        Assert.Equal(DocumentEventOriginKind.Local, created.Origin.Kind);
        Assert.Equal([parent.ItemRef], await outline.GetSelectedAsync());
        var child = await outline.CreateAsync(new OutlineItemInput { Title = "Child", Parent = parent.ItemRef, ClearTarget = true, Color = "#ff0000" });
        await outline.ExpandAsync(parent.ItemRef);
        Assert.True(await outline.IsExpandedAsync(parent.ItemRef));
        var rows = await outline.ListRowsAsync();
        Assert.Equal(1, rows.First(r => r.Item.ItemRef == child.ItemRef).Depth);
        Assert.Equal([child.ItemRef], (await outline.GetChildrenAsync(parent.ItemRef)).Select(i => i.ItemRef));
        var cleared = await outline.UpdateAsync(child.ItemRef, new OutlineItemPatch { Title = "Renamed", Clear = [OutlinePatchKey.Color, OutlinePatchKey.Target] });
        Assert.Equal("Renamed", cleared.Title);
        Assert.Null(cleared.Color);
        await outline.MoveAsync([child.ItemRef], null, new OutlinePosition.Start());
        var moved = Assert.IsType<OutlineItem>(await outline.GetItemAsync(child.ItemRef));
        Assert.Null(moved.Parent);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => outline.RefreshAsync(new CancellationToken(true)));
        var bytes = await viewer.SaveAsync(document);
        var reopened = await viewer.OpenAsync(bytes);
        using var saved = await viewer.OutlineAsync(reopened);
        await saved.EnsureLoadedAsync();
        Assert.Equal("Renamed", (await saved.GetItemAsync(child.ItemRef))?.Title);
        await outline.DeleteAsync([child.ItemRef, parent.ItemRef]);
        Assert.Null(await outline.GetItemAsync(parent.ItemRef));
        await viewer.CloseAsync(document);
        Assert.True(await MetadataTests.Ends(outline.ObserveEvents()));
    }

    [Fact]
    public async Task NavigationMembersAnswer()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Outline = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var outline = await viewer.OutlineAsync(document);
        await outline.EnsureLoadedAsync();
        var page = (await viewer.GetPagesAsync(document))[1].Page;
        var target = new PageLinkTarget.GoTo(new PageDestination.Fit(page));
        var item = await outline.CreateAsync(new OutlineItemInput { Title = "Two", Target = target });
        Assert.Equal(target, item.Target);
        Assert.Contains(item.ItemRef, (await outline.ListAsync()).Select(i => i.ItemRef));
        // Without a stage the destination comes back for the host to follow.
        Assert.IsType<LinkActivation.Destination>(await outline.ActivateAsync(item.ItemRef));
        await outline.ToggleAsync(item.ItemRef);
        await outline.RevealAsync(item.ItemRef);
        await outline.ExpandAllAsync();
        await outline.CollapseAllAsync();
        await outline.CollapseAsync(item.ItemRef);
        Assert.False(await outline.IsExpandedAsync(item.ItemRef));
        await outline.SelectAsync([item.ItemRef]);
        Assert.Equal([item.ItemRef], await outline.GetSelectedAsync());
        _ = await outline.GetCurrentItemAsync();
        _ = await outline.GetCurrentViewTargetAsync();
    }

    [Fact]
    public async Task EditingIsUnavailableWithRestrictedScope()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Scope = ["doc.open"], Outline = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var outline = await viewer.OutlineAsync(document);
        Assert.False(await outline.CanEditAsync());
        Assert.Equal(PdfErrorCode.PermissionDenied, (await Assert.ThrowsAsync<PdfException>(() => outline.CreateAsync(new OutlineItemInput { Title = "Denied" }))).Code);
    }
}
