using System.Diagnostics.CodeAnalysis;
using GP = uniffi.epdf_plugin_page_edit;

namespace EmbedPDF;

/// <summary>Where pages go: before or after a neighbour page, or at either end. A placement names a page, never an index.</summary>
public abstract record PageEditPlacement
{
    private protected PageEditPlacement() { }

    /// <summary>After <paramref name="Page"/>.</summary>
    /// <param name="Page">The neighbour.</param>
    public sealed record After(PageRef Page) : PageEditPlacement;

    /// <summary>Before <paramref name="Page"/>.</summary>
    /// <param name="Page">The neighbour.</param>
    public sealed record Before(PageRef Page) : PageEditPlacement;

    /// <summary>First.</summary>
    public sealed record Start : PageEditPlacement;

    /// <summary>Last.</summary>
    public sealed record End : PageEditPlacement;
}

/// <summary>A relative turn for <see cref="PageEditCapability.RotateAsync"/>.</summary>
public enum PageEditTurn
{
    /// <summary>90 degrees clockwise.</summary>
    Clockwise,
    /// <summary>90 degrees counterclockwise.</summary>
    CounterClockwise,
    /// <summary>180 degrees.</summary>
    Half,
}

/// <summary>An absolute page rotation for <see cref="PageEditCapability.SetRotationAsync"/>.</summary>
public enum PageEditRotation
{
    /// <summary>0 degrees.</summary>
    None,
    /// <summary>90 degrees clockwise.</summary>
    Clockwise90,
    /// <summary>180 degrees.</summary>
    Clockwise180,
    /// <summary>270 degrees clockwise.</summary>
    Clockwise270,
}

/// <summary>Blank pages to insert.</summary>
public sealed record PageEditBlankOptions
{
    /// <summary>How many; <see langword="null"/> is 1.</summary>
    public int? Count { get; init; }
    /// <summary>Their size in points; <see langword="null"/> takes the neighbour's.</summary>
    public PdfSize? Size { get; init; }
    /// <summary>Where they go; <see langword="null"/> is the engine's default.</summary>
    public PageEditPlacement? Placement { get; init; }
}

/// <summary>Pages to insert from PDF bytes.</summary>
public sealed record PageEditBytesOptions
{
    /// <summary>Which of the source's pages, by index from 0; <see langword="null"/> is all of them.</summary>
    public IReadOnlyList<int>? PageIndexes { get; init; }
    /// <summary>Where they go; <see langword="null"/> is the engine's default.</summary>
    public PageEditPlacement? Placement { get; init; }
}

/// <summary>The page list after an edit.</summary>
/// <param name="Pages">Every page, in order.</param>
public sealed record PageEditLayout(IReadOnlyList<PageInfo> Pages);

/// <summary>An insertion's new pages and the page list after it.</summary>
/// <param name="Inserted">The new pages, in order.</param>
/// <param name="Pages">Every page, in order.</param>
public sealed record PageEditInsertion(IReadOnlyList<PageRef> Inserted, IReadOnlyList<PageInfo> Pages);

