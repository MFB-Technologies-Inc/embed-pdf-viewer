using GS = uniffi.epdf_plugin_search;

namespace EmbedPDF;

// The public enums list their cases in the generated order, so they convert
// by value; EnumMirrorTests pins the order.
internal static partial class Ffi
{
    internal static GS.SearchQuery ToFfi(SearchQuery q) =>
        new(q.Text, q.Regex, q.MatchCase, q.WholeWord, q.MatchDiacritics, q.IgnoreWhitespace);

    internal static SearchQuery ToPublic(GS.SearchQuery q) =>
        new(q.Text) { Regex = q.Regex, MatchCase = q.MatchCase, WholeWord = q.WholeWord, MatchDiacritics = q.MatchDiacritics, IgnoreWhitespace = q.IgnoreWhitespace };

    internal static SearchHit ToPublic(GS.SearchHit h) =>
        new(ToPublic(h.Page), WireInteger.ToInt(h.PageIndex, "page index"), h.Start, h.Count,
            [.. h.Segments.Select(s => new TextSegment(ToPublic(s.Quad), ToPublic(s.Rect), s.Advance))],
            ToPublicOrNull(h.Bounds), h.Snippet is { } s ? new SearchSnippet(s.Before, s.Matched, s.After) : null);

    internal static SearchHit? ToPublicOrNull(GS.SearchHit? h) => h is null ? null : ToPublic(h);

    internal static IReadOnlyList<SearchHit> ToPublic(GS.SearchHit[] hits) => [.. hits.Select(ToPublic)];

    internal static SearchRevealAxis ToPublic(GS.SearchRevealAxis axis) => axis switch
    {
        GS.SearchRevealAxis.Keep => new SearchRevealAxis.Keep(),
        GS.SearchRevealAxis.Start => new SearchRevealAxis.Start(),
        GS.SearchRevealAxis.Center => new SearchRevealAxis.Center(),
        GS.SearchRevealAxis.End => new SearchRevealAxis.End(),
        GS.SearchRevealAxis.Fraction f => new SearchRevealAxis.Fraction(f.Value),
        _ => throw Unknown(axis),
    };

    internal static GS.SearchRevealAxis ToFfi(SearchRevealAxis axis) => axis switch
    {
        SearchRevealAxis.Keep => new GS.SearchRevealAxis.Keep(),
        SearchRevealAxis.Start => new GS.SearchRevealAxis.Start(),
        SearchRevealAxis.Center => new GS.SearchRevealAxis.Center(),
        SearchRevealAxis.End => new GS.SearchRevealAxis.End(),
        SearchRevealAxis.Fraction f => new GS.SearchRevealAxis.Fraction(f.Value),
        _ => throw Unknown(axis),
    };

    internal static SearchRevealAnchor? ToPublicOrNull(GS.SearchRevealAnchor? a) =>
        a is null ? null : new SearchRevealAnchor { X = a.X is null ? null : ToPublic(a.X), Y = a.Y is null ? null : ToPublic(a.Y) };

    internal static GS.SearchRevealAnchor? ToFfiOrNull(SearchRevealAnchor? a) =>
        a is null ? null : new(a.X is null ? null : ToFfi(a.X), a.Y is null ? null : ToFfi(a.Y));

    internal static GS.SearchRevealOptions ToFfi(SearchRevealOptions o) =>
        new(ToFfiOrNull(o.Anchor), o.Behavior is { } b ? (GS.SearchScrollBehavior)(int)Defined(b, "scroll behavior") : null);

    internal static GS.SearchRevealOptions? ToFfiOrNull(SearchRevealOptions? o) => o is null ? null : ToFfi(o);

    internal static SearchSettings ToPublic(GS.SearchSettings s) =>
        new(new SearchRevealSettings(ToPublicOrNull(s.Reveal.Anchor), (SearchScrollBehavior)(int)s.Reveal.Behavior),
            new SearchHighlightSettings(s.Highlight.Color, s.Highlight.ActiveColor, (SearchBlendMode)(int)s.Highlight.BlendMode));

    internal static GS.SearchSettingsPatch ToFfi(SearchSettingsPatch p) =>
        new(p.Reveal is { } r ? new GS.SearchRevealPatch(ToFfiOrNull(r.Anchor), r.ClearAnchor, r.Behavior is { } b ? (GS.SearchScrollBehavior)(int)Defined(b, "scroll behavior") : null) : null,
            p.Highlight is { } h ? new GS.SearchHighlightPatch(h.Color, h.ActiveColor, h.BlendMode is { } m ? (GS.SearchBlendMode)(int)Defined(m, "blend mode") : null) : null);

    internal static SearchEvent ToPublic(GS.SearchEvent e) => e switch
    {
        GS.SearchEvent.Started s => new SearchEvent.Started(ToPublic(s.Event.Query)),
        GS.SearchEvent.ProgressChanged p => new SearchEvent.ProgressChanged(p.Event.PagesSearched, p.Event.PageCount, WireInteger.ToInt(p.Event.HitCount, "hit count")),
        GS.SearchEvent.Completed c => new SearchEvent.Completed(WireInteger.ToInt(c.Event.HitCount, "hit count")),
        GS.SearchEvent.Cancelled c => new SearchEvent.Cancelled((SearchCancelReason)(int)c.Event.Reason),
        GS.SearchEvent.Failed f => new SearchEvent.Failed(ToPublic(f.Event.Error)),
        GS.SearchEvent.ActiveHitChanged a => new SearchEvent.ActiveHitChanged(
            a.Event.Index is { } i ? WireInteger.ToInt(i, "active hit index") : null, ToPublicOrNull(a.Event.Hit)),
        _ => throw Unknown(e),
    };
}
