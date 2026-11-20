using System.Text;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class ViewerTests
{
    static readonly byte[] NotAPdf = Encoding.UTF8.GetBytes("not a pdf");

    static Task<PdfViewer> Started(PdfViewerOptions? options = null) => PdfViewer.CreateAsync(options);

    [Fact]
    public async Task ADocumentOpensWithItsPages()
    {
        await using var viewer = await Started();
        var document = await viewer.OpenAsync(Fixture.Data("report"), new OpenDocumentOptions { Name = "report.pdf" });
        Assert.Equal(14, document.PageCount);
        Assert.Equal("report.pdf", document.Name);
        Assert.Equal(14, (await viewer.GetPagesAsync(document)).Count);
    }

    [Fact]
    public async Task AFileAndAStreamOpenToo()
    {
        await using var viewer = await Started();
        var file = await viewer.OpenFileAsync(Fixture.Path("report"));
        Assert.Equal(("report.pdf", 14), (file.Name, file.PageCount));
        await using var stream = File.OpenRead(Fixture.Path("report"));
        var streamed = await viewer.OpenAsync(stream, new OpenDocumentOptions { Name = "streamed" });
        Assert.Equal(("streamed", 14), (streamed.Name, streamed.PageCount));
    }

    [Fact]
    public async Task ADisposedViewerReadsNoFileOrStream()
    {
        var viewer = await Started();
        await viewer.DisposeAsync();
        var missing = Path.Combine(Path.GetTempPath(), $"epdf-missing-{Guid.NewGuid():N}.pdf");
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.OpenFileAsync(missing))).Code);
        using var stream = new MemoryStream(Fixture.Data("report"));
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(stream))).Code);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task AMissingFileEscapesAsTheReadersOwnError()
    {
        await using var viewer = await Started();
        var missing = Path.Combine(Path.GetTempPath(), $"epdf-missing-{Guid.NewGuid():N}.pdf");
        await Assert.ThrowsAsync<FileNotFoundException>(() => viewer.OpenFileAsync(missing));
    }

    [Fact]
    public async Task ALockedDocumentOpensAsALockedTab()
    {
        await using var viewer = await Started();
        Assert.Equal(DocumentStatus.Locked, (await viewer.OpenAsync(Fixture.Data("encrypted-hello"))).Status);
    }

    [Fact]
    public async Task AFailedOpenListsItsTabAsFailed()
    {
        await using var viewer = await Started();
        await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(NotAPdf));
        var documents = await viewer.GetDocumentsAsync();
        Assert.Equal([DocumentStatus.Failed], documents.Select(d => d.Status));
        // The engine's words, with no code in front.
        Assert.Equal("failed to open base document", documents[0].Error?.Message);
        Assert.Equal("documents", documents[0].Error?.Capability);
    }

    [Fact]
    public async Task AFailedOpenAnnouncesTheTabsErrorWithItsCode()
    {
        await using var viewer = await Started();
        var events = viewer.ObserveDocumentEvents();
        await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(NotAPdf));
        var tab = (await viewer.GetDocumentsAsync())[0];
        var failed = await Fixture.First<DocumentEvent.OpenFailed>(events);
        Assert.Equal(tab.Id, failed.DocumentId);
        Assert.Equal(tab.Error, failed.Error);
        Assert.True(failed.Error.Code == "operation-failed");
    }

    [Fact]
    public async Task AWrongPasswordParksTheTabAndAWrongUnlockIsPermissionDenied()
    {
        await using var viewer = await Started();
        var events = viewer.ObserveDocumentEvents();
        var parked = await viewer.OpenAsync(Fixture.Data("encrypted-hello"), new OpenDocumentOptions { Password = "nope" });
        Assert.Equal(DocumentStatus.Locked, parked.Status);
        var locked = await Fixture.First<DocumentEvent.Locked>(events);
        Assert.Equal((parked.Id, true), (locked.DocumentId, locked.PasswordProvided));
        var second = await viewer.OpenAsync(Fixture.Data("encrypted-hello"));
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.UnlockAsync(second, "nope"));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        Assert.Equal("incorrect document password", error.Detail);
        Assert.Equal(DocumentStatus.Locked, (await viewer.GetDocumentsAsync()).Single(d => d.Id == second.Id).Status);
    }

    [Fact]
    public async Task PagesOfAClosedDocumentAreNotFound()
    {
        await using var viewer = await Started();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.NotFound, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetPagesAsync(document))).Code);
    }

    [Fact]
    public async Task ACapabilityOfALockedDocumentIsNotReady()
    {
        await using var viewer = await Started(new PdfViewerOptions { Metadata = true });
        var locked = await viewer.OpenAsync(Fixture.Data("encrypted-hello"));
        // Through the generated accessors until Phase 3 adds the public ones.
        var notReady = await Assert.ThrowsAsync<PdfException>(() => viewer.CallAsync(h => h.Metadata(locked.Id), default));
        Assert.Equal(PdfErrorCode.NotReady, notReady.Code);
        var unsupported = await Assert.ThrowsAsync<PdfException>(() => viewer.CallAsync(h => h.Search(locked.Id), default));
        Assert.Equal(PdfErrorCode.Unsupported, unsupported.Code);
    }

    [Fact]
    public async Task ALockedOpenSaysNoPasswordWasGiven()
    {
        await using var viewer = await Started();
        var events = viewer.ObserveDocumentEvents();
        var document = await viewer.OpenAsync(Fixture.Data("encrypted-hello"));
        var locked = await Fixture.First<DocumentEvent.Locked>(events);
        Assert.Equal((document.Id, false), (locked.DocumentId, locked.PasswordProvided));
    }

    [Fact]
    public async Task ASavedDocumentReopensInEitherMode()
    {
        await using var viewer = await Started();
        var original = Fixture.Data("report");
        var document = await viewer.OpenAsync(original);
        var incremental = await viewer.SaveAsync(document);
        Assert.Equal(original, incremental.Take(original.Length));
        var rewritten = await viewer.SaveAsync(document, SaveMode.Rewrite);
        foreach (var bytes in new[] { incremental, rewritten })
        {
            Assert.Equal(14, (await viewer.OpenAsync(bytes)).PageCount);
        }
    }

    [Fact]
    public async Task ASignedDocumentRefusesARewriteSave()
    {
        await using var viewer = await Started();
        var document = await viewer.OpenAsync(Fixture.Data("signed_two_approvals"));
        Assert.Equal(PdfErrorCode.PermissionDenied,
            (await Assert.ThrowsAsync<PdfException>(() => viewer.SaveAsync(document, SaveMode.Rewrite))).Code);
        _ = await viewer.SaveAsync(document, SaveMode.Incremental);
    }

    [Fact]
    public async Task ASaveWithACancelledTokenIsCancelledAndTheNextSaveWorks()
    {
        await using var viewer = await Started();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => viewer.SaveAsync(document, SaveMode.Rewrite, new CancellationToken(true)));
        Assert.NotEmpty(await viewer.SaveAsync(document));
    }

    [Fact]
    public async Task OpeningAndClosingAreObserved()
    {
        await using var viewer = await Started();
        var events = viewer.ObserveDocumentEvents();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        await viewer.CloseAsync(document);
        var seen = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var @event in events.WithCancellation(timeout.Token))
        {
            switch (@event)
            {
                case DocumentEvent.Opened: seen.Add("opened"); break;
                case DocumentEvent.Closed: seen.Add("closed"); break;
                default: continue;
            }
            if (seen.Count == 2) break;
        }
        Assert.Equal(["opened", "closed"], seen);
    }

    [Fact]
    public async Task DisposingFinishesTheStreams()
    {
        var viewer = await Started();
        // Subscribed here, on the test thread, before disposal begins.
        var eventStream = viewer.ObserveDocumentEvents();
        var revisionStream = viewer.ObserveStateRevisions();
        var events = Task.Run(async () => { await foreach (var _ in eventStream) { } });
        var revisions = Task.Run(async () => { await foreach (var _ in revisionStream) { } });
        await viewer.DisposeAsync();
        await Task.WhenAll(events, revisions).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ACallAfterDisposeIsClosed()
    {
        var viewer = await Started();
        await viewer.DisposeAsync();
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(Fixture.Data("report")))).Code);
    }

    [Fact]
    public async Task ReportedErrorsFinishWhenTheViewerIsDisposed()
    {
        var viewer = await Started();
        var drained = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in viewer.ObserveReportedErrors()) count++;
            return count;
        });
        await viewer.DisposeAsync();
        Assert.Equal(0, await drained.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task TheFeaturesFixtureOpens()
    {
        await using var viewer = await Started();
        Assert.Equal(3, (await viewer.OpenAsync(Fixture.Data("features"))).PageCount);
    }
}
