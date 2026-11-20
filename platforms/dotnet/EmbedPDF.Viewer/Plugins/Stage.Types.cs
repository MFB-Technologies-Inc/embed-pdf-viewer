namespace EmbedPDF;

// The stage's values are plain doubles and records with no view type in
// them, so a model can drive the stage from a viewport size, a scroll offset
// and pointer samples. Coordinates pass through unrounded.

/// <summary>The stage camera: the world point at the viewport's top-left and the zoom.</summary>
/// <param name="X">World x at the viewport's left edge.</param>
/// <param name="Y">World y at the viewport's top edge.</param>
/// <param name="Zoom">Zoom level; 1 is one view unit per point.</param>
public readonly record struct Camera(double X, double Y, double Zoom);

/// <summary>A point on a page as fractions of its size, for restoring a view.</summary>
/// <param name="PageIndex">The page, from 0.</param>
/// <param name="Fx">Fraction across the page.</param>
/// <param name="Fy">Fraction down the page.</param>
public readonly record struct ViewAnchor(int PageIndex, double Fx, double Fy);

/// <summary>Space around each page, in view units.</summary>
public readonly record struct PageFrameInsets(double Top, double Right, double Bottom, double Left);

/// <summary>The stage as a scroll view sees it, in viewport points.</summary>
/// <param name="ScrollLeft">Horizontal offset.</param>
/// <param name="ScrollTop">Vertical offset.</param>
/// <param name="ScrollWidth">Content width.</param>
/// <param name="ScrollHeight">Content height.</param>
/// <param name="ClientWidth">Viewport width.</param>
/// <param name="ClientHeight">Viewport height.</param>
/// <param name="ScrollableX">Whether the content is wider than the viewport.</param>
/// <param name="ScrollableY">Whether the content is taller than the viewport.</param>
public readonly record struct StageScrollMetrics(
    double ScrollLeft, double ScrollTop, double ScrollWidth, double ScrollHeight,
    double ClientWidth, double ClientHeight, bool ScrollableX, bool ScrollableY);

/// <summary>A named fit.</summary>
public enum ZoomMode
{
    /// <summary>Fit the page width, capped at actual size.</summary>
    Automatic,
    /// <summary>The whole page.</summary>
    FitPage,
    /// <summary>The page width.</summary>
    FitWidth,
    /// <summary>Every page.</summary>
    FitAll,
}

/// <summary>An edge or the middle.</summary>
public enum StageAlign
{
    /// <summary>The leading edge.</summary>
    Start,
    /// <summary>The middle.</summary>
    Center,
    /// <summary>The trailing edge.</summary>
    End,
}

/// <summary>Reading direction.</summary>
public enum StageDirection
{
    /// <summary>Left to right.</summary>
    Ltr,
    /// <summary>Right to left.</summary>
    Rtl,
}

/// <summary>How pages pair into spreads.</summary>
public enum StageSpreadMode
{
    /// <summary>One page per item.</summary>
    None,
    /// <summary>Spreads start on odd pages.</summary>
    Odd,
    /// <summary>Spreads start on even pages.</summary>
    Even,
}

/// <summary>How pages of different sizes are scaled.</summary>
public enum StageSizingMode
{
    /// <summary>Each at its own size.</summary>
    Intrinsic,
    /// <summary>All to one width.</summary>
    Uniform,
}

/// <summary>Scrolling or one item at a time.</summary>
public enum StageFlow
{
    /// <summary>One continuous scroll.</summary>
    Continuous,
    /// <summary>One item per screen.</summary>
    Paged,
}

/// <summary>How items are laid out.</summary>
public enum StageLayout
{
    /// <summary>A column.</summary>
    Vertical,
    /// <summary>A row.</summary>
    Horizontal,
    /// <summary>A grid.</summary>
    Grid,
}

/// <summary>A viewport orientation, for responsive rules.</summary>
public enum StageOrientation
{
    /// <summary>Taller than wide.</summary>
    Portrait,
    /// <summary>Wider than tall.</summary>
    Landscape,
}

/// <summary>Where a revealed rectangle lands on an axis.</summary>
public enum StageRevealWord
{
    /// <summary>Scroll only as far as needed.</summary>
    Keep,
    /// <summary>At the leading edge.</summary>
    Start,
    /// <summary>In the middle.</summary>
    Center,
    /// <summary>At the trailing edge.</summary>
    End,
}

/// <summary>How a move animates.</summary>
public enum StageScrollBehavior
{
    /// <summary>A tween.</summary>
    Smooth,
    /// <summary>A jump.</summary>
    Instant,
}

