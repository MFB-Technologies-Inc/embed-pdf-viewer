using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// An argument that is present but invalid (a negative index or page number,
/// an undefined enum value, a negative size) is reported by the returned task,
/// never thrown while the call is made, so <c>var task = call(); await task;</c>
/// and <c>await call()</c> fail alike. Null arguments are
/// <see cref="NullArgumentTests"/>' business.
/// </summary>
public sealed class ValidationShapeTests
{
    static readonly PageRef Bad = new(-1);
    static readonly OutlineItemRef BadItem = new(-1);

    static async Task ExpectFaulted(string name, Func<Task> call)
    {
        Task task;
        try
        {
            task = call();
        }
        catch (Exception error)
        {
            Assert.Fail($"{name} threw {error.GetType().Name} synchronously: {error.Message}");
            return;
        }
        var failure = await Assert.ThrowsAsync<PdfException>(() => task);
        Assert.Equal(PdfErrorCode.InvalidInput, failure.Code);
    }

    [Fact]
    public async Task InvalidArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Metadata = true, Search = new(), Render = new(), History = true, Outline = true, Attachment = true,
            PageEdit = true, ViewManager = true, Shell = true,
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using var edit = await viewer.GetPageEditAsync(document);
        using var render = await viewer.GetRenderAsync(document);
        using var outline = await viewer.GetOutlineAsync(document);
        using var attachments = await viewer.GetAttachmentsAsync(document);
        using var manager = await viewer.GetViewManagerAsync();
        using var search = await viewer.GetSearchAsync(document);
        using var metadata = await viewer.GetMetadataAsync(document);
        var view = await render.CreateViewDemandAsync("main");
        var file = new AttachedFile("x", new AttachmentHome.Document(new AttachmentRef("x")), "x", null, null, -1, null, null, null);
        var calls = new (string, Func<Task>)[]
        {
            ("PageEdit.SelectAsync", () => edit.SelectAsync([Bad])),
            ("PageEdit.RotateAsync", () => edit.RotateAsync([page], (PageEditTurn)9)),
            ("PageEdit.SetRotationAsync", () => edit.SetRotationAsync((PageEditRotation)9, [page])),
            ("PageEdit.MovePagesAsync", () => edit.MovePagesAsync([Bad], new PageEditPlacement.End())),
            ("PageEdit.DeletePagesAsync", () => edit.DeletePagesAsync([Bad])),
            ("PageEdit.InsertBlankAsync", () => edit.InsertBlankAsync(new PageEditBlankOptions { Count = -1 })),
            ("PageEdit.InsertAsync(bytes)", () => edit.InsertAsync(Fixture.Data("colors"), new PageEditBytesOptions { PageIndexes = [-1] })),
            ("PageEdit.InsertAsync(document)", () => edit.InsertAsync([Bad], document)),
            ("PageEdit.DuplicateAsync", () => edit.DuplicateAsync([Bad])),
            ("PageEdit.ExtractAsync", () => edit.ExtractAsync([Bad])),
            ("Render.RenderPageAsync", () => render.RenderPageAsync(Bad)),
            ("Render.RenderThumbnailAsync", () => render.RenderThumbnailAsync(page, (ContentInclusion)9)),
            ("Render.RenderPagesAsync", () => render.RenderPagesAsync([page], concurrency: -1)),
            ("Render.GetRenderEpochAsync", () => render.GetRenderEpochAsync(Bad)),
            ("Render.InvalidateAsync", () => render.InvalidateAsync([page], (RenderInvalidateScope)9)),
            ("Render.RenderSourceAsync", () => render.RenderSourceAsync(Bad, 1)),
            ("Render.GetSourceKeyAsync", () => render.GetSourceKeyAsync(Bad, 1)),
            ("Render.ConformViewportAsync", () => render.ConformViewportAsync(Bad, 1)),
            ("Render.GetSnapshotBoxAsync", () => render.GetSnapshotBoxAsync(Bad)),
            ("Render.UpdateSettingsAsync", () => render.UpdateSettingsAsync(new RenderSettingsPatch { Format = (RenderEncodingFormat)9 })),
            ("RenderViewDemand.SetDemandAsync", () => view.SetDemandAsync(Bad, new RenderPageViewDemand(80))),
            ("RenderViewDemand.GetPlanAsync", () => view.GetPlanAsync(Bad)),
            ("RenderViewDemand.MarkPaintedAsync", () => view.MarkPaintedAsync(Bad, "k")),
            ("RenderViewDemand.MarkUnpaintedAsync", () => view.MarkUnpaintedAsync(Bad, "k")),
            ("RenderViewDemand.ReleaseAsync", () => view.ReleaseAsync(Bad)),
            ("Outline.GetItemAsync", () => outline.GetItemAsync(BadItem)),
            ("Outline.GetChildrenAsync", () => outline.GetChildrenAsync(BadItem)),
            ("Outline.SelectAsync", () => outline.SelectAsync([BadItem])),
            ("Outline.IsExpandedAsync", () => outline.IsExpandedAsync(BadItem)),
            ("Outline.ActivateAsync", () => outline.ActivateAsync(BadItem)),
            ("Outline.CreateAsync", () => outline.CreateAsync(new OutlineItemInput { Parent = BadItem })),
            ("Outline.UpdateAsync", () => outline.UpdateAsync(BadItem, new OutlineItemPatch())),
            ("Outline.MoveAsync", () => outline.MoveAsync([BadItem], null)),
            ("Outline.DeleteAsync", () => outline.DeleteAsync([BadItem])),
            ("Outline.ExpandAsync", () => outline.ExpandAsync(BadItem)),
            ("Outline.CollapseAsync", () => outline.CollapseAsync(BadItem)),
            ("Outline.ToggleAsync", () => outline.ToggleAsync(BadItem)),
            ("Outline.RevealAsync", () => outline.RevealAsync(BadItem)),
            ("Attachment.SelectAsync", () => attachments.SelectAsync([file])),
            ("Attachment.IsPendingAsync", () => attachments.IsPendingAsync(file)),
            ("Attachment.CanRemoveAsync", () => attachments.CanRemoveAsync(file)),
            ("Attachment.RevealAsync", () => attachments.RevealAsync(file)),
            ("Attachment.RemoveAsync", () => attachments.RemoveAsync(file)),
            ("Attachment.DownloadAsync", () => attachments.DownloadAsync(file)),
            ("ViewManager.MovePaneAsync", () => manager.MovePaneAsync("pane-1", -1)),
            ("ViewManager.AddDocumentAsync", () => manager.AddDocumentAsync(document, "pane-1", -1)),
            ("ViewManager.MoveDocumentAsync(within)", () => manager.MoveDocumentAsync(document, "pane-1", -1)),
            ("ViewManager.MoveDocumentAsync(between)", () => manager.MoveDocumentAsync(document, "pane-1", "pane-2", -1)),
            ("Search.SearchAsync", () => search.SearchAsync(new SearchQuery("x"), Bad)),
            ("Search.GoToHitAsync", () => search.GoToHitAsync(-1)),
            ("Search.GetHitsAsync", () => search.GetHitsAsync(Bad)),
            ("Search.GetHitCountAsync", () => search.GetHitCountAsync(Bad)),
            ("Search.NextHitAsync", () => search.NextHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.PreviousHitAsync", () => search.PreviousHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.RevealActiveHitAsync", () => search.RevealActiveHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.FindAllAsync", () => search.FindAllAsync(new SearchQuery("x"), (ContentInclusion)9)),
            ("Search.UpdateSettingsAsync", () => search.UpdateSettingsAsync(new SearchSettingsPatch { Highlight = new SearchHighlightPatch { BlendMode = (SearchBlendMode)99 } })),
            ("Metadata.UpdateAsync", () => metadata.UpdateAsync(new MetadataPatch { Clear = [(MetadataField)9] })),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
        await view.DisposeAsync();
    }
}
