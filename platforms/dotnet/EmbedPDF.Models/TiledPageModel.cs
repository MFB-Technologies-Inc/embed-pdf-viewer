using System.Diagnostics;

namespace EmbedPDF.Models;

/// <summary>An owned tile image positioned in unrotated page points.</summary>
/// <remarks>Equality compares identity, rectangle and stacking order, not pixels.</remarks>
/// <param name="Id">Opaque raster-source key used for display acknowledgments.</param>
/// <param name="Rect">Placement rectangle in unrotated page points with a top-left origin.</param>
/// <param name="Z">Stacking order; larger values draw above coarser tiles.</param>
/// <param name="Image">The tile's pixels, owned independently of the document.</param>
public sealed record PageTile(string Id, PdfRect Rect, double Z, PixelBuffer Image)
{
    /// <summary>Compares identity and placement without comparing pixels.</summary>
    public bool Equals(PageTile? other) =>
        other is not null && Id == other.Id && Rect == other.Rect && Z.Equals(other.Z);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Rect, Z);
}

/// <summary>Raster state for one page with a budgeted base and detail tiles.</summary>
/// <remarks>
/// <para>
/// Keep one model per page, sharing a <see cref="RenderViewDemand"/> across
/// the pages of one reader surface. Demand, visibility and tile rectangles
/// all use the page's unrotated point space, with x rightward and y
/// downward. Rotate the composed view as a whole when showing a rotated page.
/// </para>
/// <para>
/// <see cref="UpdateAsync"/> polls for tiles rather than subscribing to
/// render events. The host must ask for an update after pixel changes.
/// Errors are suppressed; a missing base or incomplete tiles have no
/// diagnostic error property. Images are <see cref="PixelBuffer"/>s, which a
/// view copies into its framework's bitmap.
/// </para>
/// </remarks>
public sealed class TiledPageModel : ObservableModel
{
    /// <summary>Between polls while tiles are in flight. The controller re-plans an arrival on its own about 16 ms later.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(32);
    /// <summary>How long the plan must stay unchanged, with nothing in flight, to count as settled. Longer than the tile plane's default settle of 150 ms.</summary>
    internal static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(250);
    /// <summary>The longest an update keeps polling, so a plan that never fills can't poll forever.</summary>
    internal static readonly TimeSpan PollLimit = TimeSpan.FromSeconds(5);

    readonly RenderCapability render;
    readonly RenderViewDemand demand;
    readonly Dictionary<string, PixelBuffer> images = [];
    readonly HashSet<string> painted = [];
    readonly PageRenderTask renderWork = new();
    PixelBuffer? baseImage;
    IReadOnlyList<PageTile> tiles = [];
    string? baseKey;
    string? lastStamp;
    int demandGeneration;