/// <summary>One stage setting, as <see cref="StageEvent.SettingsChanged"/> names it.</summary>
public enum StageSettingKey
{
#pragma warning disable CS1591 // Each case names the StageSettings property of the same name.
    Flow, Layout, Spread, Sizing, Columns, Bounded, Padding, Gap, PageFrame, Direction, FitAlign, ArrivalAlign, ZoomAlign,
    AnchorAlign, ViewRotation, Zoom, MinZoom, MaxZoom, ScrollBehavior, Interaction, PanFallback, ZoomGestures, TurnGestures,
    ScrollChaining, ZoomBox, ViewUnitsPerPoint,
#pragma warning restore CS1591
}

/// <summary>A zoom: a level, a fraction of the page width or height on screen, or a named fit.</summary>
public abstract record ZoomSpec
{
    private protected ZoomSpec() { }

    /// <summary>A zoom level.</summary>
    public sealed record Level(double Value) : ZoomSpec;

    /// <summary>The page width as this fraction of the viewport width.</summary>
    public sealed record PageWidth(double Value) : ZoomSpec;

    /// <summary>The page height as this fraction of the viewport height.</summary>
    public sealed record PageHeight(double Value) : ZoomSpec;

    /// <summary>A named fit.</summary>
    public sealed record Mode(ZoomMode Value) : ZoomSpec;
}

/// <summary>An alignment on one axis: a named edge or a fraction from 0 (start) to 1 (end).</summary>
public abstract record StageAlignValue
{
    private protected StageAlignValue() { }

    /// <summary>A named edge.</summary>
    public sealed record Named(StageAlign Align) : StageAlignValue;

    /// <summary>A fraction from 0 to 1.</summary>
    public sealed record Fraction(double Value) : StageAlignValue;
}

/// <summary>Where an item lands on arrival on one axis, or the current offset kept.</summary>
public abstract record StageArrivalAlignValue
{
    private protected StageArrivalAlignValue() { }

    /// <summary>Aligned.</summary>
    public sealed record Align(StageAlignValue Value) : StageArrivalAlignValue;

    /// <summary>The current offset on this axis.</summary>
    public sealed record Keep : StageArrivalAlignValue;
}

/// <summary>The space between items.</summary>
public abstract record StageGap
{
    private protected StageGap() { }

    /// <summary>In world units: it scales with zoom.</summary>
    public sealed record World(double Units) : StageGap;

    /// <summary>In viewport points: it stays put.</summary>
    public sealed record Px(double Value) : StageGap;
}

/// <summary>Grid columns.</summary>
public abstract record StageGridColumns
{
    private protected StageGridColumns() { }

    /// <summary>A fixed count.</summary>
    public sealed record Count(int Value) : StageGridColumns;

    /// <summary>As close to square as the page count allows.</summary>
    public sealed record Square : StageGridColumns;

    /// <summary>As many as fit.</summary>
    public sealed record Auto : StageGridColumns;
}

/// <summary>Where a revealed rectangle lands on one axis.</summary>
public abstract record StageRevealAnchorValue
{
    private protected StageRevealAnchorValue() { }

    /// <summary>A named position.</summary>
    public sealed record Word(StageRevealWord Value) : StageRevealAnchorValue;

    /// <summary>A fraction of the viewport from 0 to 1.</summary>
    public sealed record Fraction(double Value) : StageRevealAnchorValue;
}

/// <summary>The zoom a reveal ends at.</summary>
public abstract record StageRevealZoom
{
    private protected StageRevealZoom() { }

    /// <summary>A zoom level.</summary>
    public sealed record Level(double Value) : StageRevealZoom;

    /// <summary>The current zoom.</summary>
    public sealed record Keep : StageRevealZoom;

    /// <summary>Fit the rectangle.</summary>
    public sealed record Fit : StageRevealZoom;

    /// <summary>Fit the rectangle's width.</summary>
    public sealed record FitWidth : StageRevealZoom;

    /// <summary>Fit the rectangle's height.</summary>
    public sealed record FitHeight : StageRevealZoom;
}

/// <summary>How the current zoom was reached: a named fit that refits on resize, or a custom level.</summary>
public abstract record StageZoomMode
{
    private protected StageZoomMode() { }

    /// <summary>A named fit.</summary>
    public sealed record Mode(ZoomMode Value) : StageZoomMode;

    /// <summary>A level set by hand.</summary>
    public sealed record Custom : StageZoomMode;
}

/// <summary>Where <see cref="StageCapability.ZoomToAsync"/> goes.</summary>
public abstract record StageZoomTarget
{
    private protected StageZoomTarget() { }

    /// <summary>A zoom level.</summary>
    public sealed record Level(double Value) : StageZoomTarget;

    /// <summary>A zoom spec.</summary>
    public sealed record Spec(ZoomSpec Value) : StageZoomTarget;
}

