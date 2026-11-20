using GF = uniffi.epdf_ffi;
using GSt = uniffi.epdf_plugin_stage;

namespace EmbedPDF;

// The public enums list their cases in the generated order, so they convert
// by value; EnumMirrorTests pins the order. Outgoing values pass Defined.
internal static partial class Ffi
{
    internal static Camera ToPublic(GF.Camera c) => new(c.X, c.Y, c.Zoom);

    internal static GF.Camera ToFfi(Camera c) => new(c.X, c.Y, c.Zoom);

    internal static ViewAnchor ToPublic(GF.ViewAnchor a) => new(WireInteger.ToInt(a.PageIndex, "page index"), a.Fx, a.Fy);

    internal static GF.ViewAnchor ToFfi(ViewAnchor a) => new(WireInteger.ToUInt32(a.PageIndex, "page index"), a.Fx, a.Fy);

    internal static PageFrameInsets ToPublic(GF.PageFrameInsets i) => new(i.Top, i.Right, i.Bottom, i.Left);

    internal static GF.PageFrameInsets ToFfi(PageFrameInsets i) => new(i.Top, i.Right, i.Bottom, i.Left);

    internal static StageScrollMetrics ToPublic(GF.StageScrollMetrics m) =>
        new(m.ScrollLeft, m.ScrollTop, m.ScrollWidth, m.ScrollHeight, m.ClientWidth, m.ClientHeight, m.ScrollableX, m.ScrollableY);

    internal static GF.ZoomMode ToFfi(ZoomMode mode) => (GF.ZoomMode)(int)Defined(mode, "zoom mode");

    internal static GF.StageAlign ToFfi(StageAlign align) => (GF.StageAlign)(int)Defined(align, "align");

    internal static GSt.StageScrollBehavior ToFfi(StageScrollBehavior behavior) => (GSt.StageScrollBehavior)(int)Defined(behavior, "scroll behavior");

    internal static GSt.StageScrollBehavior? ToFfiOrNull(StageScrollBehavior? behavior) => behavior is { } b ? ToFfi(b) : null;

    internal static ZoomSpec ToPublic(GF.ZoomSpec spec) => spec switch
    {
        GF.ZoomSpec.Level s => new ZoomSpec.Level(s.LevelValue),
        GF.ZoomSpec.PageWidth s => new ZoomSpec.PageWidth(s.PageWidthValue),
        GF.ZoomSpec.PageHeight s => new ZoomSpec.PageHeight(s.PageHeightValue),
        GF.ZoomSpec.Mode s => new ZoomSpec.Mode((ZoomMode)(int)s.ModeValue),
        _ => throw Unknown(spec),
    };

    internal static GF.ZoomSpec ToFfi(ZoomSpec spec) => spec switch
    {
        ZoomSpec.Level s => new GF.ZoomSpec.Level(s.Value),
        ZoomSpec.PageWidth s => new GF.ZoomSpec.PageWidth(s.Value),
        ZoomSpec.PageHeight s => new GF.ZoomSpec.PageHeight(s.Value),
        ZoomSpec.Mode s => new GF.ZoomSpec.Mode(ToFfi(s.Value)),
        _ => throw Unknown(spec),
    };

    internal static StageAlignValue ToPublic(GF.StageAlignValue value) => value switch
    {
        GF.StageAlignValue.Named n => new StageAlignValue.Named((StageAlign)(int)n.Align),
        GF.StageAlignValue.Fraction f => new StageAlignValue.Fraction(f.Value),
        _ => throw Unknown(value),
    };

    internal static GF.StageAlignValue ToFfi(StageAlignValue value) => value switch
    {
        StageAlignValue.Named n => new GF.StageAlignValue.Named(ToFfi(n.Align)),
        StageAlignValue.Fraction f => new GF.StageAlignValue.Fraction(f.Value),
        _ => throw Unknown(value),
    };

    internal static StageArrivalAlignValue ToPublic(GSt.StageArrivalAlignValue value) => value switch
    {
        GSt.StageArrivalAlignValue.Align a => new StageArrivalAlignValue.Align(ToPublic(a.Value)),
        GSt.StageArrivalAlignValue.Keep => new StageArrivalAlignValue.Keep(),
        _ => throw Unknown(value),
    };

