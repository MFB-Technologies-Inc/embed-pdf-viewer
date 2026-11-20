using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>MetadataTests.swift</c>.</summary>
public sealed class MetadataTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, MetadataCapability Metadata)> Opened()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Metadata = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        return (viewer, document, await viewer.MetadataAsync(document));
    }

    /// <summary>Whether <paramref name="stream"/> ends within two seconds.</summary>
    internal static async Task<bool> Ends<T>(IAsyncEnumerable<T> stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await foreach (var _ in stream.WithCancellation(timeout.Token)) { }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [Fact]
    public async Task RefreshThenUpdateIsObserved()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        await metadata.RefreshAsync();
        Assert.Equal(ResourceStatus.Ready, await metadata.GetStatusAsync());
        var events = metadata.ObserveEvents();
        var updated = await metadata.UpdateAsync(new MetadataPatch { Title = "Renamed" });
        Assert.Equal("Renamed", updated.Title);
        var change = await Fixture.First(events, e => (e as MetadataEvent.Updated)?.Event);
        Assert.Equal(["title"], change.ChangedKeys);
        Assert.Equal("Renamed", (await metadata.GetSnapshotAsync())?.Title);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, metadata) = await Opened();
        await using var _ = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => metadata.GetSnapshotAsync());
        // PluginError, not Closed: the viewer is still running.
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task ACapabilityAfterTheViewerIsDisposedIsInstanceClosed()
    {
        var (viewer, _, metadata) = await Opened();
        await viewer.DisposeAsync();
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => metadata.GetStatusAsync())).Code);
        metadata.Dispose();
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => metadata.GetStatusAsync())).Code);
    }

    [Fact]
    public async Task ADisposedCapabilityIsInstanceClosed()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        metadata.Dispose();
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => metadata.GetStatusAsync())).Code);
        Assert.True(await Ends(metadata.ObserveEvents()));
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in metadata.ObserveEvents(cancel.Token)) { }
        });
        Assert.Equal(0, viewer.StreamCountForTests);
        await metadata.RefreshAsync();
    }

    [Fact]
    public async Task CancellingARefreshDoesNotHang()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        using var cancel = new CancellationTokenSource();
        var refresh = metadata.RefreshAsync(cancel.Token);
        cancel.Cancel();
        try
        {
            await refresh;
        }
        catch (OperationCanceledException)
        {
        }
        await metadata.RefreshAsync();
    }

    [Fact]
    public async Task UpdateCancelsWhenTheTokenIsCancelled()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        await metadata.RefreshAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => metadata.UpdateAsync(new MetadataPatch { Title = "Renamed" }, new CancellationToken(true)));
        Assert.NotEqual("Renamed", (await metadata.GetSnapshotAsync())?.Title);
    }

    [Fact]
    public async Task RefreshCancelsWhenTheTokenIsCancelled()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => metadata.RefreshAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task DisposingTheViewerFinishesTheEvents()
    {
        var (viewer, _, metadata) = await Opened();
        var events = Ends(metadata.ObserveEvents());
        await Task.Delay(50);
        await viewer.DisposeAsync();
        Assert.True(await events);
    }

    [Fact]
    public async Task ClosingTheDocumentFinishesTheEvents()
    {
        var (viewer, document, metadata) = await Opened();
        await using var _ = viewer;
        var events = Ends(metadata.ObserveEvents());
        await Task.Delay(50);
        await viewer.CloseAsync(document);
        Assert.True(await events);
        // A stream made after the close ends at once.
        Assert.True(await Ends(metadata.ObserveEvents()));
    }

    [Fact]
    public async Task CustomEntriesUpdateAndClear()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        await metadata.RefreshCustomAsync();
        Assert.Equal(ResourceStatus.Ready, await metadata.GetCustomStatusAsync());
        var after = await metadata.UpdateCustomAsync(new Dictionary<string, string?> { ["Reviewer"] = "QA" });
        Assert.Equal("QA", after["Reviewer"]);
        Assert.Equal("QA", (await metadata.GetCustomSnapshotAsync())?["Reviewer"]);
        after = await metadata.UpdateCustomAsync(new Dictionary<string, string?> { ["Reviewer"] = null });
        Assert.False(after.ContainsKey("Reviewer"));
        Assert.True(await metadata.CanUpdateAsync());
    }

    [Fact]
    public async Task APatchClearsAField()
    {
        var (viewer, _, metadata) = await Opened();
        await using var _ = viewer;
        await metadata.EnsureLoadedAsync();
        await metadata.UpdateAsync(new MetadataPatch { Author = "Someone", Trapped = DocumentMetadataTrapped.True });
        var cleared = await metadata.UpdateAsync(new MetadataPatch { Clear = [MetadataField.Author] });
        Assert.Null(cleared.Author);
        Assert.Equal(DocumentMetadataTrapped.True, cleared.Trapped);
    }
}
