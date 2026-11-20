using GA = uniffi.epdf_plugin_actions;

namespace EmbedPDF;

/// <summary>Where a lazily read resource (metadata, outline, attachments) stands.</summary>
public enum ResourceStatus
{
    /// <summary>Nothing has asked for it yet.</summary>
    Idle,
    /// <summary>A read is in flight.</summary>
    Loading,
    /// <summary>Read and current.</summary>
    Ready,
    /// <summary>The session may not read it.</summary>
    Forbidden,
    /// <summary>The last read failed.</summary>
    Error,
}

/// <summary>Whether a change came from this session or from a collaborator.</summary>
public enum DocumentEventOriginKind
{
    /// <summary>This session made it.</summary>
    Local,
    /// <summary>Another session made it.</summary>
    Remote,
}

/// <summary>Where a confirmed change came from.</summary>
/// <param name="Kind">Local or remote.</param>
/// <param name="SessionId">The session that made it.</param>
/// <param name="Subject">Who made it, when the session names one.</param>
/// <param name="At">When, as the engine's timestamp text.</param>
/// <param name="ServerSequence">The server's sequence number for a remote change.</param>
/// <param name="Transaction">The transaction it belongs to, if any.</param>
/// <param name="UndoOf">The change this one undoes, if any.</param>
public sealed record DocumentEventOrigin(
    DocumentEventOriginKind Kind, string SessionId, string? Subject, string At, long? ServerSequence,
    DocumentEventTransaction? Transaction, string? UndoOf);

/// <summary>One step of a multi-step change.</summary>
/// <param name="Id">The transaction.</param>
/// <param name="Index">This step, from 0.</param>
/// <param name="Count">Steps in the transaction.</param>
public sealed record DocumentEventTransaction(string Id, int Index, int Count);

/// <summary>Four corners in page space, y pointing down.</summary>
/// <param name="UpperLeft">The upper left corner.</param>
/// <param name="UpperRight">The upper right corner.</param>
/// <param name="LowerLeft">The lower left corner.</param>
/// <param name="LowerRight">The lower right corner.</param>
public readonly record struct PdfQuad(PdfPoint UpperLeft, PdfPoint UpperRight, PdfPoint LowerLeft, PdfPoint LowerRight)
{
    /// <summary>The four corners of <paramref name="rect"/>, with y pointing down: <see cref="UpperLeft"/> is the rect's origin.</summary>
    public PdfQuad(PdfRect rect)
        : this(
            new PdfPoint(rect.X, rect.Y),
            new PdfPoint(rect.X + rect.Width, rect.Y),
            new PdfPoint(rect.X, rect.Y + rect.Height),
            new PdfPoint(rect.X + rect.Width, rect.Y + rect.Height))
    {
    }
}

/// <summary>An annotation on a page, by object number or by position.</summary>
public abstract record AnnotationRef
{
    private protected AnnotationRef() { }

    /// <summary>The annotation's PDF object number.</summary>
    /// <param name="Page">Its page.</param>
    /// <param name="AnnotObjectNumber">Its object number.</param>
    public sealed record ObjectNumber(PageRef Page, int AnnotObjectNumber) : AnnotationRef;

    /// <summary>Its position in the page's annotation array, for an annotation with no object number.</summary>
    /// <param name="Page">Its page.</param>
    /// <param name="Value">Its index, from 0.</param>
    public sealed record Index(PageRef Page, int Value) : AnnotationRef;
}

/// <summary>A place in the document, as a link or an outline item names it. Coordinates are page space, y down.</summary>
public abstract record PageDestination
{
    private protected PageDestination() { }

    /// <summary>The page.</summary>
    public abstract PageRef Page { get; init; }

    /// <summary>A point and zoom; a missing value keeps the current one.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="X">Left edge.</param>
    /// <param name="Y">Top edge.</param>
    /// <param name="Zoom">Zoom factor.</param>
    public sealed record Xyz(PageRef Page, double? X, double? Y, double? Zoom) : PageDestination;

    /// <summary>The whole page.</summary>
    /// <param name="Page">The page.</param>
    public sealed record Fit(PageRef Page) : PageDestination;

