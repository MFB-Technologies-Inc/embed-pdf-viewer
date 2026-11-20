namespace EmbedPDF;

/// <summary>A rendered page. <see cref="Image"/> owns its pixels: it outlives the document and the viewer.</summary>
/// <param name="Width">Pixels a row.</param>
/// <param name="Height">Rows.</param>
/// <param name="Scale">Device pixels per PDF point across the rendered region's rotated width.</param>
/// <param name="Image">The pixels: <see cref="PixelFormat.Rgba8"/>, straight alpha, rows <see cref="PixelBuffer.Stride"/> bytes apart.</param>
public sealed record RenderedPage(int Width, int Height, double Scale, PixelBuffer Image);

/// <summary>One page of a batch.</summary>
/// <param name="Page">The page.</param>
/// <param name="Rendered">Its render.</param>
public sealed record RenderedPageResult(PageRef Page, RenderedPage Rendered);

/// <summary>A page a batch could not render.</summary>
/// <param name="Page">The page.</param>
/// <param name="Error">Why.</param>
public sealed record RenderBatchFailure(PageRef Page, PluginErrorInfo Error);

/// <summary>A batch, best effort per page, each list in input order.</summary>
/// <param name="Applied">The pages that rendered.</param>
/// <param name="Failed">The pages that did not.</param>
public sealed record RenderBatchResult(IReadOnlyList<RenderedPageResult> Applied, IReadOnlyList<RenderBatchFailure> Failed);

/// <summary>What <see cref="RenderCapability.RenderPageAsync"/> renders. Leave every size out for 1 device pixel a point.</summary>
public sealed record RenderPageOptions
{
    /// <summary>Page-space crop, clipped to the page bounds; <see langword="null"/> is the whole page.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary>Absolute clockwise rotation; <see langword="null"/> is unrotated page space.</summary>
    public RenderRotation? Rotation { get; init; }
    /// <summary>Exact output width in device pixels; the height keeps the aspect.</summary>
    public double? Width { get; init; }
    /// <summary>Device pixels a point, when <see cref="Width"/> is left out.</summary>
    public double? Scale { get; init; }
    /// <summary>Whether annotations draw; see <see cref="RenderCapability"/> for the default rule.</summary>
    public ContentInclusion IncludeAnnotations { get; init; }
    /// <summary>Whether form fields draw; see <see cref="RenderCapability"/> for the default rule.</summary>
    public ContentInclusion IncludeFormFields { get; init; }
}

/// <summary>Which layer of a page's pixels changed.</summary>
public enum RenderInvalidateScope
{
    /// <summary>The page content, and so everything drawn over it.</summary>
    Content,
    /// <summary>Annotations.</summary>
    Annotations,
    /// <summary>Form fields.</summary>
    Fields,
}

/// <summary>An event from <see cref="RenderCapability.ObserveEvents"/>.</summary>
public abstract record RenderEvent
{
    private protected RenderEvent() { }

    /// <summary>Pages' pixels changed.</summary>
    /// <param name="Pages">The pages.</param>
    /// <param name="Scope">Which layer.</param>
    public sealed record Invalidated(IReadOnlyList<PageRef> Pages, RenderInvalidateScope Scope) : RenderEvent;

    /// <summary>A raster resolved.</summary>
    /// <param name="Page">Its page.</param>
    /// <param name="Key">Its source key.</param>
    public sealed record Completed(PageRef Page, string Key) : RenderEvent;

    /// <summary>A raster failed.</summary>
    /// <param name="Page">Its page.</param>
    /// <param name="Key">Its source key.</param>
    /// <param name="Error">Why.</param>
    public sealed record Failed(PageRef Page, string Key, PluginErrorInfo Error) : RenderEvent;
}

/// <summary>The render points a lattice policy conforms sizes to.</summary>
/// <param name="FullPageWidths">Full-page raster widths.</param>
/// <param name="TileSizes">Tile sizes.</param>
/// <param name="TileScales">Tile scales.</param>
/// <param name="AppearanceScales">Appearance scales.</param>
/// <param name="MaxRenderPixels">The pixel budget of one raster, if any.</param>
/// <param name="Background">The page background, if set.</param>
/// <param name="Enforced">Whether requests off the lattice are refused.</param>
public sealed record RenderLatticePolicy(
    IReadOnlyList<double> FullPageWidths, IReadOnlyList<double> TileSizes, IReadOnlyList<double> TileScales,
    IReadOnlyList<double> AppearanceScales, double? MaxRenderPixels, string? Background, bool? Enforced);

/// <summary>How render sizes are chosen.</summary>
public abstract record RenderPolicy
{
    private protected RenderPolicy() { }

    /// <summary>Any size.</summary>
    public sealed record Continuous : RenderPolicy;

