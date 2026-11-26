namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>PDFReaderModelTests.swift</c>, plus the reader-model case of <c>StagePointerForwarderTests.swift</c>.</summary>
public class PdfReaderModelTests
{
    static ReaderSession Session(PdfReaderModel model) =>
        Assert.IsType<ReaderPhase.Ready>(model.Phase).Session;

    static bool IsIdle(PdfReaderModel model) => model.Phase is ReaderPhase.Idle;

    /// <summary>Returns once an open has set Loading, which it does before its first suspension.</summary>
    static async Task UntilLoadingAsync(PdfReaderModel model)
    {
        for (var i = 0; i < 1000; i++)
        {
            if (model.Phase is ReaderPhase.Loading) return;
            await Task.Yield();
        }
        Assert.Fail($"expected Loading, got {model.Phase}");
    }

    static OpenDocumentOptions Named(string name) => new() { Name = name };

    [Fact]
    public Task OpensToReady() => SingleThreadContext.RunWatched(async context =>
    {
        var model = context.Watch(new PdfReaderModel());
        Assert.True(IsIdle(model));
        await model.OpenAsync(Fixture.Data(), Named("report.pdf"));
        var session = Session(model);
        Assert.Equal(await session.Viewer.GetPagesAsync(session.Document), session.Pages);
        Assert.NotEmpty(session.Pages);
        Assert.Equal(DocumentStatus.Ready, session.Document.Status);
        Assert.NotNull(session.Search);
        Assert.NotNull(session.Selection);
        Assert.NotNull(session.Tools);
        await model.CloseAsync();
        Assert.True(IsIdle(model));
        Assert.Null(model.Viewer);
    });

