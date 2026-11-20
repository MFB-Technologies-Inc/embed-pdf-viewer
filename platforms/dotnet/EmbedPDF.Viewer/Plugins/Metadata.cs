using GM = uniffi.epdf_plugin_metadata;

namespace EmbedPDF;

/// <summary>The Info dictionary's <c>/Trapped</c> entry.</summary>
public enum DocumentMetadataTrapped
{
    /// <summary><c>/True</c>.</summary>
    True,
    /// <summary><c>/False</c>.</summary>
    False,
    /// <summary><c>/Unknown</c>, or no entry.</summary>
    Unknown,
}

/// <summary>A field <see cref="MetadataPatch.Clear"/> can remove.</summary>
public enum MetadataField
{
    /// <summary><c>/Title</c>.</summary>
    Title,
    /// <summary><c>/Author</c>.</summary>
    Author,
    /// <summary><c>/Subject</c>.</summary>
    Subject,
    /// <summary><c>/Keywords</c>.</summary>
    Keywords,
    /// <summary><c>/Producer</c>.</summary>
    Producer,
    /// <summary><c>/Creator</c>.</summary>
    Creator,
    /// <summary><c>/CreationDate</c>.</summary>
    CreatedAt,
    /// <summary><c>/ModDate</c>.</summary>
    ModifiedAt,
}

/// <summary>A document's Info dictionary.</summary>
/// <param name="Title"><c>/Title</c>.</param>
/// <param name="Author"><c>/Author</c>.</param>
/// <param name="Subject"><c>/Subject</c>.</param>
/// <param name="Keywords"><c>/Keywords</c>.</param>
/// <param name="Producer"><c>/Producer</c>.</param>
/// <param name="Creator"><c>/Creator</c>.</param>
/// <param name="CreatedAt"><c>/CreationDate</c>, as the document holds it.</param>
/// <param name="ModifiedAt"><c>/ModDate</c>, as the document holds it.</param>
/// <param name="Trapped"><c>/Trapped</c>.</param>
public sealed record DocumentMetadata(
    string? Title, string? Author, string? Subject, string? Keywords, string? Producer, string? Creator,
    string? CreatedAt, string? ModifiedAt, DocumentMetadataTrapped Trapped);

/// <summary>A change to the Info dictionary: set fields change, fields left <see langword="null"/> stay, fields in <see cref="Clear"/> go.</summary>
public sealed record MetadataPatch
{
    /// <summary>New <c>/Title</c>.</summary>
    public string? Title { get; init; }
    /// <summary>New <c>/Author</c>.</summary>
    public string? Author { get; init; }
    /// <summary>New <c>/Subject</c>.</summary>
    public string? Subject { get; init; }
    /// <summary>New <c>/Keywords</c>.</summary>
    public string? Keywords { get; init; }
    /// <summary>New <c>/Producer</c>.</summary>
    public string? Producer { get; init; }
    /// <summary>New <c>/Creator</c>.</summary>
    public string? Creator { get; init; }
    /// <summary>New <c>/CreationDate</c>.</summary>
    public string? CreatedAt { get; init; }
    /// <summary>New <c>/ModDate</c>.</summary>
    public string? ModifiedAt { get; init; }
    /// <summary>New <c>/Trapped</c>.</summary>
    public DocumentMetadataTrapped? Trapped { get; init; }
    /// <summary>Fields to remove.</summary>
    public IReadOnlyList<MetadataField> Clear { get; init; } = [];
}

/// <summary>A confirmed change to the Info dictionary.</summary>
/// <param name="Metadata">The fields after it.</param>
/// <param name="Previous">The fields before it, when they were loaded.</param>
/// <param name="ChangedKeys">The changed fields' keys, such as <c>title</c>.</param>
public sealed record MetadataUpdatedEvent(DocumentMetadata Metadata, DocumentMetadata? Previous, IReadOnlyList<string> ChangedKeys);

/// <summary>A confirmed change to the custom Info entries.</summary>
/// <param name="Custom">The entries after it.</param>
/// <param name="Previous">The entries before it, when they were loaded.</param>
/// <param name="ChangedKeys">The changed keys.</param>
public sealed record CustomMetadataUpdatedEvent(IReadOnlyDictionary<string, string> Custom, IReadOnlyDictionary<string, string>? Previous, IReadOnlyList<string> ChangedKeys);

