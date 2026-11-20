using GF = uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// An explicit destination as the file writes it: a page, a location in the
/// file's own coordinates (PDF units, y up) and a magnification. Page space
/// destinations are <see cref="PageDestination"/>.
/// </summary>
public abstract record PdfDestination
{
    private protected PdfDestination() { }

    /// <summary>The page.</summary>
    public abstract PageRef Page { get; init; }

    /// <summary>A point and zoom; a missing value keeps the current one.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Zoom">Zoom factor.</param>
    public sealed record Xyz(PageRef Page, double? Left, double? Top, double? Zoom) : PdfDestination;

    /// <summary>The whole page.</summary>
    /// <param name="Page">The page.</param>
    public sealed record Fit(PageRef Page) : PdfDestination;

    /// <summary>The page's width at a top edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Top">Top edge.</param>
    public sealed record FitH(PageRef Page, double? Top) : PdfDestination;

    /// <summary>The page's height at a left edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Left">Left edge.</param>
    public sealed record FitV(PageRef Page, double? Left) : PdfDestination;

    /// <summary>A rectangle.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Left">Left edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Top">Top edge.</param>
    public sealed record FitR(PageRef Page, double Left, double Bottom, double Right, double Top) : PdfDestination;

    /// <summary>The page's bounding box.</summary>
    /// <param name="Page">The page.</param>
    public sealed record FitB(PageRef Page) : PdfDestination;

    /// <summary>The bounding box's width at a top edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Top">Top edge.</param>
    public sealed record FitBH(PageRef Page, double? Top) : PdfDestination;

    /// <summary>The bounding box's height at a left edge.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Left">Left edge.</param>
    public sealed record FitBV(PageRef Page, double? Left) : PdfDestination;
}

/// <summary>Where a link goes, in the file's coordinates, as <see cref="AnnotationSelectionCapability.UpdateLinkAsync"/> writes it.</summary>
public abstract record PdfLinkTarget
{
    private protected PdfLinkTarget() { }

    /// <summary>A place in this document.</summary>
    /// <param name="Destination">The place.</param>
    public sealed record GoTo(PdfDestination Destination) : PdfLinkTarget;

    /// <summary>A URI.</summary>
    /// <param name="Value">The URI.</param>
    public sealed record Uri(string Value) : PdfLinkTarget;

    /// <summary>Another file.</summary>
    /// <param name="File">Its path.</param>
    public sealed record GoToRemote(string File) : PdfLinkTarget;

    /// <summary>A launch action.</summary>
    /// <param name="Path">What it launches.</param>
    public sealed record Launch(string Path) : PdfLinkTarget;

    /// <summary>A script.</summary>
    public sealed record JavaScript : PdfLinkTarget;

    /// <summary>A named action.</summary>
    /// <param name="Name">Its name.</param>
    public sealed record Named(string Name) : PdfLinkTarget;

    /// <summary>A target the engine does not read.</summary>
    public sealed record Unsupported : PdfLinkTarget;
}

internal static partial class Ffi
{
    internal static GF.PdfDestination ToFfi(PdfDestination destination) => destination switch
    {
        PdfDestination.Xyz d => new GF.PdfDestination.Xyz(ToFfi(d.Page), d.Left, d.Top, d.Zoom),
        PdfDestination.Fit d => new GF.PdfDestination.Fit(ToFfi(d.Page)),
        PdfDestination.FitH d => new GF.PdfDestination.FitH(ToFfi(d.Page), d.Top),
        PdfDestination.FitV d => new GF.PdfDestination.FitV(ToFfi(d.Page), d.Left),
        PdfDestination.FitR d => new GF.PdfDestination.FitR(ToFfi(d.Page), d.Left, d.Bottom, d.Right, d.Top),
        PdfDestination.FitB d => new GF.PdfDestination.FitB(ToFfi(d.Page)),
        PdfDestination.FitBH d => new GF.PdfDestination.FitBh(ToFfi(d.Page), d.Top),
        PdfDestination.FitBV d => new GF.PdfDestination.FitBv(ToFfi(d.Page), d.Left),
        null => throw new ArgumentException("A go-to target needs its destination."),
        _ => throw Unknown(destination),
    };

    internal static GF.PdfLinkTarget? ToFfiOrNull(PdfLinkTarget? target) => target switch
    {
        null => null,
        PdfLinkTarget.GoTo t => new GF.PdfLinkTarget.Goto(ToFfi(t.Destination)),
        PdfLinkTarget.Uri t => new GF.PdfLinkTarget.Uri(t.Value ?? throw new ArgumentException("A URI target needs its URI.")),
        PdfLinkTarget.GoToRemote t => new GF.PdfLinkTarget.GotoRemote(t.File ?? throw new ArgumentException("A remote target needs its file.")),
        PdfLinkTarget.Launch t => new GF.PdfLinkTarget.Launch(t.Path ?? throw new ArgumentException("A launch target needs its path.")),
        PdfLinkTarget.JavaScript => new GF.PdfLinkTarget.Javascript(),
        PdfLinkTarget.Named t => new GF.PdfLinkTarget.Named(t.Name ?? throw new ArgumentException("A named target needs its name.")),
        PdfLinkTarget.Unsupported => new GF.PdfLinkTarget.Unsupported(),
        _ => throw Unknown(target),
    };
}