/// <summary>An alignment on both axes, by named edge.</summary>
public sealed record StageAlignment(StageAlign X, StageAlign Y);

/// <summary>An alignment on both axes.</summary>
public sealed record StageAlignmentValue(StageAlignValue X, StageAlignValue Y);

/// <summary>Where an item lands on arrival, on both axes.</summary>
public sealed record StageArrivalAlignment(StageArrivalAlignValue X, StageArrivalAlignValue Y);

/// <summary>An arrival alignment with either axis left to the settings.</summary>
public sealed record StagePartialArrivalAlignment
{
    /// <summary>The horizontal alignment, or the settings'.</summary>
    public StageArrivalAlignValue? X { get; init; }

    /// <summary>The vertical alignment, or the settings'.</summary>
    public StageArrivalAlignValue? Y { get; init; }
}

/// <summary>How the zoom tool's box is drawn, as CSS colors.</summary>
public sealed record StageZoomBoxLook
{
    /// <summary>The fill.</summary>
    public string? Fill { get; init; }

    /// <summary>The outline.</summary>
    public string? Stroke { get; init; }
}

/// <summary>The stage's settings.</summary>
public sealed record StageSettings(
    StageFlow Flow, StageLayout Layout, StageSpreadMode Spread, StageSizingMode Sizing, StageGridColumns Columns,
    bool Bounded, double Padding, StageGap Gap, PageFrameInsets PageFrame, StageDirection Direction,
    StageAlignment FitAlign, StageArrivalAlignment ArrivalAlign, StageAlignmentValue ZoomAlign, StageAlignmentValue AnchorAlign,
    double ViewRotation, ZoomSpec Zoom, double MinZoom, double MaxZoom, StageScrollBehavior ScrollBehavior,
    bool Interaction, bool PanFallback, bool ZoomGestures, bool TurnGestures, bool ScrollChaining,
    StageZoomBoxLook ZoomBox, double ViewUnitsPerPoint);

/// <summary>A change to <see cref="StageSettings"/>; a <see langword="null"/> field keeps its value.</summary>
public sealed record StageSettingsPatch
{
#pragma warning disable CS1591 // Each property sets the StageSettings property of the same name.
    public StageFlow? Flow { get; init; }
    public StageLayout? Layout { get; init; }
    public StageSpreadMode? Spread { get; init; }
    public StageSizingMode? Sizing { get; init; }
    public StageGridColumns? Columns { get; init; }
    public bool? Bounded { get; init; }
    public double? Padding { get; init; }
    public StageGap? Gap { get; init; }
    public PageFrameInsets? PageFrame { get; init; }
    public StageDirection? Direction { get; init; }
    public StageAlignment? FitAlign { get; init; }
    public StageArrivalAlignment? ArrivalAlign { get; init; }
    public StageAlignmentValue? ZoomAlign { get; init; }
    public StageAlignmentValue? AnchorAlign { get; init; }
    public double? ViewRotation { get; init; }
    public ZoomSpec? Zoom { get; init; }
    public double? MinZoom { get; init; }
    public double? MaxZoom { get; init; }
    public StageScrollBehavior? ScrollBehavior { get; init; }
    public bool? Interaction { get; init; }
    public bool? PanFallback { get; init; }
    public bool? ZoomGestures { get; init; }
    public bool? TurnGestures { get; init; }
    public bool? ScrollChaining { get; init; }
    public StageZoomBoxLook? ZoomBox { get; init; }
    public double? ViewUnitsPerPoint { get; init; }
#pragma warning restore CS1591
}

/// <summary>A viewport size range, for <see cref="StageResponsiveRule"/>. A <see langword="null"/> bound is open.</summary>
public sealed record StageBoxQuery
{
#pragma warning disable CS1591 // Bounds in viewport points.
    public double? MinWidth { get; init; }
    public double? MaxWidth { get; init; }
    public double? MinHeight { get; init; }
    public double? MaxHeight { get; init; }
    public StageOrientation? Orientation { get; init; }
#pragma warning restore CS1591
}

/// <summary>Settings that apply while the viewport matches <paramref name="When"/>.</summary>
/// <param name="When">The viewport range.</param>
public sealed record StageResponsiveRule(StageBoxQuery When)
{
    /// <summary>A name for <see cref="StageCapability.MatchesRuleAsync"/>.</summary>
    public string? Name { get; init; }

    /// <summary>The settings it applies.</summary>
    public StageSettingsPatch? Settings { get; init; }
}

/// <summary>Where to look: an anchor and a zoom.</summary>
public sealed record StageViewpoint(ViewAnchor Anchor, ZoomSpec Zoom);