    internal static GSt.StageArrivalAlignValue ToFfi(StageArrivalAlignValue value) => value switch
    {
        StageArrivalAlignValue.Align a => new GSt.StageArrivalAlignValue.Align(ToFfi(a.Value)),
        StageArrivalAlignValue.Keep => new GSt.StageArrivalAlignValue.Keep(),
        _ => throw Unknown(value),
    };

    internal static StageGap ToPublic(GSt.StageGap gap) => gap switch
    {
        GSt.StageGap.World w => new StageGap.World(w.Units),
        GSt.StageGap.Px p => new StageGap.Px(p.PxValue),
        _ => throw Unknown(gap),
    };

    internal static GSt.StageGap ToFfi(StageGap gap) => gap switch
    {
        StageGap.World w => new GSt.StageGap.World(w.Units),
        StageGap.Px p => new GSt.StageGap.Px(p.Value),
        _ => throw Unknown(gap),
    };

    internal static StageGridColumns ToPublic(GSt.StageGridColumns columns) => columns switch
    {
        GSt.StageGridColumns.Count c => new StageGridColumns.Count(WireInteger.ToInt(c.CountValue, "column count")),
        GSt.StageGridColumns.Square => new StageGridColumns.Square(),
        GSt.StageGridColumns.Auto => new StageGridColumns.Auto(),
        _ => throw Unknown(columns),
    };

    internal static GSt.StageGridColumns ToFfi(StageGridColumns columns) => columns switch
    {
        StageGridColumns.Count c => new GSt.StageGridColumns.Count(WireInteger.ToUInt32(c.Value, "column count")),
        StageGridColumns.Square => new GSt.StageGridColumns.Square(),
        StageGridColumns.Auto => new GSt.StageGridColumns.Auto(),
        _ => throw Unknown(columns),
    };

    internal static StageRevealAnchorValue ToPublic(GSt.StageRevealAnchorValue value) => value switch
    {
        GSt.StageRevealAnchorValue.Word w => new StageRevealAnchorValue.Word((StageRevealWord)(int)w.WordValue),
        GSt.StageRevealAnchorValue.Fraction f => new StageRevealAnchorValue.Fraction(f.Value),
        _ => throw Unknown(value),
    };

    internal static GSt.StageRevealAnchorValue ToFfi(StageRevealAnchorValue value) => value switch
    {
        StageRevealAnchorValue.Word w => new GSt.StageRevealAnchorValue.Word((GSt.StageRevealWord)(int)Defined(w.Value, "reveal word")),
        StageRevealAnchorValue.Fraction f => new GSt.StageRevealAnchorValue.Fraction(f.Value),
        _ => throw Unknown(value),
    };

    internal static StageRevealZoom ToPublic(GSt.StageRevealZoom zoom) => zoom switch
    {
        GSt.StageRevealZoom.Level l => new StageRevealZoom.Level(l.LevelValue),
        GSt.StageRevealZoom.Keep => new StageRevealZoom.Keep(),
        GSt.StageRevealZoom.Fit => new StageRevealZoom.Fit(),
        GSt.StageRevealZoom.FitWidth => new StageRevealZoom.FitWidth(),
        GSt.StageRevealZoom.FitHeight => new StageRevealZoom.FitHeight(),
        _ => throw Unknown(zoom),
    };

    internal static GSt.StageRevealZoom ToFfi(StageRevealZoom zoom) => zoom switch
    {
        StageRevealZoom.Level l => new GSt.StageRevealZoom.Level(l.Value),
        StageRevealZoom.Keep => new GSt.StageRevealZoom.Keep(),
        StageRevealZoom.Fit => new GSt.StageRevealZoom.Fit(),
        StageRevealZoom.FitWidth => new GSt.StageRevealZoom.FitWidth(),
        StageRevealZoom.FitHeight => new GSt.StageRevealZoom.FitHeight(),
        _ => throw Unknown(zoom),
    };

    internal static StageZoomMode ToPublic(GSt.StageZoomMode mode) => mode switch
    {
        GSt.StageZoomMode.Mode m => new StageZoomMode.Mode((ZoomMode)(int)m.ModeValue),
        GSt.StageZoomMode.Custom => new StageZoomMode.Custom(),
        _ => throw Unknown(mode),
    };

