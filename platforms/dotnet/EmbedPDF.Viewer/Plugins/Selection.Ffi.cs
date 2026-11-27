using GSl = uniffi.epdf_plugin_selection;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static SelectionSettings ToPublic(GSl.SelectionSettings s) =>
        new(s.DragThresholdPx, s.Color, new SelectionHandleSettings(s.Handles.Color, s.Handles.Shadow));

    internal static GSl.SelectionSettingsPatch ToFfi(SelectionSettingsPatch p) =>
        new(p.DragThresholdPx, p.Color, p.ClearColor, p.Handles is { } h ? new GSl.SelectionHandleSettingsPatch(h.Color, h.ClearColor, h.Shadow) : null);

    internal static SelectionTextPosition ToPublic(GSl.SelectionTextPosition p) => new(ToPublic(p.Page), p.Index);

    internal static GSl.SelectionTextPosition ToFfi(SelectionTextPosition p) => new(ToFfi(p.Page), p.Index);

    internal static SelectionTextRange ToPublic(GSl.SelectionTextRange r) => new(ToPublic(r.Start), ToPublic(r.End));

    internal static SelectionTextRange? ToPublicOrNull(GSl.SelectionTextRange? r) => r is null ? null : ToPublic(r);

    internal static GSl.SelectionTextRange ToFfi(SelectionTextRange r) => new(ToFfi(r.Start), ToFfi(r.End));

    internal static GSl.SelectionInput ToFfi(SelectionInput input) => input switch
    {
        SelectionInput.Range r => new GSl.SelectionInput.Range(ToFfi(r.Value ?? throw new ArgumentException("A range input needs its range.", nameof(input)))),
        SelectionInput.Span s => new GSl.SelectionInput.Span(ToFfi(s.Page), s.Start, s.Count),
        _ => throw Unknown(input),
    };

    internal static SelectionSegment ToPublic(GSl.SelectionSegment s) => new(ToPublic(s.Quad), ToPublic(s.Rect), s.Advance);

    internal static SelectionEndpoint? ToPublicOrNull(GSl.SelectionEndpoint? e) =>
        e is null ? null : new(ToPublic(e.Page), ToPublic(e.GlyphQuad), e.Advance, ToPublic(e.Rect));

    internal static SelectionSnapshot ToPublic(GSl.SelectionSnapshot s) => new(
        [.. s.Pages.Select(p => new SelectionSnapshotPage(ToPublic(p.Page), [.. p.Segments.Select(ToPublic)]))],
        ToPublicOrNull(s.Start), ToPublicOrNull(s.End), (SelectionDirection)(int)s.Direction, ToPublicOrNull(s.Range));

    internal static SelectionEvent ToPublic(GSl.SelectionEvent e) => e switch
    {
        GSl.SelectionEvent.Changed c => new SelectionEvent.Changed(ToPublicOrNull(c.Event.Range), [.. c.Event.Pages.Select(ToPublic)]),
        GSl.SelectionEvent.Committed c => new SelectionEvent.Committed(ToPublic(c.Event.Range)),
        GSl.SelectionEvent.Cleared => new SelectionEvent.Cleared(),
        _ => throw Unknown(e),
    };
}