/// <summary>A saved view: settings, the current item, and the anchor. Restore it with <see cref="StageCapability.ApplyViewStateAsync"/> or an <see cref="IStageInitialViewProvider"/>.</summary>
/// <param name="Settings">The settings.</param>
/// <param name="Cursor">The current item in paged flow.</param>
/// <param name="Anchor">The point at the viewport's anchor.</param>
public sealed record StageViewState(StageSettings Settings, int? Cursor, ViewAnchor Anchor);

/// <summary>How a page move goes.</summary>
public sealed record StageGoToOptions
{
    /// <summary>Animated or a jump; <see langword="null"/> is the settings'.</summary>
    public StageScrollBehavior? Behavior { get; init; }

    /// <summary>Where to land, instead of the arrival alignment.</summary>
    public StageViewpoint? Viewpoint { get; init; }

    /// <summary>The arrival alignment, axis by axis.</summary>
    public StagePartialArrivalAlignment? ArrivalAlign { get; init; }
}

/// <summary>A scroll position or delta in viewport points; a <see langword="null"/> axis stays.</summary>
public sealed record StageScrollToOptions
{
    /// <summary>Horizontal.</summary>
    public double? Left { get; init; }

    /// <summary>Vertical.</summary>
    public double? Top { get; init; }

    /// <summary>Animated or a jump.</summary>
    public StageScrollBehavior? Behavior { get; init; }
}

/// <summary>Where a revealed rectangle lands, axis by axis.</summary>
public sealed record StageRevealAnchor
{
    /// <summary>Horizontal.</summary>
    public StageRevealAnchorValue? X { get; init; }

    /// <summary>Vertical.</summary>
    public StageRevealAnchorValue? Y { get; init; }
}

/// <summary>How <see cref="StageCapability.RevealAsync"/> shows a page or a part of it.</summary>
public sealed record StageRevealOptions
{
    /// <summary>Animated or a jump.</summary>
    public StageScrollBehavior? Behavior { get; init; }

    /// <summary>The part of the page, in page space; <see langword="null"/> is the whole page.</summary>
    public PdfRect? Rect { get; init; }

    /// <summary>The zoom to end at.</summary>
    public StageRevealZoom? Zoom { get; init; }

    /// <summary>Where it lands.</summary>
    public StageRevealAnchor? Anchor { get; init; }
}

/// <summary>A page under a viewport point.</summary>
/// <param name="Page">The page.</param>
/// <param name="Point">The point in page space.</param>
/// <param name="Scale">Viewport points per page point.</param>
/// <param name="Rotation">The page's rotation on screen, in degrees.</param>
/// <param name="Zoom">The zoom level.</param>
public sealed record StagePageHit(PageRef Page, PdfPoint Point, double Scale, double Rotation, double Zoom);

/// <summary>How a page maps to the screen.</summary>
public sealed record StagePageTransform(
    double Rotation, double ViewWidth, double ViewHeight, double ContentWidth, double ContentHeight,
    double DeviceWidth, double DeviceHeight, double RenderScale, double ViewScale, double BaseScale, double Zoom);

/// <summary>A laid-out page: its world frame, its viewport position and what of it shows.</summary>
public sealed record StageVisiblePage(
    PageRef Page, int PageIndex, double X, double Y, double Width, double Height, double Rotation, double ContentScale,
    double ScreenX, double ScreenY, StagePageTransform Transform, PdfRect VisibleRect);

/// <summary>Something the stage did.</summary>
public abstract record StageEvent
{
    private protected StageEvent() { }

    /// <summary>The current page changed.</summary>
    public sealed record PageChanged(PageRef? Page, int PageIndex, int PreviousPageIndex) : StageEvent;

    /// <summary>The zoom changed.</summary>
    public sealed record ZoomChanged(double Level, double PreviousLevel, StageZoomMode Mode) : StageEvent;

    /// <summary>The camera moved.</summary>
    public sealed record CameraChanged(Camera Camera) : StageEvent;

    /// <summary>A tween or a fling came to rest.</summary>
    public sealed record MotionEnded(Camera Camera) : StageEvent;

    /// <summary>Settings changed.</summary>
    public sealed record SettingsChanged(StageSettings Settings, IReadOnlyList<StageSettingKey> Changed) : StageEvent;

    /// <summary>The viewport was resized.</summary>
    public sealed record ViewportChanged(PdfSize Size) : StageEvent;
}

/// <summary>
/// Answers the view a document opens at, the first time the stage places it.
/// Called on the viewer's kernel thread, synchronously: answer from state
/// you already have. Disposing the viewer there throws
/// <see cref="InvalidOperationException"/>; whatever else the provider
/// throws is the kernel's host error, and the stage places the document as
/// if no provider had answered.
/// </summary>
public interface IStageInitialViewProvider
{
    /// <summary>The view to open at, or <see langword="null"/> to leave it to the stage.</summary>
    StageViewState? InitialView();
}