    /// <summary>The page's width, at a top edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Y">Top edge.</param>
    public sealed record FitH(PageRef Page, double? Y) : PageDestination;

    /// <summary>The page's height, at a left edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="X">Left edge.</param>
    public sealed record FitV(PageRef Page, double? X) : PageDestination;

    /// <summary>A rectangle.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="X">Left edge.</param>
    /// <param name="Y">Top edge.</param>
    /// <param name="Width">Width.</param>
    /// <param name="Height">Height.</param>
    public sealed record FitR(PageRef Page, double X, double Y, double Width, double Height) : PageDestination;

    /// <summary>The page's bounding box.</summary>
    /// <param name="Page">The page.</param>
    public sealed record FitB(PageRef Page) : PageDestination;

    /// <summary>The bounding box's width, at a top edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Y">Top edge.</param>
    public sealed record FitBH(PageRef Page, double? Y) : PageDestination;

    /// <summary>The bounding box's height, at a left edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="X">Left edge.</param>
    public sealed record FitBV(PageRef Page, double? X) : PageDestination;
}

/// <summary>Where a link or an outline item goes.</summary>
public abstract record PageLinkTarget
{
    private protected PageLinkTarget() { }

    /// <summary>A place in this document.</summary>
    /// <param name="Destination">The place.</param>
    public sealed record GoTo(PageDestination Destination) : PageLinkTarget;

    /// <summary>A URI.</summary>
    /// <param name="Value">The URI text, as the document holds it.</param>
    public sealed record Uri(string Value) : PageLinkTarget;

    /// <summary>Another PDF file.</summary>
    /// <param name="File">Its file specification.</param>
    public sealed record GoToRemote(string File) : PageLinkTarget;

    /// <summary>An application or file to launch.</summary>
    /// <param name="Path">Its path.</param>
    public sealed record Launch(string Path) : PageLinkTarget;

    /// <summary>A named action, such as <c>NextPage</c>.</summary>
    /// <param name="Name">The action's name.</param>
    public sealed record Named(string Name) : PageLinkTarget;
}

/// <summary>What activating a link or an outline item did.</summary>
public abstract record LinkActivation
{
    private protected LinkActivation() { }

    /// <summary>The stage revealed the destination.</summary>
    public sealed record Revealed : LinkActivation;

    /// <summary>A destination with no stage to reveal it.</summary>
    /// <param name="DestinationJson">The destination as JSON.</param>
    public sealed record Destination(string DestinationJson) : LinkActivation;

    /// <summary>A URI for the host to open.</summary>
    /// <param name="Value">The URI text.</param>
    public sealed record Uri(string Value) : LinkActivation;

    /// <summary>A named action for the host.</summary>
    /// <param name="Name">The action's name.</param>
    public sealed record Named(string Name) : LinkActivation;

    /// <summary>A target the engine reports rather than follows.</summary>
    /// <param name="TargetJson">The target as JSON.</param>
    public sealed record Reported(string TargetJson) : LinkActivation;

    /// <summary>The actions plugin ran the item's action.</summary>
    public sealed record Dispatched : LinkActivation;

    /// <summary>Nothing to do.</summary>
    public sealed record None : LinkActivation;
}

/// <summary>
/// A PDF action tree as the engine extracted it: <c>{root, incomplete, warnings}</c>,
/// echoed back exactly and never reshaped. It owns a native handle;
/// <see cref="Dispose"/> releases it, and the garbage collector does if you don't.
/// </summary>
public sealed class PdfActionTree : IDisposable
{
    internal PdfActionTree(GA.PdfActionTree tree)
    {
        Handle = tree;
        IsIncomplete = tree.IsIncomplete();
        Json = tree.Json();
        RootType = tree.RootType();
    }

    internal GA.PdfActionTree Handle { get; }

    /// <summary>Whether extraction stopped early, leaving part of the tree out.</summary>
    public bool IsIncomplete { get; }

    /// <summary>The tree as JSON.</summary>
    public string Json { get; }

    /// <summary>The root node's <c>type</c> (<c>goto</c>, <c>uri</c>, <c>javascript</c>, ...), or <see langword="null"/> for an empty tree.</summary>
    public string? RootType { get; }

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => Handle.Dispose();
}
