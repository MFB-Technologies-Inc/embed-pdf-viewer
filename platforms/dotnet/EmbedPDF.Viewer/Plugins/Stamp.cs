using GSp = uniffi.epdf_plugin_stamp;

namespace EmbedPDF;

/// <summary>A stamp preview: the encoded image bytes and their media type.</summary>
/// <param name="Data">The encoded bytes, or <see langword="null"/> when there are none.</param>
/// <param name="MimeType">The bytes' media type, such as <c>image/png</c>.</param>
public sealed record StampPreview(ReadOnlyMemory<byte>? Data, string? MimeType);

/// <summary>Where placement puts a stamp, beyond its centre.</summary>
public sealed record StampPlacementOptions
{
    /// <summary>The stamp's width in page units; the asset's own width when <see langword="null"/>.</summary>
    public double? TargetWidth { get; init; }

    /// <summary>The stamp's rotation in degrees.</summary>
    public double? Rotation { get; init; }
}

/// <summary>
/// Where custom stamp libraries live: bytes by library id.
/// <see cref="FileStampLibraryStore"/> keeps one file per library.
/// </summary>
/// <remarks>
/// Every member runs on the viewer's kernel thread, synchronously, so keep it
/// to local disk. A throw fails <see cref="StampCapability.RestoreAsync"/>
/// with <c>HostError</c> (for <see cref="List"/>) or drops that one write
/// (for <see cref="Put"/> and <see cref="Delete"/>). Disposing the viewer
/// here throws <see cref="InvalidOperationException"/>, and no member may
/// block waiting for the viewer.
/// </remarks>
public interface IStampLibraryStore
{
    /// <summary>Every stored library.</summary>
    IReadOnlyList<StoredStampLibrary> List();

    /// <summary>Stores <paramref name="bytes"/> as library <paramref name="id"/>, replacing any.</summary>
    void Put(string id, ReadOnlyMemory<byte> bytes);

    /// <summary>Removes library <paramref name="id"/>; a missing one is no error.</summary>
    void Delete(string id);
}

