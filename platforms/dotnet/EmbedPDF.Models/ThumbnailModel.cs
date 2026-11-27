using System.ComponentModel;

namespace EmbedPDF.Models;

/// <summary>
/// An observable model of a document's page thumbnails, which a view reads,
/// each turned to the page as shown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LoadAsync"/> renders a page with
/// <see cref="RenderCapability.RenderThumbnailAsync"/>, which takes no size:
/// the engine draws the whole page 512 pixels on its long side, so neither
/// the model nor a view picks the pixel size. The engine's thumbnail is
/// unrotated; the model turns it with <see cref="PageGeometry"/>, using the
/// rotation the given pages report. The pages are a snapshot. After a page
/// edit that rotates, inserts or removes pages, make a new model from the
/// new page list.
/// </para>
/// <para>
/// The model keeps its own <see cref="RenderRevisions"/> over the render
/// capability, so it observes invalidations from construction until the
/// document closes or the model is disposed or collected, with no
/// <c>RunAsync</c>. A thumbnail is cached with the page's render revision
/// at the time its render started. <see cref="GetImage"/> keeps answering
/// with that image after the page is invalidated, so a cell shows the old
/// thumbnail until the next <see cref="LoadAsync"/> replaces it.
/// </para>
/// <para>
/// The cache holds the 128 pages used most recently, where a use is a
/// <see cref="LoadAsync"/> call. A thumbnail is up to 1 MiB of pixels (512
/// by 512 at 4 bytes each), so the cache stays within about 128 MiB however
/// long the document is. A page dropped from the cache has no image until it
/// is loaded again. Nothing loads it by itself: a cell still on screen when
/// its page is dropped, as can happen when two views share one model, shows
/// no image until something calls <see cref="LoadAsync"/> for it.
/// </para>
/// <para>
/// <see cref="Error"/> is the latest failed load. It clears when the page it
/// is for loads; another page's thumbnail leaves it. The failure for a page
/// that is not among the model's pages therefore never clears, since that
/// page cannot load in this model. A failed load leaves the page's earlier
/// image, if it has one, and the next <see cref="LoadAsync"/> of that page
/// renders again. A cancelled load is not a failure.
/// </para>
/// </remarks>
public sealed class ThumbnailModel : ObservableModel, IDisposable
{
    readonly RenderCapability render;
    readonly Dictionary<PageRef, PageGeometry> geometries = [];
    readonly RenderRevisions revisions;
    readonly Dictionary<PageRef, (PixelBuffer Image, long Revision)> thumbnails = [];
    /// <summary>Cached pages, least recently used first.</summary>
    readonly List<PageRef> recency = [];
    readonly Dictionary<PageRef, Load> loads = [];
    IReadOnlyDictionary<PageRef, PixelBuffer> images = new Dictionary<PageRef, PixelBuffer>();
    int lastLoadId;
    /// <summary>The page <see cref="Error"/> is for.</summary>
    PageRef? failedPage;
    PdfException? error;

    /// <summary>One render in flight and the number of <see cref="LoadAsync"/> calls waiting on it.</summary>
    sealed class Load(int id, long revision)
    {
        public int Id { get; } = id;
        public long Revision { get; } = revision;
        public CancellationTokenSource Cancel { get; } = new();
        public Task Task { get; set; } = Task.CompletedTask;
        public int Waiters { get; set; }
    }