    internal static StageZoomTarget ToPublic(GSt.StageZoomTarget target) => target switch
    {
        GSt.StageZoomTarget.Level l => new StageZoomTarget.Level(l.LevelValue),
        GSt.StageZoomTarget.Spec s => new StageZoomTarget.Spec(ToPublic(s.SpecValue)),
        _ => throw Unknown(target),
    };

    internal static GSt.StageZoomTarget ToFfi(StageZoomTarget target) => target switch
    {
        StageZoomTarget.Level l => new GSt.StageZoomTarget.Level(l.Value),
        StageZoomTarget.Spec s => new GSt.StageZoomTarget.Spec(ToFfi(s.Value)),
        _ => throw Unknown(target),
    };

    internal static StageAlignment ToPublic(GF.StageAlignment a) => new((StageAlign)(int)a.X, (StageAlign)(int)a.Y);

    internal static GF.StageAlignment ToFfi(StageAlignment a) => new(ToFfi(a.X), ToFfi(a.Y));

    internal static StageAlignmentValue ToPublic(GF.StageAlignmentValue a) => new(ToPublic(a.X), ToPublic(a.Y));

    internal static GF.StageAlignmentValue ToFfi(StageAlignmentValue a) => new(ToFfi(a.X), ToFfi(a.Y));

    internal static StageArrivalAlignment ToPublic(GSt.StageArrivalAlignment a) => new(ToPublic(a.X), ToPublic(a.Y));

    internal static GSt.StageArrivalAlignment ToFfi(StageArrivalAlignment a) => new(ToFfi(a.X), ToFfi(a.Y));

    internal static GSt.StagePartialArrivalAlignment ToFfi(StagePartialArrivalAlignment a) =>
        new(a.X is null ? null : ToFfi(a.X), a.Y is null ? null : ToFfi(a.Y));

    internal static StageZoomBoxLook ToPublic(GSt.StageZoomBoxLook look) => new() { Fill = look.Fill, Stroke = look.Stroke };

    internal static GSt.StageZoomBoxLook ToFfi(StageZoomBoxLook look) => new(look.Fill, look.Stroke);

    internal static StageSettings ToPublic(GSt.StageSettings s) => new(
        (StageFlow)(int)s.Flow, (StageLayout)(int)s.Layout, (StageSpreadMode)(int)s.Spread, (StageSizingMode)(int)s.Sizing,
        ToPublic(s.Columns), s.Bounded, s.Padding, ToPublic(s.Gap), ToPublic(s.PageFrame), (StageDirection)(int)s.Direction,
        ToPublic(s.FitAlign), ToPublic(s.ArrivalAlign), ToPublic(s.ZoomAlign), ToPublic(s.AnchorAlign),
        s.ViewRotation, ToPublic(s.Zoom), s.MinZoom, s.MaxZoom, (StageScrollBehavior)(int)s.ScrollBehavior,
        s.Interaction, s.PanFallback, s.ZoomGestures, s.TurnGestures, s.ScrollChaining, ToPublic(s.ZoomBox), s.ViewUnitsPerPoint);

    internal static GSt.StageSettings ToFfi(StageSettings s) => new(
        (GSt.StageFlow)(int)Defined(s.Flow, "flow"), (GSt.StageLayout)(int)Defined(s.Layout, "layout"),
        (GF.StageSpreadMode)(int)Defined(s.Spread, "spread mode"), (GF.StageSizingMode)(int)Defined(s.Sizing, "sizing mode"),
        ToFfi(s.Columns), s.Bounded, s.Padding, ToFfi(s.Gap), ToFfi(s.PageFrame), (GF.StageDirection)(int)Defined(s.Direction, "direction"),
        ToFfi(s.FitAlign), ToFfi(s.ArrivalAlign), ToFfi(s.ZoomAlign), ToFfi(s.AnchorAlign),
        s.ViewRotation, ToFfi(s.Zoom), s.MinZoom, s.MaxZoom, ToFfi(s.ScrollBehavior),
        s.Interaction, s.PanFallback, s.ZoomGestures, s.TurnGestures, s.ScrollChaining, ToFfi(s.ZoomBox), s.ViewUnitsPerPoint);

