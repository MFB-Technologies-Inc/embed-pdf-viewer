using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The port of StampTests.swift, with the stamp cases of PluginSettingsTests.swift and SignatureTransportTests.swift.</summary>
public sealed class StampTests
{
    static readonly CancellationToken Cancelled = new(true);

    internal static async Task<(PdfViewer Viewer, StampCapability Stamps)> Started(StampConfig? config = null)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Stamp = config ?? new StampConfig() });
        return (viewer, await viewer.GetStampsAsync());
    }

    /// <summary>A viewer with the shipped English library imported, and its first asset.</summary>
    static async Task<(PdfViewer Viewer, StampCapability Stamps, string Library, StampAsset Asset)> Imported()
    {
        var (viewer, stamps) = await Started();
        var library = await stamps.ImportLibraryAsync(Fixture.Data("stamps"));
        var asset = (await stamps.GetAssetsAsync(new StampAssetFilter { LibraryId = library }))[0];
        return (viewer, stamps, library, asset);
    }

    static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), "embedpdf-stamps-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ALibraryImportsWithPreviews()
    {
        var (viewer, stamps, library, asset) = await Imported();
        await using var _v = viewer;
        Assert.Equal(asset.Id, (await stamps.GetLibraryAsync(library))?.AssetIds[0]);
        Assert.Equal(StampAssetKind.Stamp, asset.Kind);
        var preview = await stamps.GetAssetPreviewAsync(asset.Id);
        Assert.NotNull(preview);
        Assert.Equal("image/png", preview.MimeType);
        // A PNG signature, where Swift decodes the image.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, preview.Data!.Value[..4].ToArray());
    }

    [Fact]
    public async Task APlainPdfImportsOneAssetPerPage()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        var library = await stamps.ImportLibraryAsync(Fixture.Data("colors"));
        await using var other = await PdfViewer.CreateAsync();
        var pages = await other.GetPagesAsync(await other.OpenAsync(Fixture.Data("colors")));
        Assert.Equal(pages.Count, (await stamps.GetAssetsAsync(new StampAssetFilter { LibraryId = library })).Count);
    }

    [Fact]
    public async Task ImportingAPngThrows()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var error = await Assert.ThrowsAsync<PdfException>(() => stamps.ImportLibraryAsync(png));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Empty(await stamps.GetLibrariesAsync());
    }

    [Fact]
    public async Task APlacedStampIsAnAnnotation()
    {
        var (viewer, stamps, _, asset) = await Imported();
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var placed = await stamps.PlaceAsync(asset.Id, document, page, new PdfPoint(100, 100));
        using var annotations = await viewer.GetAnnotationAsync(document);
        Assert.NotNull(await annotations.GetAsync(placed));
    }

    [Fact]
    public async Task PlacingIntoAClosedDocumentThrows()
    {
        var (viewer, stamps, _, asset) = await Imported();
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        await stamps.ArmAsync(asset.Id, document);
        Assert.Equal(asset.Id, (await stamps.GetArmedAssetAsync(document))?.Id);
        await viewer.CloseAsync(document);
        // A closed document grants nothing: the placement's permission check refuses it first.
        var error = await Assert.ThrowsAsync<PdfException>(() => stamps.PlaceAsync(asset.Id, document, page, new PdfPoint(10, 10)));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        Assert.Null(await stamps.GetArmedAssetAsync(document));
    }

    [Fact]
    public async Task ArmingIsObservedAndDisarmingClearsIt()
    {
        var (viewer, stamps, _, asset) = await Imported();
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var events = stamps.ObserveEvents();
        await stamps.ArmAsync(asset.Id, document);
        var armed = await Fixture.First(events, e => e as StampEvent.ArmChanged);
        Assert.Equal(document.Id, armed.DocumentId);
        Assert.Equal(asset.Id, armed.Asset?.Id);
        Assert.Equal(asset.Id, (await stamps.GetArmedAssetAsync(document))?.Id);
        await stamps.DisarmAsync(document);
        Assert.Null(await stamps.GetArmedAssetAsync(document));
    }

    [Fact]
    public async Task CreateAssetFromAnnotationsExportsThroughTheNativeEngine()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var error = await Assert.ThrowsAsync<PdfException>(() => stamps.CreateAssetFromAnnotationsAsync([], document, page));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        using var annotations = await viewer.GetAnnotationAsync(document);
        var square = await annotations.CreateAsync("""{"subtype": "square", "box": {"x": 72, "y": 72, "width": 40, "height": 40}}""", page);
        var asset = await stamps.CreateAssetFromAnnotationsAsync([square], document, page);
        var bytes = await stamps.GetAssetBytesAsync(asset);
        Assert.Equal("%PDF"u8.ToArray(), bytes!.Value[..4].ToArray());
    }

    [Fact]
    public async Task ALibraryPersistsThroughAFileStore()
    {
        var directory = TemporaryDirectory();
        try
        {
            var store = new FileStampLibraryStore(directory);
            var (viewer, stamps) = await Started();
            await using (viewer)
            {
                var persistence = await stamps.PersistAsync(store);
                await stamps.ImportLibraryAsync(Fixture.Data("stamps"));
                await Task.Delay(200);
                persistence.Dispose();
            }
            var (again, restoring) = await Started();
            await using var _a = again;
            var restored = await restoring.RestoreAsync(store);
            Assert.Single(restored);
            Assert.NotEmpty(await restoring.GetAssetsAsync(new StampAssetFilter { LibraryId = restored[0] }));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ADroppedPersistRegistrationKeepsWriting()
    {
        var directory = TemporaryDirectory();
        try
        {
            var store = new FileStampLibraryStore(directory);
            var (viewer, stamps) = await Started();
            await using var _v = viewer;
            _ = await stamps.PersistAsync(store);
            await stamps.ImportLibraryAsync(Fixture.Data("stamps"));
            IReadOnlyList<StoredStampLibrary> written = [];
            for (var i = 0; i < 50 && written.Count == 0; i++)
            {
                await Task.Delay(20);
                written = store.List();
            }
            Assert.Single(written);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ADisposedViewersPersistThrows()
    {
        var directory = TemporaryDirectory();
        try
        {
            var store = new FileStampLibraryStore(directory);
            var (viewer, stamps) = await Started();
            await viewer.DisposeAsync();
            var error = await Assert.ThrowsAsync<PdfException>(() => stamps.PersistAsync(store));
            Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AStoreThatThrowsFailsTheRestore()
    {
        var file = TemporaryDirectory();
        try
        {
            var store = new FileStampLibraryStore(file);
            // A file where the store expects its directory.
            Directory.Delete(file);
            await File.WriteAllBytesAsync(file, []);
            var (viewer, stamps) = await Started();
            await using var _v = viewer;
            var error = await Assert.ThrowsAsync<PdfException>(() => stamps.RestoreAsync(store));
            Assert.Equal("HostError", error.Name);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    sealed class DisposingStore(PdfViewer viewer) : IStampLibraryStore
    {
        public Exception? Thrown;

        public IReadOnlyList<StoredStampLibrary> List()
        {
            try { viewer.Dispose(); }
            catch (Exception error) { Thrown = error; }
            return [];
        }

        public void Put(string id, ReadOnlyMemory<byte> bytes) { }

        public void Delete(string id) { }
    }

    [Fact]
    public async Task DisposingFromAStoreThrowsIntoIt()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        var store = new DisposingStore(viewer);
        Assert.Empty(await stamps.RestoreAsync(store));
        Assert.IsType<InvalidOperationException>(store.Thrown);
        Assert.Empty(await stamps.GetLibrariesAsync());
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in stamps.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await stamps.ImportLibraryAsync(Fixture.Data("stamps"));
    }

    [Fact]
    public async Task DisposingTheViewerFinishesTheEvents()
    {
        var (viewer, stamps) = await Started();
        var reading = Task.Run(async () => { await foreach (var _ in stamps.ObserveEvents()) { } });
        await Task.Delay(50);
        await viewer.DisposeAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ADisposedViewersCapabilityThrows()
    {
        var (viewer, stamps) = await Started();
        await viewer.DisposeAsync();
        var error = await Assert.ThrowsAsync<PdfException>(() => stamps.GetLibrariesAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task EveryCancellableMemberCancelsBeforeItStarts()
    {
        var (viewer, stamps, library, asset) = await Imported();
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var other = await stamps.CreateLibraryAsync("Other");
        var directory = TemporaryDirectory();
        try
        {
            var store = new FileStampLibraryStore(directory);
            var colors = Fixture.Data("colors");
            Func<Task>[] calls =
            [
                () => stamps.ImportLibraryAsync(Fixture.Data("stamps"), null, Cancelled),
                () => stamps.CreateLibraryAsync("Mine", null, Cancelled),
                () => stamps.UpdateLibraryAsync(library, new StampLibraryPatch { Name = "Renamed" }, Cancelled),
                () => stamps.DeleteLibraryAsync(library, Cancelled),
                () => stamps.ExportLibraryAsync(library, Cancelled),
                () => stamps.RenderAssetPreviewAsync(asset.Id, 64, Cancelled),
                () => stamps.CreateAssetAsync(new StampAddAssetInput { LibraryId = library, Source = colors }, Cancelled),
                () => stamps.CreateAssetFromAnnotationsAsync([], document, page, null, Cancelled),
                () => stamps.UpdateAssetAsync(asset.Id, new StampAssetPatch { Label = "Renamed" }, Cancelled),
                () => stamps.DeleteAssetAsync(asset.Id, Cancelled),
                () => stamps.MoveAssetAsync(asset.Id, other, Cancelled),
                () => stamps.DuplicateAssetAsync(asset.Id, null, Cancelled),
                () => stamps.ArmAsync(asset.Id, document, null, Cancelled),
                () => stamps.PlaceAsync(asset.Id, document, page, new PdfPoint(10, 10), null, Cancelled),
                () => stamps.PlaceOnPagesAsync(asset.Id, document, null, new PdfPoint(10, 10), null, Cancelled),
                () => stamps.RestoreAsync(store, Cancelled),
            ];
            foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
            Assert.Equal(asset, await stamps.GetAssetAsync(asset.Id));
            Assert.Equal(2, (await stamps.GetLibrariesAsync()).Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportOptionsIdNamesTheLibrary()
    {
        var (viewer, stamps) = await Started();
        await using var _v = viewer;
        Assert.Equal("csharp-library", await stamps.ImportLibraryAsync(Fixture.Data("stamps"), new StampImportOptions { Id = "csharp-library" }));
    }

    [Fact]
    public async Task ResetRestoresTheRegisteredPreviewWidth()
    {
        var (viewer, stamps) = await Started(new StampConfig { PreviewWidth = 320, Dynamic = true });
        await using var _v = viewer;
        await stamps.UpdateSettingsAsync(new StampConfig { Dynamic = false });
        Assert.Equal(320, (await stamps.GetSettingsAsync()).PreviewWidth);
        Assert.False((await stamps.GetSettingsAsync()).Dynamic);
        await stamps.ResetSettingsAsync();
        Assert.True((await stamps.GetSettingsAsync()).Dynamic);
    }

    [Fact]
    public async Task AssetsAndLibrariesEditMoveAndExport()
    {
        var (viewer, stamps, library, asset) = await Imported();
        await using var _v = viewer;
        Assert.True(await stamps.CanImportAsync());
        Assert.Single(await stamps.GetLibrariesAsync(["stamps"]));
        await stamps.UpdateAssetAsync(asset.Id, new StampAssetPatch { Label = "Renamed", Categories = ["mine"] });
        var renamed = await stamps.GetAssetAsync(asset.Id);
        Assert.Equal("Renamed", renamed?.Label);
        var copy = await stamps.DuplicateAssetAsync(asset.Id, "Copy");
        Assert.Equal("Copy", (await stamps.GetAssetAsync(copy))?.Label);
        var other = await stamps.CreateLibraryAsync("Other");
        var moved = await stamps.MoveAssetAsync(copy, other);
        Assert.Equal(other, (await stamps.GetAssetAsync(moved))?.LibraryId);
        var exported = await stamps.ExportLibraryAsync(other);
        Assert.Equal("%PDF"u8.ToArray(), exported[..4].ToArray());
        var preview = await stamps.RenderAssetPreviewAsync(asset.Id, 64);
        Assert.NotNull(preview?.Data);
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        Assert.True(await stamps.CanPlaceAsync(document));
        Assert.True(await stamps.CanCreateFromAnnotationsAsync(document));
        var batch = await stamps.PlaceOnPagesAsync(asset.Id, document, null, new PdfPoint(100, 100), new StampPlacementOptions { TargetWidth = 80, Rotation = 0 });
        Assert.Equal((await viewer.GetPagesAsync(document)).Count, batch.Applied.Count);
        await stamps.UpdateLibraryAsync(library, new StampLibraryPatch { Name = "Renamed library" });
        Assert.Equal("Renamed library", (await stamps.GetLibraryAsync(library))?.Name);
        await stamps.DeleteAssetAsync(asset.Id);
        Assert.Null(await stamps.GetAssetAsync(asset.Id));
        await stamps.DeleteLibraryAsync(library);
        Assert.Null(await stamps.GetLibraryAsync(library));
    }
}