/// <summary>A change from <see cref="MetadataCapability.ObserveEvents"/>.</summary>
public abstract record MetadataEvent
{
    private protected MetadataEvent() { }

    /// <summary>The standard fields changed.</summary>
    /// <param name="Event">The change.</param>
    public sealed record Updated(MetadataUpdatedEvent Event) : MetadataEvent;

    /// <summary>The standard fields were read again from the document.</summary>
    /// <param name="Metadata">The fields read.</param>
    public sealed record Resynced(DocumentMetadata Metadata) : MetadataEvent;

    /// <summary>The custom entries changed.</summary>
    /// <param name="Event">The change.</param>
    public sealed record CustomUpdated(CustomMetadataUpdatedEvent Event) : MetadataEvent;

    /// <summary>The custom entries were read again from the document.</summary>
    /// <param name="Custom">The entries read.</param>
    public sealed record CustomResynced(IReadOnlyDictionary<string, string> Custom) : MetadataEvent;
}

/// <summary>
/// A document's Info dictionary, read and edited. Get one with
/// <see cref="PdfViewer.GetMetadataAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Metadata"/>. After its document closes, every
/// call throws <see cref="PdfException"/> with <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class MetadataCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GM.MetadataHandle handle;

    internal MetadataCapability(PdfViewer viewer, GM.MetadataHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>The current metadata: <see langword="null"/> until the first read lands, or while reading is forbidden (see <see cref="GetStatusAsync"/>).</summary>
    public async Task<DocumentMetadata?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.Snapshot(), cancellationToken).ConfigureAwait(false));

    /// <summary>Where the read of the standard fields stands.</summary>
    public async Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false));

    /// <summary>Whether this caller may change the Info dictionary (<c>doc.metadata.modify</c>).</summary>
    public Task<bool> CanUpdateAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanUpdate(), cancellationToken);

    /// <summary>
    /// Reads the fields unless they are loaded or loading. Nothing reads them
    /// until this or <see cref="RefreshAsync"/> asks. Cancelling throws here;
    /// the shared read still lands.
    /// </summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.EnsureLoaded(token), cancellationToken);

    /// <summary>Changes the fields. Returns once the confirmed change has reached the snapshot and <see cref="ObserveEvents"/>.</summary>
    public async Task<DocumentMetadata> UpdateAsync(MetadataPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ffi = Ffi.ToFfi(patch);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Update(ffi, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reads the fields again from the document.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>The custom Info entries: <see langword="null"/> until they are read.</summary>
    public async Task<IReadOnlyDictionary<string, string>?> GetCustomSnapshotAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.CustomSnapshot(), cancellationToken).ConfigureAwait(false);

    /// <summary>Where the read of the custom entries stands.</summary>
    public async Task<ResourceStatus> GetCustomStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.CustomStatus(), cancellationToken).ConfigureAwait(false));

    /// <summary>Changes custom entries: a value sets its key, <see langword="null"/> removes it. Returns the entries after the change.</summary>
    public async Task<IReadOnlyDictionary<string, string>> UpdateCustomAsync(IReadOnlyDictionary<string, string?> patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ffi = new Dictionary<string, string?>(patch);
        return await viewer.RunCancellable(token => handle.UpdateCustom(ffi, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the custom entries again from the document.</summary>
    public Task RefreshCustomAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.RefreshCustom(token), cancellationToken);

    /// <summary>
    /// Confirmed changes, whoever made them, subscribed when this returns. The
    /// stream ends when the document closes (at once if it already has), when
    /// the viewer is disposed, or when <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once. A stream
    /// that is never enumerated still holds a native listener until its
    /// document closes, the viewer is disposed or
    /// <paramref name="cancellationToken"/> fires.
    /// </summary>
    public IAsyncEnumerable<MetadataEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>
    /// Releases the native handle. Calls after it throw
    /// <see cref="PdfErrorCode.InstanceClosed"/>. It does not end the
    /// streams from <see cref="ObserveEvents"/>;
    /// a stream still ends when its document closes, the viewer is disposed or
    /// its token fires.
    /// </summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GM.MetadataEvent, MetadataEvent>(Ffi.ToPublic), GM.MetadataEventListener
    {
        public void OnEvent(GM.MetadataEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's metadata capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Metadata"/>.</summary>
    public async Task<MetadataCapability> GetMetadataAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Metadata(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
