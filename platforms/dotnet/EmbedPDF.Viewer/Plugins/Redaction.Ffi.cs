using GF = uniffi.epdf_ffi;
using GS = uniffi.epdf_plugin_search;
using GRd = uniffi.epdf_plugin_redaction;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static RedactionApplyResult ToPublic(GRd.RedactionApplyResult r) => new(ToPublic(r.Scope), [.. r.Results.Select(x0 => ToPublic(x0))], WireInteger.ToInt(r.RemovedAnnotationCount, "removed annotation count"), r.OpId);

    internal static RedactionCollateral ToPublic(GRd.RedactionCollateral r) => new(WireInteger.ToInt(r.Count, "count"), [.. r.Refs.Select(x0 => ToPublic(x0))]);

    internal static GRd.RedactionLabelPatch ToFfi(RedactionLabelPatch r) => r is null ? throw new ArgumentException("A redaction label patch is required.") : new(OverlayText: r.OverlayText, ClearOverlayText: r.ClearOverlayText, Repeat: r.Repeat);

    internal static RedactionMark ToPublic(GRd.RedactionMark r) => new(ToPublic(r.AnnotationRef), ToPublic(r.Page), r.PageIndex is { } x0 ? WireInteger.ToInt(x0, "page index") : null, (RedactionMarkKind)(int)r.Kind, ToPublic(r.Bounds), r.OverlayText, r.Repeat);

    internal static GRd.RedactionOverlayText ToFfi(RedactionOverlayText r) => r is null ? throw new ArgumentException("A redaction overlay text is required.") : new(Color: r.Color, FontFamily: r.FontFamily, FontSize: r.FontSize);

    internal static RedactionPageResult ToPublic(GRd.RedactionPageResult r) => new(ToPublic(r.Page), (RedactionPageStatus)(int)r.Status, WireInteger.ToInt(r.RemovedAnnotationCount, "removed annotation count"));

    internal static RedactionSettings ToPublic(GRd.RedactionSettings r) => new(r.Fill, ToPublic(r.OverlayText));

    internal static RedactionSkipped ToPublic(GRd.RedactionSkipped r) => new(ToPublic(r.AnnotationRef), r.Reason);

    internal static RedactionTextSettings ToPublic(GRd.RedactionTextSettings r) => new(r.Color, r.FontFamily, r.FontSize);

    internal static RedactionUnmarkFailure ToPublic(GRd.RedactionUnmarkFailure r) => new(ToPublic(r.AnnotationRef), r.Code, r.Message, r.Capability, r.Permission);

    internal static RedactionUnmarkResult ToPublic(GRd.RedactionUnmarkResult r) => new([.. r.Applied.Select(x0 => ToPublic(x0))], [.. r.Skipped.Select(x0 => ToPublic(x0))], [.. r.Failed.Select(x0 => ToPublic(x0))]);

    internal static RedactionApplyScope ToPublic(GRd.RedactionApplyScope u) => u switch
    {
        GRd.RedactionApplyScope.Pages c => new RedactionApplyScope.Pages([.. c.PagesValue.Select(x0 => ToPublic(x0))]),
        GRd.RedactionApplyScope.Annotations c => new RedactionApplyScope.Annotations([.. c.Refs.Select(x0 => ToPublic(x0))]),
        _ => throw Unknown(u),
    };

    internal static RedactionEvent ToPublic(GRd.RedactionEvent u) => u switch
    {
        GRd.RedactionEvent.Applied c => new RedactionEvent.Applied(ToPublic(c.Result)),
        GRd.RedactionEvent.PendingChanged c => new RedactionEvent.PendingChanged([.. c.Pages.Select(x0 => ToPublic(x0))]),
        _ => throw Unknown(u),
    };
}
