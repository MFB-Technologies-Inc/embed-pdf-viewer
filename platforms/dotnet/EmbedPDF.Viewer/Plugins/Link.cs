using System.Diagnostics.CodeAnalysis;
using GL = uniffi.epdf_plugin_link;

namespace EmbedPDF;

/// <summary>Which hover actions a link carries.</summary>
public sealed record LinkHoverFlags(bool Enter, bool Exit);

/// <summary>A link on a page.</summary>
/// <param name="Id">Its id on the page.</param>
/// <param name="Page">The page.</param>
/// <param name="Bounds">Its rectangle in page space.</param>
/// <param name="TargetJson">Its target as the engine's page-space <c>PageLinkTarget</c> JSON (<c>{"kind": "uri", "uri": ...}</c>).</param>
/// <param name="Attached">Whether it is a link annotation, rather than a URL found in the text.</param>
/// <param name="AnnotationJson">The annotation, for an attached link.</param>
/// <param name="HasAction">Whether it carries an action tree.</param>
/// <param name="Hover">Its hover actions.</param>
public sealed record LinkItem(
    string Id, PageRef Page, PdfRect Bounds, string TargetJson, bool Attached, string? AnnotationJson, bool HasAction, LinkHoverFlags? Hover);

/// <summary>What activating a target would do.</summary>
public abstract record LinkResolution
{
    private protected LinkResolution() { }

    /// <summary>Go to a destination, given as the page-space <c>PageDestination</c> JSON.</summary>
    public sealed record Destination(string DestinationJson) : LinkResolution;

    /// <summary>Open a URI; the app's to open.</summary>
    public sealed record Uri(string Value) : LinkResolution;

    /// <summary>Run a named action.</summary>
    public sealed record Named(string Name) : LinkResolution;

    /// <summary>A target the engine reports rather than follows.</summary>
    public sealed record Reported(string TargetJson) : LinkResolution;
}

/// <summary>Something the link plugin did.</summary>
public abstract record LinkEvent
{
    private protected LinkEvent() { }

    /// <summary>A target was activated.</summary>
    public sealed record Activated(string TargetJson, LinkActivation Activation) : LinkEvent;

    /// <summary>A page's links loaded.</summary>
    public sealed record Loaded(PageRef Page) : LinkEvent;
}

/// <summary>
/// A document's links: where they are, what they target, and activating
/// them. Get one with <see cref="PdfViewer.GetLinkAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Interaction"/> and <see cref="PdfViewerOptions.Link"/>.
/// Taps reach links once <see cref="RegisterTapHandlerAsync"/> has run. After
/// its document closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// The plugin never opens a URL. A <see cref="LinkActivation.Uri"/>, returned
/// or on <see cref="ObserveEvents"/>, is the app's to open. Targets cross as
/// the engine's JSON: <see cref="LinkItem.TargetJson"/> is the page-space
/// <c>PageLinkTarget</c> record, and a destination carries the
/// <c>PageDestination</c> record. A target string with an unpaired
/// surrogate is <see cref="PdfErrorCode.InvalidInput"/>, as the commands'
/// JSON is, since it would reach the engine as U+FFFD.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Activate and label take a listed link or a target, as in Swift.")]
public sealed class LinkCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GL.LinkHandle handle;

    internal LinkCapability(PdfViewer viewer, GL.LinkHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Where a page's links stand: <c>idle</c>, <c>loading</c>, <c>ready</c>, ...</summary>
    public Task<string> GetStatusAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.Status(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>A page's links; empty until the page loads (<see cref="EnsureLoadedAsync"/>).</summary>
    public async Task<IReadOnlyList<LinkItem>> GetLinksAsync(PageRef page, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Links(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>A link by id.</summary>
    public async Task<LinkItem?> GetLinkAsync(PageRef page, string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.Link(Ffi.ToFfi(page), id), cancellationToken).ConfigureAwait(false) is { } item ? Ffi.ToPublic(item) : null;
    }

    /// <summary>The smallest link under a page point.</summary>
    public async Task<LinkItem?> GetLinkAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.LinkAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } item ? Ffi.ToPublic(item) : null;

    /// <summary>The links of every loaded page.</summary>
    public async Task<IReadOnlyList<LinkItem>> GetAllLinksAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.AllLinks(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Loads a page's links.</summary>
    public Task EnsureLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.EnsureLoaded(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>Whether a page's links are loaded.</summary>
    public Task<bool> IsLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.IsLoaded(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>What activating a target would do, with no side effect.</summary>
    public async Task<LinkResolution> ResolveAsync(string targetJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetJson);
        var json = Ffi.WellFormed(targetJson, "target");
        return Ffi.ToPublic(await viewer.Run(() => handle.Resolve(json), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Activates a listed link.</summary>
    public async Task<LinkActivation> ActivateAsync(LinkItem link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        return Ffi.ToPublic(await viewer.Run(() => handle.Activate(Ffi.ToLinkInput(link)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Activates a target given as JSON.</summary>
    public async Task<LinkActivation> ActivateAsync(string targetJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetJson);
        var json = Ffi.WellFormed(targetJson, "target");
        return Ffi.ToPublic(await viewer.Run(() => handle.Activate(new GL.LinkInput.Target(json)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Activates the link under a page point; <see langword="null"/> when there is none.</summary>
    public async Task<LinkActivation?> ActivateAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.ActivateAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } a ? Ffi.ToPublic(a) : null;

    /// <summary>A label for a listed link: its URL, <c>Go to destination</c>, ...</summary>
    public async Task<string> GetLabelAsync(LinkItem link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        return await viewer.Run(() => handle.Label(Ffi.ToLinkInput(link)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A label for a target that isn't a link on a page (an annotation's link, an outline entry).</summary>
    public async Task<string> GetLabelAsync(string targetJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetJson);
        var json = Ffi.WellFormed(targetJson, "target");
        return await viewer.Run(() => handle.Label(new GL.LinkInput.Target(json)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What activating a typed target would do, with no side effect.</summary>
    public async Task<LinkResolution> ResolveAsync(AnnotationLinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var json = target.ToJson();
        return await ResolveAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Activates a typed target, such as an annotation's link.</summary>
    public async Task<LinkActivation> ActivateAsync(AnnotationLinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var json = target.ToJson();
        return await ActivateAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A label for a typed target.</summary>
    public async Task<string> GetLabelAsync(AnnotationLinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var json = target.ToJson();
        return await GetLabelAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether a tap on a link is in flight.</summary>
    public Task<bool> IsNavigationEngagedAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsNavigationEngaged(), cancellationToken);

    /// <summary>
    /// Lets taps on the pointer hub follow links: under a <c>link-nav</c> tool,
    /// a tap that goes down and up on one link activates it, and the outcome
    /// arrives on <see cref="ObserveEvents"/>. A view that forwards its pointer
    /// samples with <see cref="InteractionCapability.DispatchPointer"/> calls
    /// this once per document. A drag that starts on a link still scrolls.
    /// </summary>
    public async Task<Registration> RegisterTapHandlerAsync(CancellationToken cancellationToken = default) =>
        new(await viewer.Run(() => handle.RegisterTapHandler(), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Link events, subscribed when this returns. Ends when the document
    /// closes (at once if it already has), when the viewer is disposed, or
    /// when <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<LinkEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GL.LinkEvent, LinkEvent>(Ffi.ToPublic), GL.LinkEventListener
    {
        public void OnEvent(GL.LinkEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's links. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Link"/>.</summary>
    public async Task<LinkCapability> GetLinkAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Link(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