    /// <param name="render">The document's render capability. The model does not dispose it.</param>
    /// <param name="pages">The document's pages, which give each thumbnail its turn.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public ThumbnailModel(RenderCapability render, IEnumerable<PageInfo> pages, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(pages);
        this.render = render;
        foreach (var page in pages) geometries[page.Page] = page.Geometry;
        revisions = new RenderRevisions(render, Context);
        revisions.PropertyChanged += OnRevisionsChanged;
    }

    /// <summary>The latest failed load.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    /// <summary>The cached thumbnails as shown, replaced as a whole when one lands or leaves.</summary>
    public IReadOnlyDictionary<PageRef, PixelBuffer> Images => images;

    /// <summary>
    /// Every page's render revision, which rises each time the engine
    /// invalidates the page. A view keys a cell's load on it, so an edit loads
    /// that cell again.
    /// </summary>
    public IReadOnlyDictionary<PageRef, long> Revisions => revisions.Revisions;

    /// <summary>How many thumbnails the cache keeps. Tests lower it.</summary>
    internal int CacheLimit { get; set; } = 128;

    /// <summary>How many renders have started. Tests read it.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Runs when a render has started, before it reaches the engine. Tests hold a render open with it.</summary>
    internal Func<CancellationToken, Task>? RenderStarted { get; set; }

    /// <summary>How many <see cref="LoadAsync"/> calls wait on the page's render. Tests read it.</summary>
    internal int WaiterCount(PageRef page) => loads.TryGetValue(page, out var load) ? load.Waiters : 0;

    /// <summary>The page's cached thumbnail as shown, <c>null</c> until a load of it lands.</summary>
    public PixelBuffer? GetImage(PageRef page) => images.GetValueOrDefault(page);

    /// <summary>The page's render revision.</summary>
    public long GetRevision(PageRef page) => revisions.GetRevision(page);

    /// <summary>
    /// Renders the page's thumbnail unless the cache holds one for the page's
    /// current render revision, and returns when that render ends.
    /// </summary>
    /// <remarks>
    /// Calls for one page at one revision share a single render, and each
    /// returns when it ends: with the image cached, with <see cref="Error"/>
    /// set, or with neither when the render was cancelled or superseded. The
    /// render is cancelled when every call waiting on it is cancelled, or when
    /// a call arrives after the page's revision has moved on; the later call's
    /// render supersedes it. Cancelling one call does not return it early. It
    /// gives up its claim on the render and still returns when the render
    /// ends, which is when the image lands if another call is still waiting.
    /// A page that is not among the model's pages sets <see cref="Error"/> to
    /// <c>not-found</c> without rendering.
    /// </remarks>
    public async Task LoadAsync(PageRef page, CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        if (cancellationToken.IsCancellationRequested) return;
        if (!geometries.TryGetValue(page, out var geometry))
        {
            Error = new PdfException(PdfErrorCode.NotFound, "The page is not among the thumbnail model's pages.");
            failedPage = page;
            return;
        }
        var revision = revisions.GetRevision(page);
        if (thumbnails.TryGetValue(page, out var cached) && cached.Revision == revision)
        {
            MarkUsed(page);
            return;
        }
        if (!loads.TryGetValue(page, out var load) || load.Revision != revision)
        {
            var superseded = load;
            load = new Load(++lastLoadId, revision);
            loads[page] = load;
            // After the replacement: a cancelled render can resume inline, and
            // it must find that it no longer owns the page.
            superseded?.Cancel.Cancel();
            load.Task = RenderAsync(page, geometry, load);
        }
        load.Waiters++;
        var id = load.Id;
        using (cancellationToken.Register(() => Home.Run(() => StopWaiting(page, id))))
        {
            await load.Task;
        }
    }

    /// <summary>Ends the model's invalidation subscription. The cache stays.</summary>
    public void Dispose()
    {
        revisions.PropertyChanged -= OnRevisionsChanged;
        revisions.Dispose();
    }

    void OnRevisionsChanged(object? sender, PropertyChangedEventArgs e) => Raise(nameof(Revisions));

    /// <summary>Cancels the render once no call waits on it. A finished or superseded load is gone from <c>loads</c>, so this leaves it alone.</summary>
    void StopWaiting(PageRef page, int id)
    {
        if (!loads.TryGetValue(page, out var load) || load.Id != id) return;
        load.Waiters--;
        if (load.Waiters > 0) return;
        // Removed before the cancel, which can resume the render inline.
        loads.Remove(page);
        load.Cancel.Cancel();
    }

    async Task RenderAsync(PageRef page, PageGeometry geometry, Load load)
    {
        await Task.Yield();
        RenderCount++;
        var token = load.Cancel.Token;
        try
        {
            if (RenderStarted is { } started) await started(token);
            var rendered = await render.RenderThumbnailAsync(page, cancellationToken: token);
            // A cancelled or superseded load no longer owns the page.
            if (!Owns(page, load)) return;
            // Performance: for a rotated page this draws the thumbnail into a
            // new buffer, up to 512 by 512 pixels, on the home context. The
            // turn could run on the thread pool, since the buffer is not shared.
            var image = geometry.ToDisplay(rendered.Image);
            loads.Remove(page);
            thumbnails[page] = (image, load.Revision);
            MarkUsed(page);
            PublishImages();
            if (failedPage == page)
            {
                Error = null;
                failedPage = null;
            }
        }
        catch (Exception failure)
        {
            if (!Owns(page, load)) return;
            loads.Remove(page);
            Error = ModelErrors.Control(failure);
            failedPage = page;
        }
    }

    bool Owns(PageRef page, Load load) => loads.TryGetValue(page, out var current) && ReferenceEquals(current, load);

    /// <summary>Moves a cached page to the most recent end and drops the pages over the limit, oldest first.</summary>
    void MarkUsed(PageRef page)
    {
        if (!thumbnails.ContainsKey(page)) return;
        // Performance: a linear scan of at most CacheLimit pages per load.
        recency.Remove(page);
        recency.Add(page);
        var dropped = false;
        while (recency.Count > Math.Max(CacheLimit, 1))
        {
            thumbnails.Remove(recency[0]);
            recency.RemoveAt(0);
            dropped = true;
        }
        if (dropped) PublishImages();
    }

    void PublishImages()
    {
        images = thumbnails.ToDictionary(pair => pair.Key, pair => pair.Value.Image);
        Raise(nameof(Images));
    }
}
