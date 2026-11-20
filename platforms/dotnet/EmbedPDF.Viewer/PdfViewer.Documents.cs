using System.Diagnostics.CodeAnalysis;
using GH = uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>How a document opens.</summary>
public sealed record OpenDocumentOptions
{
    /// <summary>The tab's display name.</summary>
    public string? Name { get; init; }

    /// <summary>The document password, if it has one.</summary>
    public string? Password { get; init; }

    /// <summary>Whether the new tab becomes the active document. Defaults to <see langword="true"/>.</summary>
    public bool Activate { get; init; } = true;

    internal GH.OpenDocumentOptions ToFfi() => new(Name, Password, Activate);
}

// The open overloads share an optional options record and token on purpose:
// a new open setting is a new options property, not a new parameter, so the
// optional-parameter compatibility rule (RS0026) has nothing to protect here.
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Open settings grow in OpenDocumentOptions.")]
public sealed partial class PdfViewer
{
    static GH.OpenDocumentOptions Options(OpenDocumentOptions? options) => (options ?? new OpenDocumentOptions()).ToFfi();

    /// <summary>
    /// Opens PDF bytes. Bytes that are not a PDF fail the open
    /// (<c>operation-failed</c>, "failed to open base document") and leave a
    /// <see cref="DocumentStatus.Failed"/> tab. A wrong password
    /// does not fail it: the document opens <see cref="DocumentStatus.Locked"/>,
    /// and its <see cref="DocumentEvent.Locked"/> event says a password was given.
    /// </summary>
    /// <param name="pdf">The document.</param>
    /// <param name="options">The tab's name, the password and whether it activates.</param>
    /// <param name="cancellationToken">Checked before the open is posted; the open itself runs to completion.</param>
    public Task<DocumentInfo> OpenAsync(ReadOnlyMemory<byte> pdf, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default) =>
        Telemetry.Measured("open", async () => Ffi.ToPublic(await CallAsync(
            h => h.OpenDocument(pdf.ToArray(), Options(options)), cancellationToken).ConfigureAwait(false)));