    /// <summary>Sizes conform to fixed render points.</summary>
    /// <param name="Policy">The points.</param>
    public sealed record Lattice(RenderLatticePolicy Policy) : RenderPolicy;
}

/// <summary>A conformed render size, from <see cref="RenderCapability.ConformViewportAsync"/>.</summary>
public abstract record RenderViewport
{
    private protected RenderViewport() { }

    /// <summary>Render at a scale; <see langword="null"/> is the page's natural size.</summary>
    /// <param name="Value">Device pixels a point.</param>
    public sealed record Scale(double? Value) : RenderViewport;

    /// <summary>Render at a width.</summary>
    /// <param name="Value">Device pixels.</param>
    public sealed record Width(double Value) : RenderViewport;

    /// <summary>Use the thumbnail.</summary>
    public sealed record Thumbnail : RenderViewport;
}

/// <summary>Paint settings a view reads.</summary>
/// <param name="Tiles">Whether the tile plane is on.</param>
public sealed record RenderPaintSettings(bool Tiles);

/// <summary>What one view wants for a page right now.</summary>
/// <param name="DesiredDeviceWidth">The page's width on screen, in device pixels.</param>
public sealed record RenderPageViewDemand(double DesiredDeviceWidth)
{
    /// <summary>The visible part of the page, in page points; <see langword="null"/> is all of it.</summary>
    public PdfRect? VisibleRect { get; init; }
    /// <summary>Horizontal scroll velocity, for prefetch bias.</summary>
    public double? VelocityX { get; init; }
    /// <summary>Vertical scroll velocity, for prefetch bias.</summary>
    public double? VelocityY { get; init; }
}

/// <summary>One raster a view paints.</summary>
/// <param name="Key">Its source key.</param>
/// <param name="Scale">Device pixels a point.</param>
/// <param name="Rect">Placement, y-down page points.</param>
/// <param name="Z">Stacking: coarse under fine.</param>
public sealed record RenderTileSource(string Key, double Scale, PdfRect Rect, double Z);

/// <summary>A view's plan for a page.</summary>
/// <param name="Engaged">Whether the tile plane is in use for the page.</param>
/// <param name="Paint">What to paint, coarse first.</param>
/// <param name="Fetching">Keys still rendering.</param>
/// <param name="Stamp">Changes whenever the plan does.</param>
public sealed record RenderTilePlan(bool Engaged, IReadOnlyList<RenderTileSource> Paint, IReadOnlyList<string> Fetching, string Stamp);

/// <summary>How requested widths snap to render points.</summary>
public abstract record RenderQuantize
{
    private protected RenderQuantize() { }

    /// <summary>No snapping.</summary>
    public sealed record Exact : RenderQuantize;

    /// <summary>Snap to the next of these widths.</summary>
    /// <param name="Widths">The ladder.</param>
    public sealed record Ladder(IReadOnlyList<double> Widths) : RenderQuantize;
}

/// <summary>Full-page raster options; <see langword="null"/> takes the engine's default.</summary>
public sealed record RenderFullPageOptions
{
    /// <summary>The widest full-page raster; <see langword="null"/> is 640 device pixels.</summary>
    public double? MaxWidth { get; init; }
    /// <summary>Width snapping; <see langword="null"/> is <see cref="RenderQuantize.Exact"/>.</summary>
    public RenderQuantize? Quantize { get; init; }
}

/// <summary>Tile prefetch options.</summary>
public sealed record RenderPrefetchOptions
{
    /// <summary>How far past the viewport to prefetch.</summary>
    public double? Margin { get; init; }
    /// <summary>Whether to prefetch further in the scroll direction.</summary>
    public bool? VelocityBias { get; init; }
}

/// <summary>Tile plane settings; <see langword="null"/> takes the engine's default.</summary>
public sealed record RenderTilesSettings
{
    /// <summary>Tile size in device pixels.</summary>
    public double? Size { get; init; }
    /// <summary>Scale snapping.</summary>
    public RenderQuantize? Quantize { get; init; }
    /// <summary>The finest tile scale.</summary>
    public double? MaxScale { get; init; }
    /// <summary>Bleed around each tile, in pixels.</summary>
    public double? BleedPx { get; init; }
    /// <summary>The zoom past the full-page raster at which tiles engage.</summary>
    public double? EngageRatio { get; init; }
    /// <summary>Prefetch.</summary>
    public RenderPrefetchOptions? Prefetch { get; init; }
    /// <summary>How long a view must settle before tiles render.</summary>
    public double? SettleMs { get; init; }
}

