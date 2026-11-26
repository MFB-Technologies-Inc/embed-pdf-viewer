namespace EmbedPDF.Models.Tests;

/// <summary>
/// Cases for the history, outline and attachment models that Swift covers
/// only through <c>PluginIntegrationTests.swift</c>.
/// </summary>
public class PanelModelTests
{
    [Fact]
    public Task AnUndoFailureSetsTheErrorAndAnUndoClearsIt() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.History());
        var document = await viewer.OpenAsync(Fixture.Data());
        using var history = await viewer.GetHistoryAsync(document);
        var model = context.Watch(new HistoryModel(viewer, history));
        var label = new HistoryChangeLabel("history.change", new Dictionary<string, HistoryLabelValue>());
        await model.ReceiveAsync(new HistoryEvent.UndoFailed(label, false, HistoryUndoFailure.Refused,
            new PluginErrorInfo(PdfErrorCode.Conflict, "changed elsewhere", "history", null)));
        Assert.Equal(PdfErrorCode.Conflict, model.Error?.Code);
        Assert.Equal("changed elsewhere", model.Error?.Detail);
        await model.ReceiveAsync(new HistoryEvent.Undone(label, false, 2));
        Assert.Null(model.Error);
        Assert.Equal("Some changes could not be undone.", model.Notice);
        await model.ReceiveAsync(new HistoryEvent.Undone(label, true, 0));
        Assert.Null(model.Notice);
        Assert.Equal("Undo", model.UndoTitle);
    });

    [Fact]
    public Task AnAttachmentActionWhileAnotherIsOutIsIgnored() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Attachment());
        var document = await viewer.OpenAsync(Fixture.Data());
        using var attachments = await viewer.GetAttachmentsAsync(document);
        var model = context.Watch(new AttachmentModel(viewer, attachments));
        await attachments.EnsureLoadedAsync();
        var first = model.AddAsync([new AttachmentFileSource(new byte[] { 1 }) { Name = "one.bin" }]);
        Assert.True(model.Performing);
        var second = model.AddAsync([new AttachmentFileSource(new byte[] { 2 }) { Name = "two.bin" }]);
        await Task.WhenAll(first, second);
        Assert.False(model.Performing);
        Assert.Equal(["one.bin"], model.Files.Select(file => file.Name));
    });

    [Fact]
    public Task ImportingAFileFromDiskAttachesIt() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Attachment());
        var document = await viewer.OpenAsync(Fixture.Data());
        using var attachments = await viewer.GetAttachmentsAsync(document);
        var model = context.Watch(new AttachmentModel(viewer, attachments));
        await attachments.EnsureLoadedAsync();
        var directory = Directory.CreateTempSubdirectory("epdf-models-");
        try
        {
            var path = Path.Combine(directory.FullName, "notes.txt");
            await File.WriteAllTextAsync(path, "hello");
            await model.ImportFilesAsync([path]);
            Assert.Null(model.Error);
            var file = Assert.Single(model.Files);
            Assert.Equal("notes.txt", file.Name);
            Assert.Equal("text/plain", file.MimeType);
            await model.ImportFilesAsync([Path.Combine(directory.FullName, "missing.bin")]);
            Assert.NotNull(model.Error);
            Assert.Single(model.Files);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    });

    [Fact]
    public Task AFileOperationsCancellationIsNotAnError() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Attachment());
        var document = await viewer.OpenAsync(Fixture.Data());
        using var attachments = await viewer.GetAttachmentsAsync(document);
        var model = context.Watch(new AttachmentModel(viewer, attachments));
        model.FileOperationFailed(new OperationCanceledException());
        Assert.Null(model.Error);
        model.FileOperationFailed(new IOException("disk full"));
        Assert.Equal(PdfErrorCode.OperationFailed, model.Error?.Code);
    });

    [Fact]
    public Task ActivatingAnOutlineItemRecordsTheActivation() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Outline().Stage(new StageConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        using var outline = await viewer.GetOutlineAsync(document);
        var model = context.Watch(new OutlineModel(viewer, outline));
        await outline.EnsureLoadedAsync();
        var created = await model.CreateAsync(new OutlineItemInput { Title = "Here" });
        Assert.NotNull(created);
        Assert.Equal([created!.ItemRef], model.SelectedItems);
        var activation = await model.ActivateAsync(created.ItemRef);
        Assert.Null(model.Error);
        Assert.Same(activation, model.LastActivation);
    });
}