    /// <summary>
    /// Reads <paramref name="pdf"/> to its end and opens the bytes, as
    /// <see cref="OpenAsync(ReadOnlyMemory{byte}, OpenDocumentOptions?, CancellationToken)"/>
    /// does. The stream stays open. A disposed viewer throws before reading.
    /// What the stream throws while it is read (an <see cref="IOException"/>,
    /// <see cref="NotSupportedException"/>, <see cref="ObjectDisposedException"/>)
    /// escapes unwrapped: it is the caller's stream, not the engine, that failed.
    /// </summary>
    public async Task<DocumentInfo> OpenAsync(Stream pdf, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ThrowIfClosed();
        using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return await OpenAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the file at <paramref name="path"/> and opens it. The name
    /// defaults to the file's name. A disposed viewer throws before reading.
    /// A file that can't be read throws the reader's own exception
    /// (<see cref="FileNotFoundException"/>, <see cref="DirectoryNotFoundException"/>,
    /// <see cref="UnauthorizedAccessException"/>, <see cref="IOException"/>),
    /// unwrapped, as <see cref="File.ReadAllBytesAsync(string, CancellationToken)"/> does.
    /// </summary>
    public async Task<DocumentInfo> OpenFileAsync(string path, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfClosed();
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        options ??= new OpenDocumentOptions();
        return await OpenAsync(bytes, options with { Name = options.Name ?? Path.GetFileName(path) }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a URL through the kernel's loading tab and the options'
    /// <see cref="IDocumentFetcher"/>. Cancelling the token closes the tab and
    /// cancels the fetch, then throws <see cref="OperationCanceledException"/>.
    /// HTTP failures and non-PDF responses leave a failed tab that
    /// <see cref="RetryAsync"/> can fetch again.
    /// </summary>
    public Task<DocumentInfo> OpenAsync(Uri url, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Telemetry.Measured("open", async () => Ffi.ToPublic(await CallCancellableAsync(
            (h, token) => h.OpenUrl(url.AbsoluteUri, Options(options), token), cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>
    /// Restores a <see cref="SaveLayerAsync"/> artifact over its original PDF.
    /// The engine validates both. Cancelling the token closes its loading tab.
    /// </summary>
    public Task<DocumentInfo> OpenLayerAsync(ReadOnlyMemory<byte> basePdf, ReadOnlyMemory<byte> layer, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default) =>
        Telemetry.Measured("open", async () => Ffi.ToPublic(await CallCancellableAsync(
            (h, token) => h.OpenLayerDocument(basePdf.ToArray(), layer.ToArray(), Options(options), token), cancellationToken).ConfigureAwait(false)));

    /// <summary>
    /// Retries a failed source in its tab position without activating it. Use
    /// the returned document: the engine may replace the pending tab's id.
    /// Close its loading tab to cancel; the token does not cancel a retry once posted.
    /// </summary>
    public async Task<DocumentInfo> RetryAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Ffi.ToPublic(await CallAsync(h => h.RetryDocument(document.Id), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Renames a document's tab.</summary>
    public Task RenameAsync(DocumentInfo document, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        return CallAsync(h => h.RenameDocument(document.Id, name), cancellationToken);
    }

    /// <summary>Closes a document.</summary>
    public Task CloseAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallAsync(h => h.CloseDocument(document.Id), cancellationToken);
    }

    /// <summary>Closes every document.</summary>
    public Task CloseAllAsync(CancellationToken cancellationToken = default) => CallAsync(h => h.CloseAllDocuments(), cancellationToken);

    /// <summary>
    /// Unlocks a document that opened <see cref="DocumentStatus.Locked"/>.
    /// Throws <c>permission-denied</c> for a wrong password, with the engine's
    /// own message ("incorrect document password"); the tab stays locked.
    /// </summary>
    public Task UnlockAsync(DocumentInfo document, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(password);
        return CallAsync(h => h.UnlockDocument(document.Id, password), cancellationToken);
    }

    /// <summary>A document by id, including loading, failed and locked tabs; <see langword="null"/> selects the active tab.</summary>
    public async Task<DocumentInfo?> GetDocumentAsync(string? id = null, CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await CallAsync(h => h.Document(id), cancellationToken).ConfigureAwait(false));

    /// <summary>Whether the viewer has a document with this id.</summary>
    public Task<bool> HasDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return CallAsync(h => h.HasDocument(id), cancellationToken);
    }

    /// <summary>How many documents the viewer has, tabs of every status included.</summary>
    public async Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default) =>
        WireInteger.ToInt(await CallAsync(h => h.DocumentCount(), cancellationToken).ConfigureAwait(false), "document count");

    /// <summary>Every document, in tab order.</summary>
    public async Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken = default) =>
        (await CallAsync(h => h.Documents(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic).ToArray();

    /// <summary>The active document, or <see langword="null"/>.</summary>
    public async Task<DocumentInfo?> GetActiveDocumentAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await CallAsync(h => h.ActiveDocument(), cancellationToken).ConfigureAwait(false));

    /// <summary>The active document's id, or <see langword="null"/>.</summary>
    public Task<string?> GetActiveDocumentIdAsync(CancellationToken cancellationToken = default) =>
        CallAsync(h => h.ActiveDocumentId(), cancellationToken);

    /// <summary>Makes <paramref name="document"/> the active one.</summary>
    public Task SetActiveAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallAsync(h => h.SetActiveDocument(document.Id), cancellationToken);
    }

    /// <summary>The document ids in tab order.</summary>
    public async Task<IReadOnlyList<string>> GetDocumentOrderAsync(CancellationToken cancellationToken = default) =>
        await CallAsync(h => h.DocumentOrder(), cancellationToken).ConfigureAwait(false);

    /// <summary>Sets the tab order. It must be a permutation of the current order, or throws <c>invalid-input</c>.</summary>
    public Task SetDocumentOrderAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var order = ids.ToArray();
        return CallAsync(h => h.SetDocumentOrder(order), cancellationToken);
    }

    /// <summary>Moves a document to <paramref name="index"/>, clamped into the order.</summary>
    public Task MoveAsync(DocumentInfo document, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallAsync(h => h.MoveDocument(document.Id, WireInteger.ToInt64(index)), cancellationToken);
    }

    /// <summary>Swaps two documents' tab positions.</summary>
    public Task SwapAsync(DocumentInfo first, DocumentInfo second, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return CallAsync(h => h.SwapDocuments(first.Id, second.Id), cancellationToken);
    }

    /// <summary>A document's pages. Throws <c>not-found</c> for a document the viewer doesn't have.</summary>
    public Task<IReadOnlyList<PageInfo>> GetPagesAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Telemetry.Measured("pages", async () => (IReadOnlyList<PageInfo>)(await CallAsync(h => h.Pages(document.Id), cancellationToken).ConfigureAwait(false)) .Select(Ffi.ToPublic).ToArray());
    }

    /// <summary>A page by its reference, in <paramref name="document"/> or the active document. A missing or closed reference is <see langword="null"/>.</summary>
    public async Task<PageInfo?> GetPageAsync(PageRef page, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        var reference = Ffi.ToFfi(page);
        return Ffi.ToPublicOrNull(await CallAsync(h => h.Page(reference, document?.Id), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The page at <paramref name="index"/>; <see langword="null"/> below zero or past the last page.</summary>
    public async Task<PageInfo?> GetPageAtAsync(int index, DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await CallAsync(h => h.PageAt(WireInteger.ToInt64(index), document?.Id), cancellationToken).ConfigureAwait(false));

    /// <summary>A page's index, or -1 for a missing page or document.</summary>
    public async Task<int> GetPageIndexAsync(PageRef page, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        var reference = Ffi.ToFfi(page);
        return WireInteger.ToInt(await CallAsync(h => h.PageIndex(reference, document?.Id), cancellationToken).ConfigureAwait(false), "page index");
    }

    /// <summary>The page-list revision, or -1 when the document is absent.</summary>
    public Task<long> GetPageRevisionAsync(DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        CallAsync(h => h.PageRevision(document?.Id), cancellationToken);

    /// <summary>Whether the document's scope allows <paramref name="permission"/>.</summary>
    public Task<bool> AllowsAsync(DocumentPermission permission, DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        CallAsync(h => h.Allows(permission.Value, document?.Id), cancellationToken);

    /// <summary>Whether the document may be saved: <see cref="DocumentPermission.Download"/> in its scope.</summary>
    public Task<bool> CanDownloadAsync(DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        CallAsync(h => h.CanDownload(document?.Id), cancellationToken);

    /// <summary>
    /// The complete document as PDF bytes, for the app to write wherever it
    /// keeps files. <see cref="SaveMode.Incremental"/> keeps every signature
    /// valid; <see cref="SaveMode.Rewrite"/> writes a smaller file without the
    /// revision history, and a signed document refuses it with
    /// <c>permission-denied</c>. Needs <c>doc.download</c> in the scope.
    /// Cancelling the token aborts the save.
    /// </summary>
    public Task<byte[]> SaveAsync(DocumentInfo document, SaveMode mode = SaveMode.Incremental, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Telemetry.Measured("save", () => CallCancellableAsync((h, token) => h.Save(document.Id, Ffi.ToFfi(mode), token), cancellationToken));
    }

    /// <summary>
    /// An editing-layer artifact that needs its original PDF to restore
    /// (<see cref="OpenLayerAsync"/>). Use <see cref="SaveAsync"/> for a
    /// standalone PDF. Needs <c>doc.download</c>.
    /// </summary>
    public Task<byte[]> SaveLayerAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallCancellableAsync((h, token) => h.SaveLayer(document.Id, token), cancellationToken);
    }

    /// <summary>Presentation hints for the app's own chrome.</summary>
    public async Task<ViewerSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await CallAsync(h => h.ViewerSettings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Changes the settings. Omitted fields stay unchanged; observe the change through <see cref="ObserveStateRevisions"/>.</summary>
    public Task UpdateSettingsAsync(ViewerSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ffi = Ffi.ToFfi(patch);
        return CallAsync(h => h.UpdateViewerSettings(ffi), cancellationToken);
    }

    /// <summary>Restores the settings the viewer started with.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => CallAsync(h => h.ResetViewerSettings(), cancellationToken);
}
