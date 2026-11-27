using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// A fetcher written against the public API alone: this file imports no
/// generated namespace, so it compiles only while <see cref="IDocumentFetcher"/>
/// and <see cref="DocumentFetchResponse"/> are public.
/// </summary>
public sealed class FetcherExportTests
{
    sealed class StubFetcher(byte[] body) : IDocumentFetcher
    {
        public Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken) =>
            url.EndsWith("/report.pdf", StringComparison.Ordinal)
                ? Task.FromResult(new DocumentFetchResponse(200, body))
                : throw new PdfException("StubFetcher", PdfErrorCode.NotFound, "no such document");
    }

    [Fact]
    public async Task AFetcherWrittenAgainstThePublicApiServesAUrlDocument()
    {
        await using var viewer = await PdfViewer.CreateAsync(
            new PdfViewerOptionsBuilder().DocumentFetcher(new StubFetcher(Fixture.Data("report"))).Build());
        var document = await viewer.OpenAsync(new Uri("https://embedpdf.test/report.pdf"));
        Assert.Equal(DocumentStatus.Ready, document.Status);
        Assert.Equal(14, document.PageCount);
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.OpenAsync(new Uri("https://embedpdf.test/missing.pdf")));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
    }
}
