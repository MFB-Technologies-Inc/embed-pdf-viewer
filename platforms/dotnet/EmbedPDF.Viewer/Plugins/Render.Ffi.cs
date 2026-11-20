using GR = uniffi.epdf_plugin_render;

namespace EmbedPDF;

internal static partial class Ffi
{
    /// <summary>Wraps the engine's owned pixels without copying; the array is the engine's copy already.</summary>
    internal static RenderedPage ToPublic(GR.RenderedImage image)
    {
        var width = WireInteger.ToInt(image.Width, "width");
        var height = WireInteger.ToInt(image.Height, "height");
        var stride = WireInteger.ToInt(image.Stride, "stride");
        PixelBuffer pixels;
        try
        {
            pixels = new PixelBuffer(image.Pixels, width, height, stride, PixelFormat.Rgba8, isPremultiplied: false);
        }
        catch (ArgumentException error)
        {
            throw new PdfException("InvalidState", PdfErrorCode.InvalidState, $"the engine answered a malformed {width}x{height} raster: {error.Message}");
        }
        return new RenderedPage(width, height, image.Scale, pixels);
    }

    internal static GR.RenderPageOptions ToFfi(RenderPageOptions o) =>
        new(o.Rect is { } rect ? ToFfi(rect) : null, o.Rotation is { } r ? (GR.RenderRotation)(int)r : null, o.Width, o.Scale,
            o.IncludeAnnotations.ToEngine(), o.IncludeFormFields.ToEngine());

    internal static RenderQuantize ToPublic(GR.RenderQuantize q) => q switch
    {
        GR.RenderQuantize.Exact => new RenderQuantize.Exact(),
        GR.RenderQuantize.Ladder l => new RenderQuantize.Ladder(l.Widths),
        _ => throw Unknown(q),
    };

    internal static GR.RenderQuantize ToFfi(RenderQuantize q) => q switch
    {
        RenderQuantize.Exact => new GR.RenderQuantize.Exact(),
        RenderQuantize.Ladder l => new GR.RenderQuantize.Ladder([.. l.Widths]),
        _ => throw Unknown(q),
    };

    internal static RenderFullPageOptions ToPublic(GR.RenderFullPageOptions o) =>
        new() { MaxWidth = o.MaxWidth, Quantize = o.Quantize is null ? null : ToPublic(o.Quantize) };

    internal static GR.RenderFullPageOptions ToFfi(RenderFullPageOptions o) =>
        new(o.MaxWidth, o.Quantize is null ? null : ToFfi(o.Quantize));

    static RenderPrefetchOptions? ToPublicOrNull(GR.RenderPrefetchOptions? o) =>
        o is null ? null : new() { Margin = o.Margin, VelocityBias = o.VelocityBias };

    static GR.RenderPrefetchOptions? ToFfiOrNull(RenderPrefetchOptions? o) => o is null ? null : new(o.Margin, o.VelocityBias);

    internal static RenderTilesSetting ToPublic(GR.RenderTilesSetting t) => t switch
    {
        GR.RenderTilesSetting.Off => new RenderTilesSetting.Off(),
        GR.RenderTilesSetting.On on => new RenderTilesSetting.On(new RenderTilesSettings
        {
            Size = on.Options.Size,
            Quantize = on.Options.Quantize is null ? null : ToPublic(on.Options.Quantize),
            MaxScale = on.Options.MaxScale,
            BleedPx = on.Options.BleedPx,
            EngageRatio = on.Options.EngageRatio,
            Prefetch = ToPublicOrNull(on.Options.Prefetch),
            SettleMs = on.Options.SettleMs,
        }),
        _ => throw Unknown(t),
    };

    internal static GR.RenderTilesPatch ToFfi(RenderTilesPatch t) => t switch
    {
        RenderTilesPatch.Off => new GR.RenderTilesPatch.Off(),
        RenderTilesPatch.On on => new GR.RenderTilesPatch.On(new GR.RenderTilesOptions(
            on.Options.Size, on.Options.Quantize is null ? null : ToFfi(on.Options.Quantize), on.Options.MaxScale, on.Options.BleedPx,
            on.Options.EngageRatio, on.Options.ClearEngageRatio, ToFfiOrNull(on.Options.Prefetch), on.Options.SettleMs)),
        _ => throw Unknown(t),
    };

    static RenderSnapshotBox ToPublic(GR.RenderSnapshotBox box) => new() { Fill = box.Fill, Stroke = box.Stroke };

    internal static RenderSettings ToPublic(GR.RenderSettings s) =>
        new(ToPublic(s.FullPage), ToPublic(s.Tiles), s.Format is { } f ? (RenderEncodingFormat)(int)f : null, s.Quality,
            s.SlowAfterMs, s.Debug, new RenderSnapshotSettings(s.Snapshot.Scale, ToPublic(s.Snapshot.BoxLook)));

    internal static GR.RenderSettingsPatch ToFfi(RenderSettingsPatch p) =>
        new(p.FullPage is null ? null : ToFfi(p.FullPage), p.Tiles is null ? null : ToFfi(p.Tiles),
            p.Format is { } f ? (GR.RenderEncodingFormat)(int)f : null, p.ClearFormat, p.Quality, p.ClearQuality, p.SlowAfterMs, p.Debug,
            p.Snapshot is { } s ? new GR.RenderSnapshotPatch(s.Scale, s.BoxLook is { } b ? new GR.RenderSnapshotBox(b.Fill, b.Stroke) : null) : null);

    internal static RenderPolicy ToPublic(GR.RenderPolicy p) => p switch
    {
        GR.RenderPolicy.Continuous => new RenderPolicy.Continuous(),
        GR.RenderPolicy.Lattice l => new RenderPolicy.Lattice(new RenderLatticePolicy(
            l.LatticeValue.FullPageWidths, l.LatticeValue.TileSizes, l.LatticeValue.TileScales, l.LatticeValue.AppearanceScales,
            l.LatticeValue.MaxRenderPixels, l.LatticeValue.Background, l.LatticeValue.Enforced)),
        _ => throw Unknown(p),
    };

    internal static RenderViewport ToPublic(GR.RenderViewport v) => v switch
    {
        GR.RenderViewport.Scale s => new RenderViewport.Scale(s.ScaleValue),
        GR.RenderViewport.Width w => new RenderViewport.Width(w.WidthValue),
        GR.RenderViewport.Thumbnail => new RenderViewport.Thumbnail(),
        _ => throw Unknown(v),
    };

    internal static GR.RenderPageViewDemand ToFfi(RenderPageViewDemand d) =>
        new(d.DesiredDeviceWidth, d.VisibleRect is { } r ? ToFfi(r) : null, d.VelocityX, d.VelocityY);

    internal static RenderTilePlan ToPublic(GR.RenderTilePlan p) =>
        new(p.Engaged, [.. p.Paint.Select(t => new RenderTileSource(t.Key, t.Scale, ToPublic(t.Rect), t.Z))], p.Fetching, p.Stamp);

    internal static RenderEvent ToPublic(GR.RenderEvent e) => e switch
    {
        GR.RenderEvent.Invalidated i => new RenderEvent.Invalidated(ToPublic(i.Pages), (RenderInvalidateScope)(int)i.Scope),
        GR.RenderEvent.Completed c => new RenderEvent.Completed(ToPublic(c.Page), c.Key),
        GR.RenderEvent.Failed f => new RenderEvent.Failed(ToPublic(f.Page), f.Key, ToPublic(f.Error)),
        _ => throw Unknown(e),
    };
}
