using GT = uniffi.epdf_plugin_attachment;

namespace EmbedPDF;

/// <summary>A document-level embedded file's key in the names tree.</summary>
/// <param name="Key">The key.</param>
public readonly record struct AttachmentRef(string Key);

/// <summary>Where an embedded file lives.</summary>
public abstract record AttachmentHome
{
    private protected AttachmentHome() { }

    /// <summary>In the document's embedded files.</summary>
    /// <param name="File">Its key.</param>
    public sealed record Document(AttachmentRef File) : AttachmentHome;

    /// <summary>In a file attachment annotation on a page.</summary>
    /// <param name="Annotation">The annotation.</param>
    public sealed record Page(AnnotationRef Annotation) : AttachmentHome;
}

/// <summary>An embedded file.</summary>
/// <param name="Id">Its identity in this capability.</param>
/// <param name="Home">Where it lives.</param>
/// <param name="Name">Its file name.</param>
/// <param name="MimeType">Its media type, if recorded.</param>
/// <param name="Description">Its description, if any.</param>
/// <param name="SizeBytes">Its size, if recorded.</param>
/// <param name="Md5">Its checksum, if recorded.</param>
/// <param name="CreatedAt">Its creation date, as the document holds it.</param>
/// <param name="ModifiedAt">Its modification date, as the document holds it.</param>
public sealed record AttachedFile(
    string Id, AttachmentHome Home, string Name, string? MimeType, string? Description, long? SizeBytes,
    string? Md5, string? CreatedAt, string? ModifiedAt);

/// <summary>A file to embed.</summary>
/// <param name="Data">Its bytes.</param>
public sealed record AttachmentFileSource(ReadOnlyMemory<byte> Data)
{
    /// <summary>Its file name; the engine picks one when left out and makes a taken name unique.</summary>
    public string? Name { get; init; }
    /// <summary>Its media type.</summary>
    public string? MimeType { get; init; }
    /// <summary>Its description.</summary>
    public string? Description { get; init; }
}

/// <summary>A downloaded file.</summary>
/// <param name="Data">Its bytes.</param>
/// <param name="Name">Its file name.</param>
/// <param name="MimeType">Its media type, if recorded.</param>
public sealed record AttachmentContent(byte[] Data, string Name, string? MimeType);

/// <summary>An event from <see cref="AttachmentCapability.ObserveEvents"/>.</summary>
public abstract record AttachmentEvent
{
    private protected AttachmentEvent() { }

    /// <summary>Files changed.</summary>
    /// <param name="Kind">What happened, such as <c>added</c>.</param>
    /// <param name="Files">The files.</param>
    /// <param name="Origin">Who did it.</param>
    public sealed record Changed(string Kind, IReadOnlyList<AttachedFile> Files, DocumentEventOrigin Origin) : AttachmentEvent;

    /// <summary>The files were read again.</summary>
    /// <param name="Files">Every file.</param>
    public sealed record Resynced(IReadOnlyList<AttachedFile> Files) : AttachmentEvent;
}

/// <summary>
/// Embedded files from the document and, when installed, its annotation
/// plugin. Get one with <see cref="PdfViewer.GetAttachmentsAsync"/>; the viewer
/// needs <see cref="PdfViewerOptions.Attachment"/>. After its document closes,
/// every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class AttachmentCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GT.AttachmentHandle handle;

    internal AttachmentCapability(PdfViewer viewer, GT.AttachmentHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Every file.</summary>
    public async Task<IReadOnlyList<AttachedFile>> ListAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Files(), cancellationToken).ConfigureAwait(false));

    /// <summary>One file; <see langword="null"/> when it does not exist.</summary>
    public async Task<AttachedFile?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.File(id), cancellationToken).ConfigureAwait(false) is { } file ? Ffi.ToPublic(file) : null;
    }

    /// <summary>The selected files.</summary>
    public async Task<IReadOnlyList<AttachedFile>> GetSelectedAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.SelectedFiles(), cancellationToken).ConfigureAwait(false));

    /// <summary>Replaces the selection.</summary>
    public Task SelectAsync(IEnumerable<AttachedFile> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var ffi = files.Select(Ffi.ToFfi).ToArray();
        return viewer.Run(() => handle.Select(ffi), cancellationToken);
    }

    /// <summary>Whether a change to the file is waiting for confirmation.</summary>
    public Task<bool> IsPendingAsync(AttachedFile file, CancellationToken cancellationToken = default) => OnFile(file, handle.IsPending, cancellationToken);

    /// <summary>Where the read stands.</summary>
    public async Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false));

    /// <summary>Whether this caller may download files.</summary>
    public Task<bool> CanDownloadAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanDownload(), cancellationToken);

    /// <summary>Whether this caller may add files.</summary>
    public Task<bool> CanAddAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanAdd(), cancellationToken);

    /// <summary>Whether this caller may remove the file.</summary>
    public Task<bool> CanRemoveAsync(AttachedFile file, CancellationToken cancellationToken = default) => OnFile(file, handle.CanRemove, cancellationToken);

    /// <summary>Shows the file where it lives: selects a page attachment's annotation.</summary>
    public Task RevealAsync(AttachedFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ffi = Ffi.ToFfi(file);
        return viewer.Run(() => handle.Reveal(ffi), cancellationToken);
    }

    /// <summary>Reads the files unless they are loaded or loading. Nothing reads them until this or <see cref="RefreshAsync"/> asks.</summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.EnsureLoaded(token), cancellationToken);

    /// <summary>Reads the files again from the document.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>Embeds files, in order. A name already taken gets a numbered one. Returns the new files.</summary>
    public async Task<IReadOnlyList<AttachedFile>> AddAsync(IEnumerable<AttachmentFileSource> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var ffi = sources.Select(s => new GT.AttachmentFileSource(Bytes.Array(s.Data), s.Name, s.MimeType, s.Description)).ToArray();
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Add(ffi, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Removes a file.</summary>
    public Task RemoveAsync(AttachedFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ffi = Ffi.ToFfi(file);
        return viewer.RunCancellable(token => handle.Remove(ffi, token), cancellationToken);
    }

    /// <summary>A file's bytes.</summary>
    public async Task<AttachmentContent> DownloadAsync(AttachedFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ffi = Ffi.ToFfi(file);
        var content = await viewer.RunCancellable(token => handle.Download(ffi, token), cancellationToken).ConfigureAwait(false);
        return new(content.Data, content.Name, content.MimeType);
    }

    /// <summary>
    /// Confirmed changes and resyncs, subscribed when this returns. Ends when
    /// the document closes (at once if it already has), when the viewer is
    /// disposed, or when <paramref name="cancellationToken"/> fires. Each call
    /// is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<AttachmentEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    Task<bool> OnFile(AttachedFile file, Func<GT.AttachedFile, Task<bool>> call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ffi = Ffi.ToFfi(file);
        return viewer.Run(() => call(ffi), cancellationToken);
    }

    sealed class Relay() : EventRelay<GT.AttachmentEvent, AttachmentEvent>(Ffi.ToPublic), GT.AttachmentEventListener
    {
        public void OnEvent(GT.AttachmentEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's attachment capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Attachment"/>.</summary>
    public async Task<AttachmentCapability> GetAttachmentsAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Attachments(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
