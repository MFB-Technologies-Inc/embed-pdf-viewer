using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>SecurityTests.swift</c>.</summary>
public sealed class SecurityTests
{
    static readonly IReadOnlyList<string> Narrow = ["doc.open", "doc.render"];

    [Fact]
    public async Task ALockedDocumentUnlocks()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions());
        var document = await viewer.OpenAsync(Fixture.Data("encrypted-hello"));
        Assert.Equal(DocumentStatus.Locked, document.Status);
        await Assert.ThrowsAsync<PdfException>(() => viewer.UnlockAsync(document, "nope"));
        await viewer.UnlockAsync(document, "hôtel");
        var info = (await viewer.GetDocumentsAsync()).Single(d => d.Id == document.Id);
        Assert.Equal(DocumentStatus.Ready, info.Status);
        Assert.True(info.PageCount > 0);
    }

    [Fact]
    public async Task ANarrowScopeCannotSign()
    {
        // The field's ref, read where the session may read the form: refs are
        // object numbers, the same in every viewer of the file.
        var (full, _, readable) = await SignatureTests.Opened("unsigned_sigfield");
        await using var _f = full;
        var field = await SignatureTests.UnsignedField(readable);
        Assert.True(await readable.CanSignAsync(field));

        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Scope = Narrow, Interaction = true, Form = new FormConfig(), Signature = new SignatureConfig(),
        });
        viewer.SetSigner(TestKeys.Signer());
        var document = await viewer.OpenAsync(Fixture.Data("unsigned_sigfield"));
        using var signatures = await viewer.GetSignaturesAsync(document);
        Assert.False(await signatures.CanSignAsync(field));
        Assert.False(await signatures.CanFillAsync(field));
    }

    [Fact]
    public async Task ANarrowScopeCannotSave()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Scope = Narrow });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.SaveAsync(document));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
    }

    [Fact]
    public async Task ANarrowScopeCannotEditMetadata()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Scope = Narrow, Metadata = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var metadata = await viewer.GetMetadataAsync(document);
        Assert.False(await metadata.CanUpdateAsync());
    }
}