/// <summary>A change to the tile plane's settings; <see langword="null"/> keeps a value.</summary>
public sealed record RenderTilesOptions
{
    /// <summary>New tile size.</summary>
    public double? Size { get; init; }
    /// <summary>New scale snapping.</summary>
    public RenderQuantize? Quantize { get; init; }
    /// <summary>New finest scale.</summary>
    public double? MaxScale { get; init; }
    /// <summary>New bleed.</summary>
    public double? BleedPx { get; init; }
    /// <summary>New engage ratio.</summary>
    public double? EngageRatio { get; init; }
    /// <summary>Remove the engage ratio.</summary>
    public bool ClearEngageRatio { get; init; }
    /// <summary>New prefetch.</summary>
    public RenderPrefetchOptions? Prefetch { get; init; }
    /// <summary>New settle time.</summary>
    public double? SettleMs { get; init; }
}

/// <summary>Whether the tile plane is on, and its settings.</summary>
public abstract record RenderTilesSetting
{
    private protected RenderTilesSetting() { }

    /// <summary>Off.</summary>
    public sealed record Off : RenderTilesSetting;

    /// <summary>On.</summary>
    /// <param name="Options">Its settings.</param>
    public sealed record On(RenderTilesSettings Options) : RenderTilesSetting;
}

/// <summary>A change to the tile plane.</summary>
public abstract record RenderTilesPatch
{
    private protected RenderTilesPatch() { }

    /// <summary>Turn it off.</summary>
    public sealed record Off : RenderTilesPatch;

    /// <summary>Turn it on, merging these settings.</summary>
    /// <param name="Options">The changes.</param>
    public sealed record On(RenderTilesOptions Options) : RenderTilesPatch;
}

/// <summary>A stored encoding preference. This host returns raw pixels and provides no encoder.</summary>
public enum RenderEncodingFormat
{
    /// <summary>PNG.</summary>
    Png,
    /// <summary>WebP.</summary>
    Webp,
    /// <summary>BMP.</summary>
    Bmp,
}

/// <summary>The snapshot tool's box colors; <see langword="null"/> is the accent.</summary>
public sealed record RenderSnapshotBox
{
    /// <summary>Fill color.</summary>
    public string? Fill { get; init; }
    /// <summary>Stroke color.</summary>
    public string? Stroke { get; init; }
}

/// <summary>Snapshot settings.</summary>
/// <param name="Scale">Device pixels a point in a capture.</param>
/// <param name="BoxLook">The capture box's colors.</param>
public sealed record RenderSnapshotSettings(double Scale, RenderSnapshotBox BoxLook);

/// <summary>A change to <see cref="RenderSnapshotSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record RenderSnapshotPatch
{
    /// <summary>New scale.</summary>
    public double? Scale { get; init; }
    /// <summary>A whole value: colors left out of a supplied box clear to the accent.</summary>
    public RenderSnapshotBox? BoxLook { get; init; }
}

/// <summary>Render settings, shared by every document using the plugin registration.</summary>
/// <param name="FullPage">Full-page raster options.</param>
/// <param name="Tiles">The tile plane.</param>
/// <param name="Format">Stored encoding preference; native page images stay raw pixels.</param>
/// <param name="Quality">Stored encoded-image quality; it does not change raw pixels.</param>
/// <param name="SlowAfterMs">When a render counts as slow.</param>
/// <param name="Debug">Whether tile scheduling logs.</param>
/// <param name="Snapshot">Snapshot settings.</param>
public sealed record RenderSettings(
    RenderFullPageOptions FullPage, RenderTilesSetting Tiles, RenderEncodingFormat? Format, double? Quality,
    double SlowAfterMs, bool Debug, RenderSnapshotSettings Snapshot);

/// <summary>A change to <see cref="RenderSettings"/>. Clear flags remove nullable values; omission preserves them.</summary>
public sealed record RenderSettingsPatch
{
    /// <summary>New full-page options.</summary>
    public RenderFullPageOptions? FullPage { get; init; }
    /// <summary>Tile plane change.</summary>
    public RenderTilesPatch? Tiles { get; init; }
    /// <summary>New encoding preference.</summary>
    public RenderEncodingFormat? Format { get; init; }
    /// <summary>Remove the encoding preference.</summary>
    public bool ClearFormat { get; init; }
    /// <summary>New quality.</summary>
    public double? Quality { get; init; }
    /// <summary>Remove the quality.</summary>
    public bool ClearQuality { get; init; }
    /// <summary>New slow threshold.</summary>
    public double? SlowAfterMs { get; init; }
    /// <summary>New debug flag.</summary>
    public bool? Debug { get; init; }
    /// <summary>Snapshot changes.</summary>
    public RenderSnapshotPatch? Snapshot { get; init; }
}

/// <summary>Installs the render plugin: page rasters as owned pixels.</summary>
public sealed partial record RenderConfig
{
    /// <summary>Full-page raster options; <see langword="null"/> is the engine's defaults.</summary>
    public RenderFullPageOptions? FullPage { get; init; }
    /// <summary>Log tile scheduling to standard error.</summary>
    public bool? Debug { get; init; }
}