    internal static GSt.StageSettingsPatch ToFfi(StageSettingsPatch p) => new(
        TurnGestures: p.TurnGestures,
        Flow: p.Flow is { } flow ? (GSt.StageFlow)(int)Defined(flow, "flow") : null,
        Layout: p.Layout is { } layout ? (GSt.StageLayout)(int)Defined(layout, "layout") : null,
        Spread: p.Spread is { } spread ? (GF.StageSpreadMode)(int)Defined(spread, "spread mode") : null,
        Sizing: p.Sizing is { } sizing ? (GF.StageSizingMode)(int)Defined(sizing, "sizing mode") : null,
        Columns: p.Columns is null ? null : ToFfi(p.Columns),
        Bounded: p.Bounded,
        Padding: p.Padding,
        Gap: p.Gap is null ? null : ToFfi(p.Gap),
        PageFrame: p.PageFrame is { } frame ? ToFfi(frame) : null,
        Direction: p.Direction is { } direction ? (GF.StageDirection)(int)Defined(direction, "direction") : null,
        FitAlign: p.FitAlign is null ? null : ToFfi(p.FitAlign),
        ArrivalAlign: p.ArrivalAlign is null ? null : ToFfi(p.ArrivalAlign),
        ZoomAlign: p.ZoomAlign is null ? null : ToFfi(p.ZoomAlign),
        AnchorAlign: p.AnchorAlign is null ? null : ToFfi(p.AnchorAlign),
        ViewRotation: p.ViewRotation,
        Zoom: p.Zoom is null ? null : ToFfi(p.Zoom),
        MinZoom: p.MinZoom,
        MaxZoom: p.MaxZoom,
        ScrollBehavior: ToFfiOrNull(p.ScrollBehavior),
        Interaction: p.Interaction,
        PanFallback: p.PanFallback,
        ZoomGestures: p.ZoomGestures,
        ScrollChaining: p.ScrollChaining,
        ZoomBox: p.ZoomBox is null ? null : ToFfi(p.ZoomBox),
        ViewUnitsPerPoint: p.ViewUnitsPerPoint);

    internal static GSt.StageBoxQuery ToFfi(StageBoxQuery q) => new(
        q.MinWidth, q.MaxWidth, q.MinHeight, q.MaxHeight,
        q.Orientation is { } o ? (GSt.StageOrientation)(int)Defined(o, "orientation") : null);

    internal static GSt.StageResponsiveRule ToFfi(StageResponsiveRule rule) =>
        new(ToFfi(rule.When ?? throw new ArgumentException("A responsive rule needs When.", nameof(rule))), rule.Name,
            rule.Settings is null ? null : ToFfi(rule.Settings));

    internal static StageViewpoint ToPublic(GSt.StageViewpoint v) => new(ToPublic(v.Anchor), ToPublic(v.Zoom));

    internal static GSt.StageViewpoint ToFfi(StageViewpoint v) => new(ToFfi(v.Anchor), ToFfi(v.Zoom));

    internal static StageViewState ToPublic(GSt.StageViewState v) =>
        new(ToPublic(v.Settings), v.Cursor is { } c ? WireInteger.ToInt(c, "cursor") : null, ToPublic(v.Anchor));

    internal static GSt.StageViewState ToFfi(StageViewState v) =>
        new(ToFfi(v.Settings), v.Cursor is { } c ? WireInteger.ToUInt32(c, "cursor") : null, ToFfi(v.Anchor));

    internal static GSt.StageGoToOptions? ToFfiOrNull(StageGoToOptions? o) => o is null ? null :
        new(ToFfiOrNull(o.Behavior), o.Viewpoint is null ? null : ToFfi(o.Viewpoint), o.ArrivalAlign is null ? null : ToFfi(o.ArrivalAlign));

    internal static GSt.StageScrollToOptions ToFfi(StageScrollToOptions o) => new(o.Left, o.Top, ToFfiOrNull(o.Behavior));

    internal static GSt.StageRevealOptions? ToFfiOrNull(StageRevealOptions? o) => o is null ? null :
        new(ToFfiOrNull(o.Behavior), o.Rect is { } r ? ToFfi(r) : null, o.Zoom is null ? null : ToFfi(o.Zoom),
            o.Anchor is null ? null : new GSt.StageRevealAnchor(o.Anchor.X is null ? null : ToFfi(o.Anchor.X), o.Anchor.Y is null ? null : ToFfi(o.Anchor.Y)));

