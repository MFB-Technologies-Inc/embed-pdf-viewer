using GF = uniffi.epdf_ffi;
using GL = uniffi.epdf_plugin_link;

namespace EmbedPDF;

/// <summary>Mappings for the types several plugins share.</summary>
internal static partial class Ffi
{
    internal static PdfRect ToPublic(GF.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    internal static PdfRect? ToPublicOrNull(GF.Rect? rect) => rect is null ? null : ToPublic(rect);

    internal static GF.Rect ToFfi(PdfRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    internal static PdfPoint ToPublic(GF.Point point) => new(point.X, point.Y);

    internal static GF.Point ToFfi(PdfPoint point) => new(point.X, point.Y);

    internal static PdfSize ToPublic(GF.Size size) => new(size.Width, size.Height);

    internal static GF.Size ToFfi(PdfSize size) => new(size.Width, size.Height);

    internal static PdfQuad ToPublic(GF.Quad quad) =>
        new(ToPublic(quad.UpperLeft), ToPublic(quad.UpperRight), ToPublic(quad.LowerLeft), ToPublic(quad.LowerRight));

    internal static PageRef? ToPublicOrNull(GF.PageRef? page) => page is null ? null : ToPublic(page);

    internal static GF.PageRef? ToFfiOrNull(PageRef? page) => page is { } value ? ToFfi(value) : null;

    internal static GF.PageRef[] ToFfi(IEnumerable<PageRef> pages) => [.. pages.Select(ToFfi)];

    internal static IReadOnlyList<PageRef> ToPublic(GF.PageRef[] pages) => [.. pages.Select(p => ToPublic(p))];

    internal static IReadOnlyList<PageInfo> ToPublic(GF.PageInfo[] pages) => [.. pages.Select(ToPublic)];

    internal static PluginErrorInfo? ToPublicOrNull(GF.PluginErrorInfo? error) => error is null ? null : ToPublic(error);

    internal static ResourceStatus ToPublic(GF.ResourceStatus status) => status switch
    {
        GF.ResourceStatus.Idle => ResourceStatus.Idle,
        GF.ResourceStatus.Loading => ResourceStatus.Loading,
        GF.ResourceStatus.Ready => ResourceStatus.Ready,
        GF.ResourceStatus.Forbidden => ResourceStatus.Forbidden,
        _ => ResourceStatus.Error,
    };

    internal static DocumentEventOrigin ToPublic(GF.DocumentEventOrigin origin) =>
        new(origin.Kind == GF.DocumentEventOriginKind.Remote ? DocumentEventOriginKind.Remote : DocumentEventOriginKind.Local,
            origin.SessionId, origin.Subject, origin.At, origin.ServerSequence,
            origin.Transaction is { } t ? new DocumentEventTransaction(t.Id, WireInteger.ToInt(t.Index, "transaction index"), WireInteger.ToInt(t.Count, "transaction count")) : null,
            origin.UndoOf);

    internal static AnnotationRef ToPublic(GF.AnnotationRef annotation) => annotation switch
    {
        GF.AnnotationRef.ObjectNumber n => new AnnotationRef.ObjectNumber(ToPublic(n.Page), WireInteger.ToInt(n.AnnotObjectNumber, "annotation object number")),
        GF.AnnotationRef.Index i => new AnnotationRef.Index(ToPublic(i.Page), WireInteger.ToInt(i.IndexValue, "annotation index")),
        _ => throw Unknown(annotation),
    };

    internal static GF.AnnotationRef ToFfi(AnnotationRef annotation) => annotation switch
    {
        AnnotationRef.ObjectNumber n => new GF.AnnotationRef.ObjectNumber(ToFfi(n.Page), WireInteger.ToUInt32(n.AnnotObjectNumber, "annotation object number")),
        AnnotationRef.Index i => new GF.AnnotationRef.Index(ToFfi(i.Page), WireInteger.ToUInt32(i.Value, "annotation index")),
        _ => throw Unknown(annotation),
    };

    internal static PageDestination ToPublic(GF.PageDestination destination) => destination switch
    {
        GF.PageDestination.Xyz d => new PageDestination.Xyz(ToPublic(d.Page), d.X, d.Y, d.Zoom),
        GF.PageDestination.Fit d => new PageDestination.Fit(ToPublic(d.Page)),
        GF.PageDestination.FitH d => new PageDestination.FitH(ToPublic(d.Page), d.Y),
        GF.PageDestination.FitV d => new PageDestination.FitV(ToPublic(d.Page), d.X),
        GF.PageDestination.FitR d => new PageDestination.FitR(ToPublic(d.Page), d.X, d.Y, d.Width, d.Height),
        GF.PageDestination.FitB d => new PageDestination.FitB(ToPublic(d.Page)),
        GF.PageDestination.FitBh d => new PageDestination.FitBH(ToPublic(d.Page), d.Y),
        GF.PageDestination.FitBv d => new PageDestination.FitBV(ToPublic(d.Page), d.X),
        _ => throw Unknown(destination),
    };

    internal static GF.PageDestination ToFfi(PageDestination destination) => destination switch
    {
        PageDestination.Xyz d => new GF.PageDestination.Xyz(ToFfi(d.Page), d.X, d.Y, d.Zoom),
        PageDestination.Fit d => new GF.PageDestination.Fit(ToFfi(d.Page)),
        PageDestination.FitH d => new GF.PageDestination.FitH(ToFfi(d.Page), d.Y),
        PageDestination.FitV d => new GF.PageDestination.FitV(ToFfi(d.Page), d.X),
        PageDestination.FitR d => new GF.PageDestination.FitR(ToFfi(d.Page), d.X, d.Y, d.Width, d.Height),
        PageDestination.FitB d => new GF.PageDestination.FitB(ToFfi(d.Page)),
        PageDestination.FitBH d => new GF.PageDestination.FitBh(ToFfi(d.Page), d.Y),
        PageDestination.FitBV d => new GF.PageDestination.FitBv(ToFfi(d.Page), d.X),
        _ => throw Unknown(destination),
    };

    internal static PageLinkTarget ToPublic(GF.PageLinkTarget target) => target switch
    {
        GF.PageLinkTarget.Goto t => new PageLinkTarget.GoTo(ToPublic(t.Destination)),
        GF.PageLinkTarget.Uri t => new PageLinkTarget.Uri(t.UriValue),
        GF.PageLinkTarget.GotoRemote t => new PageLinkTarget.GoToRemote(t.File),
        GF.PageLinkTarget.Launch t => new PageLinkTarget.Launch(t.Path),
        GF.PageLinkTarget.Named t => new PageLinkTarget.Named(t.Name),
        _ => throw Unknown(target),
    };

    internal static PageLinkTarget? ToPublicOrNull(GF.PageLinkTarget? target) => target is null ? null : ToPublic(target);

    internal static GF.PageLinkTarget ToFfi(PageLinkTarget target) => target switch
    {
        PageLinkTarget.GoTo t => new GF.PageLinkTarget.Goto(ToFfi(t.Destination)),
        PageLinkTarget.Uri t => new GF.PageLinkTarget.Uri(t.Value),
        PageLinkTarget.GoToRemote t => new GF.PageLinkTarget.GotoRemote(t.File),
        PageLinkTarget.Launch t => new GF.PageLinkTarget.Launch(t.Path),
        PageLinkTarget.Named t => new GF.PageLinkTarget.Named(t.Name),
        _ => throw Unknown(target),
    };

    internal static GF.PageLinkTarget? ToFfiOrNull(PageLinkTarget? target) => target is null ? null : ToFfi(target);

    internal static LinkActivation ToPublic(GL.LinkActivation activation) => activation switch
    {
        GL.LinkActivation.Revealed => new LinkActivation.Revealed(),
        GL.LinkActivation.Destination d => new LinkActivation.Destination(d.DestinationJson),
        GL.LinkActivation.Uri u => new LinkActivation.Uri(u.UriValue),
        GL.LinkActivation.Named n => new LinkActivation.Named(n.Name),
        GL.LinkActivation.Reported r => new LinkActivation.Reported(r.TargetJson),
        GL.LinkActivation.Dispatched => new LinkActivation.Dispatched(),
        GL.LinkActivation.None => new LinkActivation.None(),
        _ => throw Unknown(activation),
    };

    internal static PdfException Unknown(object value) =>
        new("RangeError", PdfErrorCode.InvalidState, $"unknown {value.GetType().Name}");
}