    /// <summary>Creates a tiled raster model for one page in a reader surface.</summary>
    /// <param name="render">Render capability for the page's open document. The model does not dispose it.</param>
    /// <param name="demand">
    /// Surface handle from <see cref="RenderCapability.CreateViewDemandAsync"/>,
    /// shared across this surface's pages. The owner disposes it when done;
    /// the model does not, and does not close the document.
    /// </param>
    /// <param name="page">Page belonging to the renderer's document.</param>
    /// <param name="includeAnnotations">Whether both raster planes include annotations.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public TiledPageModel(
        RenderCapability render, RenderViewDemand demand, PageRef page, bool includeAnnotations = true,
        SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(demand);
        this.render = render;
        this.demand = demand;
        Page = page;
        IncludeAnnotations = includeAnnotations;
    }

    /// <summary>Budgeted whole-page raster, <c>null</c> at first; kept by <see cref="ReleaseAsync"/>.</summary>
    public PixelBuffer? Base { get => baseImage; private set => Set(ref baseImage, value); }

    /// <summary>The current plan's available tile images in paint order; empty at first.</summary>
    public IReadOnlyList<PageTile> Tiles { get => tiles; private set => SetList(ref tiles, value); }

    /// <summary>The page this model serves for its whole lifetime.</summary>
    public PageRef Page { get; }

    /// <summary>Whether both raster planes include annotations.</summary>
    public bool IncludeAnnotations { get; }

    ContentInclusion Annotations => IncludeAnnotations ? ContentInclusion.Always : ContentInclusion.Never;

    /// <summary>Declares raster demand, refreshes the base, and polls for detail tiles.</summary>
    /// <remarks>
    /// Polls every 32 ms until the plan stays quiet for 250 ms,
    /// <paramref name="cancellationToken"/> is cancelled, a plan request fails,
    /// or five seconds pass. Returns without throwing and suppresses
    /// capability failures; returning does not promise every requested pixel
    /// exists. Keeps an earlier image if refreshing fails. A new update
    /// cancels and waits for the previous work before declaring its demand.
    /// </remarks>
    /// <param name="deviceScale">Positive device pixels per page point at the current zoom.</param>
    /// <param name="unrotatedWidth">Positive page width in points, without its rotation.</param>
    /// <param name="visibleRect">
    /// The visible region in unrotated page points, y down; <c>null</c> is the
    /// whole page and a zero-area rect is a page built but off screen.
    /// </param>
    /// <param name="onPainted">
    /// Runs on the home context at most once, after the base is current and
    /// every tile in a plan that is not waiting has pixels. It can run for a
    /// base-only plan. It acknowledges available pixels, not display, and may
    /// never run on error, cancellation or timeout.
    /// </param>
    /// <param name="cancellationToken">Cancels this update's work.</param>
    public async Task UpdateAsync(
        double deviceScale, double unrotatedWidth, PdfRect? visibleRect, Action? onPainted = null,
        CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        demandGeneration++;
        await renderWork.RunAsync(token => UpdateDemandAsync(deviceScale, unrotatedWidth, visibleRect, onPainted, token), cancellationToken);
    }

    /// <summary>
    /// Acknowledges that a tile has appeared so coarser covering tiles can
    /// retire. Call it after the view presents the tile. Ignores unknown and
    /// already acknowledged keys; suppresses capability errors and starts
    /// background polling.
    /// </summary>
    /// <param name="key">A key from <see cref="PageTile.Id"/> this model holds.</param>
    public async Task TileDisplayedAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        await Home.SwitchTo();
        if (painted.Contains(key) || !images.ContainsKey(key)) return;
        var generation = demandGeneration;
        painted.Add(key);
        try { await demand.MarkPaintedAsync(Page, key); }
        catch (PdfException) { }
        if (generation != demandGeneration || !images.ContainsKey(key)) return;
        renderWork.StartIfIdle(async token =>
        {
            if (generation != demandGeneration) return;
            await PollUntilSettledAsync(null, token);
        });
    }

    /// <summary>
    /// Releases this page's tile demand and clears local tile images. Cancels
    /// and joins earlier page work first, so an in-flight response cannot
    /// repopulate the retired tiles. The base raster stays; drop the model to
    /// release it. Dispose the shared demand separately after all pages stop
    /// using it.
    /// </summary>
    public async Task ReleaseAsync()
    {
        await Home.SwitchTo();
        demandGeneration++;
        await renderWork.RunAsync(async _ =>
        {
            Tiles = [];
            images.Clear();
            painted.Clear();
            lastStamp = null;
            try { await demand.ReleaseAsync(Page); }
            catch (PdfException) { }
        });
    }

    async Task UpdateDemandAsync(double deviceScale, double unrotatedWidth, PdfRect? visibleRect, Action? onPainted, CancellationToken token)
    {
        try
        {
            await demand.SetDemandAsync(Page, new RenderPageViewDemand(deviceScale * unrotatedWidth) { VisibleRect = visibleRect }, Annotations);
            if (token.IsCancellationRequested) return;
            var key = await render.GetSourceKeyAsync(Page, deviceScale, Annotations);
            if (token.IsCancellationRequested) return;
            if (key != baseKey)
            {
                var rendered = await render.RenderSourceAsync(Page, deviceScale, Annotations);
                if (token.IsCancellationRequested) return;
                Base = rendered.Image;
                baseKey = key;
            }
        }
        catch (PdfException)
        {
            return;
        }
        await PollUntilSettledAsync(onPainted, token);
    }

    async Task PollUntilSettledAsync(Action? onPainted, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var quietSince = clock.Elapsed;
        while (!token.IsCancellationRequested && clock.Elapsed < PollLimit)
        {
            RenderTilePlan plan;
            try { plan = await demand.GetPlanAsync(Page); }
            catch (PdfException) { return; }
            if (token.IsCancellationRequested) return;
            var changed = plan.Stamp != lastStamp;
            lastStamp = plan.Stamp;
            await ReconcileAsync(plan, token);
            if (token.IsCancellationRequested) return;
            var waiting = plan.Fetching.Count > 0 || (plan.Engaged && plan.Paint.Count == 0);
            if (!waiting && plan.Paint.All(source => images.ContainsKey(source.Key)))
            {
                onPainted?.Invoke();
                onPainted = null;
            }
            if (changed || waiting) quietSince = clock.Elapsed;
            else if (clock.Elapsed - quietSince >= QuietPeriod) return;
            try { await Task.Delay(PollInterval, token); }
            catch (OperationCanceledException) { return; }
        }
    }

    async Task ReconcileAsync(RenderTilePlan plan, CancellationToken token)
    {
        foreach (var source in plan.Paint)
        {
            if (images.ContainsKey(source.Key)) continue;
            RenderedPage? tile;
            try { tile = await demand.GetTileImageAsync(Page, source.Key); }
            catch (PdfException) { continue; }
            if (tile is null) continue;
            if (token.IsCancellationRequested) return;
            images[source.Key] = tile.Image;
        }
        var wanted = plan.Paint.Select(source => source.Key).ToHashSet();
        foreach (var key in images.Keys.Where(key => !wanted.Contains(key)).ToList())
        {
            images.Remove(key);
            if (painted.Remove(key))
            {
                try { await demand.MarkUnpaintedAsync(Page, key); }
                catch (PdfException) { }
            }
        }
        Tiles = plan.Paint
            .Where(source => images.ContainsKey(source.Key))
            .Select(source => new PageTile(source.Key, source.Rect, source.Z, images[source.Key]))
            .ToList();
    }
}
