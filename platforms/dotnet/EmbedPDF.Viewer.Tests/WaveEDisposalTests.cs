using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Wave D's rule for a disposed capability, applied to wave E: every call on
/// a disposed redaction, measurement, stamp or signature capability is
/// <see cref="PdfErrorCode.InstanceClosed"/>, never an <see cref="ObjectDisposedException"/>,
/// and a stream from one ends rather than throwing.
/// </summary>
public sealed class WaveEDisposalTests
{
    static async Task ExpectClosed(Func<Task> call) =>
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(call)).Code);

    static async Task EndsWithoutThrowing<T>(IAsyncEnumerable<T> events)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var _ in events.WithCancellation(timeout.Token)) { }
    }

    [Fact]
    public async Task ADisposedWaveECapabilityIsInstanceClosed()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new(), Redaction = new(), Measurement = new(), Stamp = new(), Signature = new(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var redaction = await viewer.GetRedactionAsync(document);
        var measurement = await viewer.GetMeasurementAsync(document);
        var stamps = await viewer.GetStampsAsync();
        var signatures = await viewer.GetSignaturesAsync(document);
        redaction.Dispose();
        measurement.Dispose();
        stamps.Dispose();
        signatures.Dispose();
        await ExpectClosed(() => redaction.GetPendingAsync());
        await ExpectClosed(() => redaction.MarkPageAsync(new PageRef(1)));
        await ExpectClosed(() => measurement.GetPresetsAsync());
        await ExpectClosed(() => measurement.EnsureLoadedAsync(new PageRef(1)));
        await ExpectClosed(() => stamps.GetLibrariesAsync());
        await ExpectClosed(() => stamps.CreateLibraryAsync("x"));
        await ExpectClosed(() => stamps.PersistAsync(new FileStampLibraryStore(Path.Combine(Path.GetTempPath(), "embedpdf-disposed"))));
        await ExpectClosed(() => signatures.GetSnapshotAsync());
        await ExpectClosed(() => signatures.RefreshAsync());
        await EndsWithoutThrowing(redaction.ObserveEvents());
        await EndsWithoutThrowing(measurement.ObserveEvents());
        await EndsWithoutThrowing(stamps.ObserveEvents());
        await EndsWithoutThrowing(signatures.ObserveEvents());
    }
}
