using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>AttachmentTests.swift</c>.</summary>
public sealed class AttachmentTests
{
    [Fact]
    public async Task BatchImportDownloadsAndReopensWithDistinctUnicodeNames()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Attachment = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var attachments = await viewer.GetAttachmentsAsync(document);
        await attachments.EnsureLoadedAsync();
        Assert.Equal(ResourceStatus.Ready, await attachments.GetStatusAsync());
        Assert.True(await attachments.CanAddAsync());
        Assert.True(await attachments.CanDownloadAsync());
        byte[] data = [0, 255, 128, 13, 10];
        var events = attachments.ObserveEvents();
        var added = await attachments.AddAsync(
        [
            new AttachmentFileSource(data) { Name = "Résumé.bin" },
            new AttachmentFileSource(Array.Empty<byte>()) { Name = "Résumé.bin" },
        ]);
        Assert.Equal(["Résumé.bin", "Résumé (2).bin"], added.Select(f => f.Name));
        Assert.NotEqual(added[0].Id, added[1].Id);
        Assert.IsType<AttachmentHome.Document>(added[0].Home);
        var changed = await Fixture.First(events, e => e as AttachmentEvent.Changed);
        Assert.Equal(DocumentEventOriginKind.Local, changed.Origin.Kind);
        var content = await attachments.DownloadAsync(added[0]);
        Assert.Equal(data, content.Data);
        Assert.Null(content.MimeType);
        await attachments.SelectAsync(added);
        Assert.Equal(added.Select(f => f.Id), (await attachments.GetSelectedAsync()).Select(f => f.Id));
        Assert.Empty(await attachments.AddAsync([]));
        Assert.False(await attachments.IsPendingAsync(added[0]));
        Assert.True(await attachments.CanRemoveAsync(added[0]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attachments.DownloadAsync(added[0], new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attachments.AddAsync([new AttachmentFileSource(data) { Name = "cancelled" }], new CancellationToken(true)));
        var bytes = await viewer.SaveAsync(document);
        var reopened = await viewer.OpenAsync(bytes);
        using var saved = await viewer.GetAttachmentsAsync(reopened);
        await saved.EnsureLoadedAsync();
        var savedFile = (await saved.ListAsync()).First(f => f.Name == "Résumé.bin");
        Assert.Equal(data, (await saved.DownloadAsync(savedFile)).Data);
        await attachments.RemoveAsync(added[0]);
        Assert.Null(await attachments.GetAsync(added[0].Id));
        await attachments.RefreshAsync();
        await viewer.CloseAsync(document);
        Assert.True(await MetadataTests.Ends(attachments.ObserveEvents()));
    }

    [Fact]
    public async Task PageAttachmentDownloadsAndRevealsThroughAnnotationCapability()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Attachment = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var annotations = await viewer.GetAnnotationAsync(document);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var data = "attached"u8.ToArray();
        using var drafts = annotations.GetDrafts();
        var annotationRef = await drafts.CreateAttachmentAsync(new AnnotationAttachmentFile(data, "page.txt"), page, new PdfPoint(40, 40));
        using var attachments = await viewer.GetAttachmentsAsync(document);
        await attachments.EnsureLoadedAsync();
        var file = (await attachments.ListAsync()).First(f => f.Home is AttachmentHome.Page);
        Assert.Equal(data, (await attachments.DownloadAsync(file)).Data);
        await attachments.RevealAsync(file);
        Assert.Contains(await annotations.GetSelectedAsync(), info => info.AnnotationRef == annotationRef);
    }
}