    internal static StagePageHit ToPublic(GSt.StagePageHit h) => new(ToPublic(h.Page), ToPublic(h.Point), h.Scale, h.Rotation, h.Zoom);

    internal static StagePageTransform ToPublic(GSt.StagePageTransform t) => new(
        t.Rotation, t.ViewWidth, t.ViewHeight, t.ContentWidth, t.ContentHeight, t.DeviceWidth, t.DeviceHeight,
        t.RenderScale, t.ViewScale, t.BaseScale, t.Zoom);

    internal static StageVisiblePage ToPublic(GSt.StageVisiblePage p) => new(
        ToPublic(p.Page), WireInteger.ToInt(p.PageIndex, "page index"), p.X, p.Y, p.Width, p.Height, p.Rotation, p.ContentScale,
        p.ScreenX, p.ScreenY, ToPublic(p.Transform), ToPublic(p.VisibleRect));

    internal static PageDestination ToPublic(GSt.StagePageDestination d) => d switch
    {
        GSt.StagePageDestination.Xyz x => new PageDestination.Xyz(ToPublic(x.Page), x.X, x.Y, x.Zoom),
        GSt.StagePageDestination.Fit x => new PageDestination.Fit(ToPublic(x.Page)),
        GSt.StagePageDestination.FitH x => new PageDestination.FitH(ToPublic(x.Page), x.Y),
        GSt.StagePageDestination.FitV x => new PageDestination.FitV(ToPublic(x.Page), x.X),
        GSt.StagePageDestination.FitR x => new PageDestination.FitR(ToPublic(x.Page), x.X, x.Y, x.Width, x.Height),
        GSt.StagePageDestination.FitB x => new PageDestination.FitB(ToPublic(x.Page)),
        GSt.StagePageDestination.FitBh x => new PageDestination.FitBH(ToPublic(x.Page), x.Y),
        GSt.StagePageDestination.FitBv x => new PageDestination.FitBV(ToPublic(x.Page), x.X),
        _ => throw Unknown(d),
    };

    internal static GSt.StagePageDestination ToStageFfi(PageDestination d) => d switch
    {
        PageDestination.Xyz x => new GSt.StagePageDestination.Xyz(ToFfi(x.Page), x.X, x.Y, x.Zoom),
        PageDestination.Fit x => new GSt.StagePageDestination.Fit(ToFfi(x.Page)),
        PageDestination.FitH x => new GSt.StagePageDestination.FitH(ToFfi(x.Page), x.Y),
        PageDestination.FitV x => new GSt.StagePageDestination.FitV(ToFfi(x.Page), x.X),
        PageDestination.FitR x => new GSt.StagePageDestination.FitR(ToFfi(x.Page), x.X, x.Y, x.Width, x.Height),
        PageDestination.FitB x => new GSt.StagePageDestination.FitB(ToFfi(x.Page)),
        PageDestination.FitBH x => new GSt.StagePageDestination.FitBh(ToFfi(x.Page), x.Y),
        PageDestination.FitBV x => new GSt.StagePageDestination.FitBv(ToFfi(x.Page), x.X),
        _ => throw Unknown(d),
    };

    internal static StageEvent ToPublic(GSt.StageEvent e) => e switch
    {
        GSt.StageEvent.PageChanged p => new StageEvent.PageChanged(
            p.Event.Page is null ? null : ToPublic(p.Event.Page),
            WireInteger.ToInt(p.Event.PageIndex, "page index"), WireInteger.ToInt(p.Event.PreviousPageIndex, "page index")),
        GSt.StageEvent.ZoomChanged z => new StageEvent.ZoomChanged(z.Event.Level, z.Event.PreviousLevel, ToPublic(z.Event.Mode)),
        GSt.StageEvent.CameraChanged c => new StageEvent.CameraChanged(ToPublic(c.Event.Camera)),
        GSt.StageEvent.MotionEnded m => new StageEvent.MotionEnded(ToPublic(m.Event.Camera)),
        GSt.StageEvent.SettingsChanged s => new StageEvent.SettingsChanged(
            ToPublic(s.Event.Settings), [.. s.Event.Changed.Select(k => (StageSettingKey)(int)k)]),
        GSt.StageEvent.ViewportChanged v => new StageEvent.ViewportChanged(ToPublic(v.Event.Size)),
        _ => throw Unknown(e),
    };
}