/// <summary>
/// A document's page edits: rotate, move, delete, insert, duplicate and
/// extract. Get one with <see cref="PdfViewer.GetPageEditAsync"/>; the viewer
/// needs <see cref="PdfViewerOptions.PageEdit"/>. After its document closes,
/// every call throws <see cref="PdfErrorCode.InstanceClosed"/>. Every edit
/// cancels only before it starts: once started, it lands.
/// </summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The two inserts take different sources, as in Swift.")]
public sealed class PageEditCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GP.PageEditHandle handle;

    internal PageEditCapability(PdfViewer viewer, GP.PageEditHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>The selected pages.</summary>
    public async Task<IReadOnlyList<PageRef>> GetSelectedAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.GetSelected(), cancellationToken).ConfigureAwait(false));

    /// <summary>Replaces the selection.</summary>
    public Task SelectAsync(IEnumerable<PageRef> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(pages);
            return handle.Select(ffi);
        }, cancellationToken);
    }

    /// <summary>Whether this caller may edit pages.</summary>
    public Task<bool> CanEditAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanEdit(), cancellationToken);

    /// <summary>Whether this caller may extract pages.</summary>
    public Task<bool> CanExtractAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanExtract(), cancellationToken);

    /// <summary>Turns pages by <paramref name="turn"/> from where they are.</summary>
    public Task<PageEditLayout> RotateAsync(IEnumerable<PageRef> pages, PageEditTurn turn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return Layout(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            return handle.RotateBy(ffi, Ffi.ToFfi(turn), token);
        }, cancellationToken);
    }

    /// <summary>Sets pages to an absolute rotation.</summary>
    public Task<PageEditLayout> SetRotationAsync(PageEditRotation rotation, IEnumerable<PageRef> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return Layout(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            return handle.SetRotation(ffi, Ffi.ToFfi(rotation), token);
        }, cancellationToken);
    }

    /// <summary>Moves pages to <paramref name="placement"/>; several pages keep their order as one block.</summary>
    public Task<PageEditLayout> MovePagesAsync(IEnumerable<PageRef> pages, PageEditPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(placement);
        return Layout(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            var to = Ffi.ToFfi(placement);
            return handle.MovePages(ffi, to, token);
        }, cancellationToken);
    }

    /// <summary>Deletes pages. Deleting every page throws and keeps them all.</summary>
    public Task<PageEditLayout> DeletePagesAsync(IEnumerable<PageRef> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return Layout(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            return handle.DeletePages(ffi, token);
        }, cancellationToken);
    }

    /// <summary>Inserts blank pages.</summary>
    public Task<PageEditInsertion> InsertBlankAsync(PageEditBlankOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Insertion(token =>
        {
            var ffi = Ffi.ToFfi(options ?? new PageEditBlankOptions());
            return handle.InsertBlank(ffi, token);
        }, cancellationToken);
    }

    /// <summary>Inserts pages from a PDF's bytes.</summary>
    public Task<PageEditInsertion> InsertAsync(ReadOnlyMemory<byte> pdf, PageEditBytesOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Insertion(token =>
        {
            var ffi = Ffi.ToFfi(options ?? new PageEditBytesOptions());
            var bytes = Bytes.Array(pdf);
            return handle.InsertFromBytes(bytes, ffi, token);
        }, cancellationToken);
    }

    /// <summary>Inserts pages of another open document.</summary>
    public Task<PageEditInsertion> InsertAsync(IEnumerable<PageRef> pages, DocumentInfo from, PageEditPlacement? placement = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(from);
        return Insertion(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            var at = placement is null ? null : Ffi.ToFfi(placement);
            return handle.InsertFromDocument(from.Id, ffi, at, token);
        }, cancellationToken);
    }

    /// <summary>Duplicates pages, by default right after them.</summary>
    public Task<PageEditInsertion> DuplicateAsync(IEnumerable<PageRef> pages, PageEditPlacement? placement = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return Insertion(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            var at = placement is null ? null : Ffi.ToFfi(placement);
            return handle.Duplicate(ffi, at, token);
        }, cancellationToken);
    }

    /// <summary>A new PDF of these pages; the document is unchanged.</summary>
    public Task<byte[]> ExtractAsync(IEnumerable<PageRef> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return viewer.RunCancellable(token =>
        {
            var ffi = Ffi.ToFfi(pages);
            return handle.Extract(ffi, token);
        }, cancellationToken);
    }

    /// <summary>
    /// Releases the native handle. Calls after it throw
    /// <see cref="PdfErrorCode.InstanceClosed"/>. It does not end the
    /// streams from <see cref="ObserveEvents"/>;
    /// a stream still ends when its document closes, the viewer is disposed or
    /// its token fires.
    /// </summary>
    public void Dispose() => handle.Dispose();

    async Task<PageEditLayout> Layout(Func<uniffi.epdf_ffi.CancelToken, Task<GP.PageEditLayout>> call, CancellationToken cancellationToken) =>
        new(Ffi.ToPublic((await viewer.RunCancellable(call, cancellationToken).ConfigureAwait(false)).Pages));

    async Task<PageEditInsertion> Insertion(Func<uniffi.epdf_ffi.CancelToken, Task<GP.PageEditInsertion>> call, CancellationToken cancellationToken)
    {
        var insertion = await viewer.RunCancellable(call, cancellationToken).ConfigureAwait(false);
        return new(Ffi.ToPublic(insertion.Inserted), Ffi.ToPublic(insertion.Pages));
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's page-edit capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.PageEdit"/>.</summary>
    public async Task<PageEditCapability> GetPageEditAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.PageEdit(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
