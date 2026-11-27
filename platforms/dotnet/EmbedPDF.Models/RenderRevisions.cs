namespace EmbedPDF.Models;

/// <summary>
/// A per-page counter that rises each time the engine invalidates a page's
/// render. Views key their page images on it, so an edit redraws only the
/// pages it touched.
/// </summary>
/// <remarks>
/// The model observes by itself from construction until the document closes,
/// the model is disposed, or it is collected; there is no
/// <c>RunAsync</c>. Its loop holds the model weakly, so an abandoned model
/// is collectable, and disposing or collecting it ends its event
/// subscription, so nothing buffers for a model no one holds. A change
/// raises <c>PropertyChanged</c> for <see cref="Revisions"/>.
/// </remarks>
public sealed class RenderRevisions : ObservableModel, IDisposable
{
    readonly WeakObservation observation;
    IReadOnlyDictionary<PageRef, long> revisions = new Dictionary<PageRef, long>();

    /// <summary>Subscribes at once, so a page invalidated right after construction counts.</summary>
    /// <param name="render">The document's render capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public RenderRevisions(RenderCapability render, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(render);
        observation = WeakObservation.Start<RenderRevisions, RenderEvent>(
            this, Home, render.ObserveEvents, static (model, item) => model.Count(item));
    }

    /// <summary>Finalizes a model that was never disposed: ends its loop.</summary>
    ~RenderRevisions() => observation.Cancel();

    /// <summary>
    /// Every page invalidated so far and its revision, replaced as a whole
    /// on each invalidation.
    /// </summary>
    public IReadOnlyDictionary<PageRef, long> Revisions => revisions;

    /// <summary>The counting loop, which finishes when the document closes or the model is released. Tests await it.</summary>
    internal Task ObservationTask => observation.Task;

    /// <summary>The page's revision; <c>0</c> for a page never invalidated.</summary>
    public long GetRevision(PageRef page) => revisions.TryGetValue(page, out var revision) ? revision : 0;

    /// <summary>Ends the subscription. The counts stay.</summary>
    public void Dispose()
    {
        observation.Cancel();
        GC.SuppressFinalize(this);
    }

    void Count(RenderEvent item)
    {
        if (item is not RenderEvent.Invalidated invalidated || invalidated.Pages.Count == 0) return;
        var next = new Dictionary<PageRef, long>(revisions);
        foreach (var page in invalidated.Pages)
        {
            next[page] = next.TryGetValue(page, out var revision) ? revision + 1 : 1;
        }
        revisions = next;
        Raise(nameof(Revisions));
    }
}
