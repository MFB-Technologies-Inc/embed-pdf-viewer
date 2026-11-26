namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>PluginIntegrationTests.swift</c>.</summary>
public class PluginIntegrationTests
{
    sealed class IntegrationFeedback : IPlatformFeedback
    {
        public void Selection() { }
        public void Impact(FeedbackImpactWeight? weight) { }
        public void Notify(FeedbackNotifyKind kind) { }
    }

    static Task<PdfViewer> StartedAsync(string[]? scope = null) => Fixture.StartedAsync(b => b
        .Scope(scope).Metadata().Search(new SearchConfig()).Render(new RenderConfig()).Interaction()
        .Feedback(new FeedbackConfig { Provider = new IntegrationFeedback() }).Stage(new StageConfig())
        .Selection().Link().Annotation().History().Outline().Attachment().Form(new FormConfig())
        .Redaction(new RedactionConfig()).Measurement(new MeasurementConfig()).Stamp(new StampConfig())
        .PageEdit().ViewManager().Shell()
        .I18n(new I18nConfig { Locale = "en", Locales = [new LocalePack("en", "English", new Dictionary<string, string>())] })
        .Commands().Actions(new ActionsConfig { OpenSequence = OpenSequence.Off }).Signature(new SignatureConfig()));

    [Fact]
    public Task AllPluginsAndThreeModelsEditObserveSaveAndCloseTogether() => SingleThreadContext.RunWatched(async context =>
    {
        await using var viewer = await StartedAsync();
        var document = await viewer.OpenAsync(Fixture.Data());
        using var history = await viewer.GetHistoryAsync(document);
        using var outline = await viewer.GetOutlineAsync(document);
        using var attachments = await viewer.GetAttachmentsAsync(document);
        using var annotations = await viewer.GetAnnotationAsync(document);
        var historyModel = context.Watch(new HistoryModel(viewer, history));
        var outlineModel = context.Watch(new OutlineModel(viewer, outline));
        var attachmentModel = context.Watch(new AttachmentModel(viewer, attachments));
        var historyTask = historyModel.RunAsync();
        var outlineTask = outlineModel.RunAsync();
        var attachmentTask = attachmentModel.RunAsync();
        await Observed.ExpectAsync(outlineModel, () => outlineModel.Status, ResourceStatus.Ready);
        await Observed.ExpectAsync(attachmentModel, () => attachmentModel.Status, ResourceStatus.Ready);
        await Observed.ExpectAsync(historyModel, () => historyModel.CanUndo, true, () =>
            Observed.ExpectAsync(outlineModel, () => outlineModel.Rows.Any(row => row.Item.Title == "Saved bookmark"), true,
                () => outline.CreateAsync(new OutlineItemInput { Title = "Saved bookmark", ClearTarget = true })));
        byte[] bytes = [0, 255, 42];
        await Observed.ExpectAsync(attachmentModel, () => attachmentModel.Files.Count, 1,
            () => attachments.AddAsync([new AttachmentFileSource(bytes) { Name = "Saved.bin" }]));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var note = await annotations.CreateAsync(new NoteAnnotationDraft(new PdfRect(40, 40, 24, 24)) { Contents = "Saved note" }, page);
        var undoEvents = history.ObserveEvents();
        await historyModel.UndoAsync();
        await Fixture.First(Fixture.Cast(undoEvents), item => item as HistoryEvent.Undone);
        Assert.Null(await annotations.GetAsync(note));
        var redoEvents = history.ObserveEvents();
        await historyModel.RedoAsync();
        await Fixture.First(Fixture.Cast(redoEvents), item => item as HistoryEvent.Undone);
        Assert.Equal("Saved note", (await annotations.GetAsync(note))?.Contents);
        var saved = await viewer.SaveAsync(document);
        var reopened = await viewer.OpenAsync(saved);
        using var savedOutline = await viewer.GetOutlineAsync(reopened);
        using var savedFiles = await viewer.GetAttachmentsAsync(reopened);
        await savedOutline.EnsureLoadedAsync();
        await savedFiles.EnsureLoadedAsync();
        Assert.Contains(await savedOutline.ListAsync(), item => item.Title == "Saved bookmark");
        var file = (await savedFiles.ListAsync()).Single(item => item.Name == "Saved.bin");
        Assert.Equal(bytes, (await savedFiles.DownloadAsync(file)).Data);
        await viewer.CloseAsync(document);
        await Task.WhenAll(historyTask, outlineTask, attachmentTask).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(historyModel.CanUndo);
        Assert.False(outlineModel.CanEdit);
        Assert.False(attachmentModel.CanAdd);
    });

    [Fact]
    public Task LockedAndAbsentCapabilitiesRetainTypedErrors() => SingleThreadContext.Run(async () =>
    {
        await using (var viewer = await StartedAsync())
        {
            var locked = await viewer.OpenAsync(Fixture.Data("encrypted-hello"));
            Assert.Equal(DocumentStatus.Locked, locked.Status);
            Assert.Equal(PdfErrorCode.NotReady, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetHistoryAsync(locked))).Code);
            Assert.Equal(PdfErrorCode.NotReady, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetOutlineAsync(locked))).Code);
            Assert.Equal(PdfErrorCode.NotReady, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetAttachmentsAsync(locked))).Code);
        }
        await using var bare = await Fixture.StartedAsync();
        var document = await bare.OpenAsync(Fixture.Data());
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(() => bare.GetHistoryAsync(document))).Code);
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(() => bare.GetOutlineAsync(document))).Code);
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(() => bare.GetAttachmentsAsync(document))).Code);
    });

    [Fact]
    public Task FullConfigStillEnforcesScopeAndCancellation() => SingleThreadContext.Run(async () =>
    {
        await using (var viewer = await StartedAsync(["doc.open"]))
        {
            var document = await viewer.OpenAsync(Fixture.Data());
            using var outline = await viewer.GetOutlineAsync(document);
            using var attachments = await viewer.GetAttachmentsAsync(document);
            Assert.False(await outline.CanEditAsync());
            Assert.False(await attachments.CanAddAsync());
            var denied = await Assert.ThrowsAsync<PdfException>(() => outline.CreateAsync(new OutlineItemInput { Title = "Denied" }));
            Assert.Equal(PdfErrorCode.PermissionDenied, denied.Code);
        }
        await using var allowed = await StartedAsync();
        var next = await allowed.OpenAsync(Fixture.Data());
        using var files = await allowed.GetAttachmentsAsync(next);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            files.AddAsync([new AttachmentFileSource(Array.Empty<byte>()) { Name = "Cancelled" }], new CancellationToken(canceled: true)));
        await files.EnsureLoadedAsync();
        Assert.Empty(await files.ListAsync());
    });
}
