using uniffi.epdf_ffi;
using uniffi.epdf_plugin_metadata;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class DocumentAPITests
{
    static async Task UpdateTitle(PdfViewer viewer, DocumentInfo document, string title)
    {
        using var metadata = await viewer.CallAsync(h => h.Metadata(document.Id), default);
        using var token = new CancelToken();
        _ = await metadata.Update(new MetadataPatch(Title: title), token);
    }

    static async Task<string?> Title(PdfViewer viewer, DocumentInfo document)
    {
        using var metadata = await viewer.CallAsync(h => h.Metadata(document.Id), default);
        using var token = new CancelToken();
        await metadata.EnsureLoaded(token);
        return (await metadata.Snapshot())?.Title;
    }

    [Fact]
    public async Task DocumentControlsUseTheCurrentKernelRegistry()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Metadata = true });
        var first = await viewer.OpenAsync(Fixture.Data("report"));
        var second = await viewer.OpenAsync(Fixture.Data("report"), new OpenDocumentOptions { Activate = false });
        Assert.Equal(first.Id, await viewer.GetActiveDocumentIdAsync());
        await viewer.RenameAsync(first, "");
        Assert.Equal("", (await viewer.GetDocumentAsync(first.Id))?.Name);
        await viewer.SetActiveAsync(second);
        var page = Assert.IsType<PageInfo>(await viewer.GetPageAtAsync(0));
        Assert.Equal(page, await viewer.GetPageAsync(page.Page));
        Assert.Equal(0, await viewer.GetPageIndexAsync(page.Page));
        Assert.Equal(0, await viewer.GetPageRevisionAsync());
        await viewer.MoveAsync(second, 0);
        Assert.Equal([second.Id, first.Id], await viewer.GetDocumentOrderAsync());
        await viewer.SwapAsync(first, second);
        Assert.Equal([first.Id, second.Id], await viewer.GetDocumentOrderAsync());
        await viewer.SetDocumentOrderAsync([second.Id, first.Id]);
        Assert.True(await viewer.HasDocumentAsync(first.Id));
        await UpdateTitle(viewer, first, "Saved layer title");
        var bytes = await viewer.SaveLayerAsync(first);
        var restored = await viewer.OpenLayerAsync(Fixture.Data("report"), bytes);
        Assert.Equal(14, restored.PageCount);
        Assert.Equal("Saved layer title", await Title(viewer, restored));
        await viewer.CloseAsync(second);
        Assert.Null(await viewer.GetDocumentAsync(second.Id));
        Assert.Null(await viewer.GetPageAsync(page.Page, second));
        Assert.Equal(-1, await viewer.GetPageRevisionAsync(second));
        await viewer.CloseAllAsync();
        Assert.Equal(0, await viewer.GetDocumentCountAsync());
        Assert.Null(await viewer.GetActiveDocumentAsync());
    }

    [Fact]
    public async Task SettingsPreserveOmissionsAndClearWithoutGrantingDownload()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Scope = ["doc.open"] });
        var initial = await viewer.GetSettingsAsync();
        await using var revisions = viewer.ObserveStateRevisions().GetAsyncEnumerator();
        await viewer.UpdateSettingsAsync(new ViewerSettingsPatch { Accent = "#123456", PageBackground = "transparent" });
        Assert.True(await revisions.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await viewer.UpdateSettingsAsync(new ViewerSettingsPatch { Clear = [ViewerSettingsField.Accent] });
        var current = await viewer.GetSettingsAsync();
        Assert.Null(current.Accent);
        Assert.Equal("transparent", current.PageBackground);
        Assert.Equal(initial.PageShadow, current.PageShadow);
        await viewer.ResetSettingsAsync();
        Assert.Equal(initial, await viewer.GetSettingsAsync());
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        Assert.False(await viewer.CanDownloadAsync(document));
        Assert.False(await viewer.AllowsAsync(DocumentPermission.Download, document));
        Assert.True(await viewer.AllowsAsync("doc.open", document));
        Assert.Equal(PdfErrorCode.PermissionDenied, (await Assert.ThrowsAsync<PdfException>(() => viewer.SaveLayerAsync(document))).Code);
    }

    [Fact]
    public async Task TheHttpFetcherKeepsFailedTabsAndRefetchesOnRetry()
    {
        var statuses = new Queue<int>([404, 200]);
        var body = Fixture.Data("report");
        await using var server = Loopback.Start(context =>
        {
            int status;
            lock (statuses) status = statuses.Count > 0 ? statuses.Dequeue() : 200;
            return Loopback.Reply(context, status, body);
        });
        using var client = new HttpClient();
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { DocumentFetcher = new HttpClientDocumentFetcher(client) });
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(server.Url("report.pdf")));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
        var failed = (await viewer.GetDocumentsAsync()).Single();
        Assert.Equal(DocumentStatus.Failed, failed.Status);
        var retried = await viewer.RetryAsync(failed);
        Assert.Equal(DocumentStatus.Ready, retried.Status);
        Assert.Single(await viewer.GetDocumentsAsync());
        Assert.Equal(14, retried.PageCount);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task AnHttpTimeoutFailsTheTabRetryably()
    {
        var requests = 0;
        var body = Fixture.Data("report");
        await using var server = Loopback.Start(async context =>
        {
            // The first request stalls past the client's timeout; the retry is answered.
            if (Interlocked.Increment(ref requests) == 1) await Task.Delay(TimeSpan.FromSeconds(5));
            await Loopback.Reply(context, 200, body);
        });
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(300) };
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { DocumentFetcher = new HttpClientDocumentFetcher(client) });
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(server.Url("slow.pdf")));
        Assert.NotEqual(PdfErrorCode.OperationCancelled, error.Code);
        var failed = (await viewer.GetDocumentsAsync()).Single();
        Assert.Equal(DocumentStatus.Failed, failed.Status);
        var retried = await viewer.RetryAsync(failed);
        Assert.Equal((DocumentStatus.Ready, 14), (retried.Status, retried.PageCount));
    }
}