    [Fact]
    public Task OpensAFileUrl() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        await model.OpenAsync(new Uri(Fixture.Path("report")));
        Assert.NotEmpty(Session(model).Pages);
        await model.CloseAsync();
    });

    [Fact]
    public Task NonPdfBytesFail() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel());
        await model.OpenAsync("no"u8.ToArray());
        Assert.IsType<ReaderPhase.Failed>(model.Phase);
        // The failed tab the engine leaves behind is closed.
        Assert.Equal(0, await model.Viewer!.GetDocumentCountAsync());
        // A failure does not end the model: the next open works.
        await model.OpenAsync(Fixture.Data());
        Session(model);
        await model.CloseAsync();
    });

    [Fact]
    public Task AnEncryptedDocumentLocksThenUnlocks() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel());
        await model.OpenAsync(Fixture.Data("encrypted-hello"));
        var document = Assert.IsType<ReaderPhase.Locked>(model.Phase).Document;
        Assert.Equal(DocumentStatus.Locked, document.Status);
        Assert.Null(model.UnlockError);
        await model.UnlockAsync("wrong");
        Assert.IsType<ReaderPhase.Locked>(model.Phase);
        Assert.Equal(PdfErrorCode.PermissionDenied, model.UnlockError?.Code);
        await model.UnlockAsync("hôtel");
        var session = Session(model);
        Assert.Equal(document.Id, session.Document.Id);
        Assert.Equal(DocumentStatus.Ready, session.Document.Status);
        Assert.NotEmpty(session.Pages);
        Assert.Null(model.UnlockError);
        await model.CloseAsync();
    });

    [Fact]
    public Task AnEncryptedDocumentOpensWithItsPassword() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        await model.OpenAsync(Fixture.Data("encrypted-hello"), new OpenDocumentOptions { Password = "hôtel" });
        Session(model);
        await model.CloseAsync();
    });

    [Fact]
    public Task UnlockDoesNothingUnlessLocked() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        await model.UnlockAsync("hôtel");
        Assert.True(IsIdle(model));
        await model.OpenAsync(Fixture.Data());
        var before = Session(model);
        await model.UnlockAsync("hôtel");
        Assert.Equal(before.Document, Session(model).Document);
        await model.CloseAsync();
    });

    [Fact]
    public Task FeaturesPickThePlugins() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        await model.OpenAsync(Fixture.Data());
        var session = Session(model);
        Assert.Null(session.Search);
        Assert.Null(session.Selection);
        Assert.Null(session.Tools);
        var error = await Assert.ThrowsAsync<PdfException>(() => session.Viewer.GetInteractionAsync(session.Document));
        Assert.Equal(PdfErrorCode.Unsupported, error.Code);
        await model.CloseAsync();
    });

    [Fact]
    public Task EachFeatureBringsItsOwnModel() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.Search | ReaderFeatures.Links));
        await model.OpenAsync(Fixture.Data());
        var session = Session(model);
        Assert.NotNull(session.Search);
        Assert.Null(session.Selection);
        Assert.Null(session.Tools);
        using (await session.Viewer.GetLinkAsync(session.Document)) { }
        await model.CloseAsync();
    });

    [Fact]
    public Task OpeningASecondDocumentClosesTheFirst() => SingleThreadContext.RunWatched(async context =>
    {
        var model = context.Watch(new PdfReaderModel());
        await model.OpenAsync(Fixture.Data());
        var first = Session(model);
        await model.OpenAsync(Fixture.Data("features"));
        var second = Session(model);
        Assert.Same(first.Viewer, second.Viewer);
        Assert.NotEqual(first.Document.Id, second.Document.Id);
        Assert.Equal(1, await second.Viewer.GetDocumentCountAsync());
        Assert.NotSame(first.Reader, second.Reader);
        await model.CloseAsync();
    });

    [Fact]
    public Task AnOpenSupersededByASecondOpenLeavesTheSecond() => SingleThreadContext.RunWatched(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        var first = model.OpenAsync(Fixture.Data(), Named("first"));
        await UntilLoadingAsync(model);
        await model.OpenAsync(Fixture.Data("features"), Named("second"));
        await first;
        var session = Session(model);
        Assert.Equal("second", session.Document.Name);
        Assert.Equal(1, await session.Viewer.GetDocumentCountAsync());
        await model.CloseAsync();
    });

    [Fact]
    public Task AnOpenSupersededByCloseEndsIdle() => SingleThreadContext.RunWatched(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        var opening = model.OpenAsync(Fixture.Data());
        await UntilLoadingAsync(model);
        await model.CloseAsync();
        await opening;
        Assert.True(IsIdle(model));
        Assert.Null(model.Viewer);
    });

    [Fact]
    public Task OpenShowsLoadingUntilItLands() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        var opening = model.OpenAsync(Fixture.Data());
        await UntilLoadingAsync(model);
        await opening;
        Session(model);
        await model.CloseAsync();
    });

    [Fact]
    public Task ConfigureAddsAPlugin() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(configure: b => b.Metadata()));
        await model.OpenAsync(Fixture.Data(), Named("report.pdf"));
        var session = Session(model);
        using (await session.Viewer.GetMetadataAsync(session.Document)) { }
        // The features' own plugins are still there.
        Assert.NotNull(session.Search);
        await model.CloseAsync();
    });

    [Fact]
    public Task TheDefaultModelLeavesOutTheMetadataPlugin() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel());
        await model.OpenAsync(Fixture.Data(), Named("report.pdf"));
        var session = Session(model);
        var error = await Assert.ThrowsAsync<PdfException>(() => session.Viewer.GetMetadataAsync(session.Document));
        Assert.Equal(PdfErrorCode.Unsupported, error.Code);
        await model.CloseAsync();
    });

    [Fact]
    public Task ConfigureAppliesToEachViewerTheModelStarts() => SingleThreadContext.Run(async context =>
    {
        var calls = 0;
        var model = context.Watch(new PdfReaderModel(configure: b =>
        {
            calls++;
            return b.Metadata();
        }));
        await model.OpenAsync(Fixture.Data());
        await model.CloseAsync();
        await model.OpenAsync(Fixture.Data());
        var session = Session(model);
        using (await session.Viewer.GetMetadataAsync(session.Document)) { }
        Assert.Equal(2, calls);
        await model.CloseAsync();
    });

    [Fact]
    public Task TurningOffAFeaturesPluginFailsTheOpen() => SingleThreadContext.Run(async context =>
    {
        var model = context.Watch(new PdfReaderModel(configure: b => b.Selection(false)));
        await model.OpenAsync(Fixture.Data());
        var failed = Assert.IsType<ReaderPhase.Failed>(model.Phase);
        Assert.Equal(PdfErrorCode.Unsupported, failed.Error.Code);
        // Nothing is left open, and the model is not stuck.
        Assert.Equal(0, await model.Viewer!.GetDocumentCountAsync());
        await model.CloseAsync();
    });

    /// <summary>
    /// From <c>StagePointerForwarderTests.swift</c>: without the annotation
    /// plugin a tap reads the link plugin's own page mirror, which is empty
    /// until the page loads. The reader loads each page it shows, so the test
    /// loads nothing itself.
    /// </summary>
    [Fact]
    public Task AReaderWithOnlyTheLinksFeatureActivatesALinkOnATap() => SingleThreadContext.Run(async context =>
    {
        await using var reader = context.Watch(new PdfReaderModel(ReaderFeatures.Links));
        await reader.OpenAsync(Fixture.Data("features"), Named("features.pdf"));
        var session = Session(reader);
        var error = await Assert.ThrowsAsync<PdfException>(() => session.Viewer.GetAnnotationAsync(session.Document));
        Assert.Equal(PdfErrorCode.Unsupported, error.Code);
        var model = context.Watch(session.Reader);
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await model.SizeChangedAsync(390, 844, 2);
        // The link tap handler registers in RunAsync, before the tool is read.
        await Observed.ExpectAsync(model, () => model.ActiveToolId is not null, true);
        using var links = await session.Viewer.GetLinkAsync(session.Document);
        var activation = await StagePointerForwarderTests.TapAVisibleLinkAsync(model, links);
        Assert.NotNull(activation);
        Assert.Null(model.Error);
        stop.Cancel();
        await observing;
    });

    [Fact]
    public Task TheSessionFindsAPagesInfo() => SingleThreadContext.Run(async context =>
    {
        await using var model = context.Watch(new PdfReaderModel(ReaderFeatures.None));
        await model.OpenAsync(Fixture.Data());
        var session = Session(model);
        var page = session.Pages[1];
        var shown = new StageVisiblePage(page.Page, 1, 0, 0, 10, 10, 0, 1, 0, 0,
            new StagePageTransform(0, 10, 10, page.Width, page.Height, 10, 10, 1, 1, 1, 1), default);
        Assert.Equal(page, session.GetPageInfo(shown));
        Assert.Equal(page, session.GetPageInfo(shown with { PageIndex = 0 }));
        Assert.Null(session.GetPageInfo(shown with { Page = new PageRef(int.MaxValue) }));
    });
}