/// <summary>
/// The workspace's stamp libraries. Get it with <see cref="PdfViewer.GetStampsAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Stamp"/>, plus
/// <see cref="PdfViewerOptions.Interaction"/> and <see cref="PdfViewerOptions.Annotation"/>
/// to place. One capability serves the whole viewer.
/// </summary>
/// <remarks>
/// Libraries open on their own engine thread. There an asset can come from
/// PDF bytes (a library, or <see cref="StampAddAssetInput.Source"/>), from
/// ink and text marks, or from a document's annotations
/// (<see cref="CreateAssetFromAnnotationsAsync"/>).
/// </remarks>
public sealed class StampCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GSp.StampHandle handle;

    internal StampCapability(PdfViewer viewer, GSp.StampHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public Task<StampSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.Settings().ConfigureAwait(false)), cancellationToken);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(StampConfig patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(patch.ToFfi()), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Every library, or those of the given kinds (<c>stamps</c>, <c>signatures</c>).</summary>
    public Task<IReadOnlyList<StampLibrary>> GetLibrariesAsync(IEnumerable<string>? kinds = null, CancellationToken cancellationToken = default)
    {
        var list = kinds is null ? null : Arguments.NoNullElements(kinds, nameof(kinds));
        return viewer.Run(async () => (IReadOnlyList<StampLibrary>)[.. (await handle.Libraries(list).ConfigureAwait(false)).Select(Ffi.ToPublic)], cancellationToken);
    }

    /// <summary>The library <paramref name="id"/>, or <see langword="null"/>.</summary>
    public Task<StampLibrary?> GetLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(async () => await handle.Library(id).ConfigureAwait(false) is { } library ? Ffi.ToPublic(library) : null, cancellationToken);
    }

    /// <summary>Every asset, or those <paramref name="filter"/> matches.</summary>
    public Task<IReadOnlyList<StampAsset>> GetAssetsAsync(StampAssetFilter? filter = null, CancellationToken cancellationToken = default) =>
        viewer.Run(async () => (IReadOnlyList<StampAsset>)[.. (await handle.Assets(filter is null ? null : Ffi.ToFfi(filter)).ConfigureAwait(false)).Select(Ffi.ToPublic)], cancellationToken);

    /// <summary>The asset <paramref name="id"/>, or <see langword="null"/>.</summary>
    public Task<StampAsset?> GetAssetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(async () => await handle.Asset(id).ConfigureAwait(false) is { } asset ? Ffi.ToPublic(asset) : null, cancellationToken);
    }

    /// <summary>The preview cached at import; <see langword="null"/> for an unknown asset.</summary>
    public Task<StampPreview?> GetAssetPreviewAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(async () => Preview(await handle.AssetPreview(id).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>A fresh render <paramref name="width"/> device pixels wide.</summary>
    public Task<StampPreview?> RenderAssetPreviewAsync(string id, double width, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.RunCancellable(async token => Preview(await handle.RenderAssetPreview(id, Ffi.Finite(width, "width"), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>The asset's own one-page PDF, or <see langword="null"/> for an unknown asset.</summary>
    public Task<ReadOnlyMemory<byte>?> GetAssetBytesAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(async () => await handle.ReadAssetBytes(id).ConfigureAwait(false) is { } bytes ? (ReadOnlyMemory<byte>?)bytes : null, cancellationToken);
    }

    /// <summary>The library's canonical PDF.</summary>
    public Task<ReadOnlyMemory<byte>> ExportLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.RunCancellable(async token => (ReadOnlyMemory<byte>)await handle.ExportLibrary(id, token).ConfigureAwait(false), cancellationToken);
    }

    /// <summary>Whether <paramref name="document"/> (the active document when <see langword="null"/>) can take a stamp.</summary>
    public Task<bool> CanPlaceAsync(DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanPlace(document?.Id), cancellationToken);

    /// <summary>Whether assets can be made from <paramref name="document"/>'s annotations (the active document when <see langword="null"/>).</summary>
    public Task<bool> CanCreateFromAnnotationsAsync(DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanCreateFromAnnotations(document?.Id), cancellationToken);

    /// <summary>Whether libraries may be imported.</summary>
    public Task<bool> CanImportAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanImport(), cancellationToken);

    /// <summary>Creates an empty library and returns its id.</summary>
    public Task<string> CreateLibraryAsync(string name, StampCreateLibraryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return viewer.RunCancellable(token => handle.CreateLibrary(name, Ffi.ToFfi(options ?? new StampCreateLibraryOptions()), token), cancellationToken);
    }

    /// <summary>Renames or recategorizes a library.</summary>
    public Task UpdateLibraryAsync(string id, StampLibraryPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.RunCancellable(token => handle.UpdateLibrary(id, Ffi.ToFfi(patch), token), cancellationToken);
    }

    /// <summary>
    /// Imports a library PDF and returns the library's id. A PDF without a
    /// named-pages registry imports one asset per page. Bytes that are not a
    /// PDF throw <see cref="PdfErrorCode.InvalidInput"/>.
    /// </summary>
    public Task<string> ImportLibraryAsync(ReadOnlyMemory<byte> data, StampImportOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.ImportLibrary(Bytes.Array(data), Ffi.ToFfi(options ?? new StampImportOptions()), token), cancellationToken);

    /// <summary>Deletes a library and its assets.</summary>
    public Task DeleteLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.RunCancellable(token => handle.DeleteLibrary(id, token), cancellationToken);
    }

    /// <summary>Creates an asset and returns its id.</summary>
    public Task<string> CreateAssetAsync(StampAddAssetInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return viewer.RunCancellable(token => handle.CreateAsset(Ffi.ToFfi(input), token), cancellationToken);
    }

    /// <summary>
    /// Makes one stamp of <paramref name="annotations"/> on <paramref name="page"/>
    /// of <paramref name="document"/> and returns its id. An empty list throws
    /// <see cref="PdfErrorCode.InvalidInput"/>.
    /// </summary>
    public Task<string> CreateAssetFromAnnotationsAsync(IEnumerable<AnnotationRef> annotations, DocumentInfo document, PageRef page, StampAddAssetInput? input = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(document);
        var list = Arguments.NoNullElements(annotations, nameof(annotations));
        return viewer.RunCancellable(token => handle.CreateAssetFromAnnotations(
            document.Id, Ffi.ToFfi(page), [.. list.Select(Ffi.ToFfi)], Ffi.ToFfi(input ?? new StampAddAssetInput()), token), cancellationToken);
    }

    /// <summary>Changes an asset's label, subject or categories.</summary>
    public Task UpdateAssetAsync(string id, StampAssetPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.RunCancellable(token => handle.UpdateAsset(id, Ffi.ToFfi(patch), token), cancellationToken);
    }

    /// <summary>Deletes an asset.</summary>
    public Task DeleteAssetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.RunCancellable(token => handle.DeleteAsset(id, token), cancellationToken);
    }

    /// <summary>Moves an asset to another library and returns its new id (ids carry the library).</summary>
    public Task<string> MoveAssetAsync(string id, string toLibraryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(toLibraryId);
        return viewer.RunCancellable(token => handle.MoveAsset(id, toLibraryId, token), cancellationToken);
    }

    /// <summary>Copies an asset within its library and returns the copy's id.</summary>
    public Task<string> DuplicateAssetAsync(string id, string? label = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.RunCancellable(token => handle.DuplicateAsset(id, label, token), cancellationToken);
    }

    /// <summary>The next tap in <paramref name="document"/> places the asset.</summary>
    public Task ArmAsync(string assetId, DocumentInfo document, double? targetWidth = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        ArgumentNullException.ThrowIfNull(document);
        return viewer.RunCancellable(token => handle.ArmAsset(document.Id, assetId, Ffi.Finite(targetWidth, "target width"), token), cancellationToken);
    }

    /// <summary>Places the asset with its middle on <paramref name="center"/> (page space) and returns the annotation.</summary>
    public Task<AnnotationRef> PlaceAsync(string assetId, DocumentInfo document, PageRef page, PdfPoint center, StampPlacementOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        ArgumentNullException.ThrowIfNull(document);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.PlaceAsset(
            document.Id, assetId, Ffi.ToFfi(page), Ffi.ToFfi(center), Ffi.Finite(options?.TargetWidth, "target width"), Ffi.Finite(options?.Rotation, "rotation"), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>The same placement on every page (<paramref name="pages"/> <see langword="null"/>) or those listed.</summary>
    public Task<StampBatchResult> PlaceOnPagesAsync(string assetId, DocumentInfo document, IEnumerable<PageRef>? pages, PdfPoint center, StampPlacementOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        ArgumentNullException.ThrowIfNull(document);
        var list = pages?.ToArray();
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.PlaceAssetOnPages(
            document.Id, assetId, list is null ? null : [.. list.Select(Ffi.ToFfi)], Ffi.ToFfi(center),
            Ffi.Finite(options?.TargetWidth, "target width"), Ffi.Finite(options?.Rotation, "rotation"), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Clears the armed asset of <paramref name="document"/>.</summary>
    public Task DisarmAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return viewer.Run(() => handle.Disarm(document.Id), cancellationToken);
    }

    /// <summary>The asset armed in <paramref name="document"/>, or <see langword="null"/>.</summary>
    public Task<StampAsset?> GetArmedAssetAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return viewer.Run(async () => await handle.ArmedAsset(document.Id).ConfigureAwait(false) is { } asset ? Ffi.ToPublic(asset) : null, cancellationToken);
    }

    /// <summary>Imports every library <paramref name="store"/> holds and returns their ids. One that fails to import is skipped.</summary>
    public Task<IReadOnlyList<string>> RestoreAsync(IStampLibraryStore store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var adapter = new StoreAdapter(store, viewer.Mark);
        return viewer.RunCancellable(async token => (IReadOnlyList<string>)await handle.Restore(adapter, token).ConfigureAwait(false), cancellationToken);
    }

    /// <summary>
    /// Writes each changed library to <paramref name="store"/>, except the ids
    /// in <paramref name="except"/>: one burst of changes is one save. Runs
    /// until the registration is disposed (work already asked for still
    /// finishes) or the viewer is. The registration holds
    /// <paramref name="store"/> until then.
    /// </summary>
    public Task<Registration> PersistAsync(IStampLibraryStore store, IEnumerable<string>? except = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var skipped = except is null ? [] : Arguments.NoNullElements(except, nameof(except));
        var adapter = new StoreAdapter(store, viewer.Mark);
        return viewer.Run(async () => new Registration(await handle.Persist(adapter, skipped).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>
    /// Library, asset and arming changes, subscribed when this returns. Ends
    /// when the viewer is disposed or when <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<StampEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>; streams end as they would otherwise.</summary>
    public void Dispose() => handle.Dispose();

    static StampPreview? Preview(GSp.StampAssetPreview? preview) =>
        preview is null ? null : new StampPreview(preview.Bytes is { } bytes ? bytes : null, preview.MimeType);

    sealed class Relay() : EventRelay<GSp.StampEvent, StampEvent>(Ffi.ToPublic), GSp.StampEventListener
    {
        public void OnEvent(GSp.StampEvent @event) => Deliver(@event);
    }

    /// <summary>The app's store behind the generated trait: kernel thread, marked; a throw is the kernel's host error.</summary>
    sealed class StoreAdapter(IStampLibraryStore store, KernelMark mark) : GSp.StampLibraryStore
    {
        public GSp.StoredStampLibrary[] List() => KernelCallback.RunOrFail(mark, () =>
            (store.List() ?? throw new InvalidOperationException("The stamp store listed nothing.")).Select(Ffi.ToFfi).ToArray());

        public void Put(string id, byte[] bytes) => KernelCallback.RunOrFail(mark, () => store.Put(id, bytes));

        public void Delete(string id) => KernelCallback.RunOrFail(mark, () => store.Delete(id));
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The viewer's stamp capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Stamp"/>.</summary>
    public async Task<StampCapability> GetStampsAsync(CancellationToken cancellationToken = default) =>
        new(this, await CallAsync(h => h.Stamp(), cancellationToken).ConfigureAwait(false));
}
