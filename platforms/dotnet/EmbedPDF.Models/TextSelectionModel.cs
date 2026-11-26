namespace EmbedPDF.Models;

/// <summary>What a drag's first point selects.</summary>
public enum SelectionStart
{
    /// <summary>An empty selection at the point, grown by the drag.</summary>
    Caret,

    /// <summary>The word under the point, grown by the drag.</summary>
    Word,
}

/// <summary>
/// Turns a drag over a page into a text selection and mirrors the
/// selection's rects onto the home context. Every call runs after the calls
/// made before it, and drag points that arrive while an extension waits
/// collapse into the newest one.
/// </summary>
/// <remarks>
/// The model observes by itself from construction until the document closes,
/// the model is disposed, or it is collected; there is no <c>RunAsync</c>.
/// Its loop holds the model weakly, and disposing or collecting it ends its
/// event subscription, so nothing buffers for a model no one holds. Points
/// are in unrotated page space; a view turns its own coordinates into them
/// with <see cref="StageVisiblePageExtensions.PagePoint"/>.
/// </remarks>
public sealed class TextSelectionModel : ObservableModel, IDisposable
{
    static readonly IReadOnlyDictionary<PageRef, IReadOnlyList<PdfRect>> NoRects = new Dictionary<PageRef, IReadOnlyList<PdfRect>>();

    readonly SelectionCapability selection;
    /// <summary>
    /// Orders the drag calls and every rects refresh. Each call leaves the
    /// home context while it reaches the engine, so without this an end could
    /// reach the engine before the begin it closes, and an older rects read
    /// could overwrite a newer one.
    /// </summary>
    readonly SerialCalls calls = new();
    readonly WeakObservation observation;
    IReadOnlyDictionary<PageRef, IReadOnlyList<PdfRect>> rects = NoRects;
    bool isSelecting;
    PdfException? error;
    /// <summary>The newest drag point no extension has applied yet.</summary>
    (PdfPoint Point, PageRef Page)? pending;
    /// <summary>Whether an extension waits in the chain; it applies whatever <see cref="pending"/> holds when it runs.</summary>
    bool extensionQueued;

    /// <summary>Subscribes at once, so a selection made right after construction still reaches <see cref="Rects"/>.</summary>
    /// <param name="selection">The document's selection capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public TextSelectionModel(SelectionCapability selection, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(selection);
        this.selection = selection;
        observation = WeakObservation.Start<TextSelectionModel, SelectionEvent>(
            this, Home, selection.ObserveEvents, static (model, item) => model.Follow(item));
    }

    /// <summary>Finalizes a model that was never disposed: ends its loop.</summary>
    ~TextSelectionModel() => observation.Cancel();

    /// <summary>Selected rects per page, in unrotated page points, replaced as a whole when they change.</summary>
    public IReadOnlyDictionary<PageRef, IReadOnlyList<PdfRect>> Rects => rects;

    /// <summary>Whether a drag has begun on text and not yet ended.</summary>
    public bool IsSelecting { get => isSelecting; private set => Set(ref isSelecting, value); }

    /// <summary>The last error the engine threw, kept until the next <see cref="DragBegan"/> or <see cref="ClearAsync"/> runs.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    /// <summary>Extensions made so far. Tests read it.</summary>
    internal int ExtensionCount { get; private set; }

    /// <summary>Runs after a refresh's reads return and before they are applied. Tests hold a refresh here.</summary>
    internal Func<Task>? RectsReadReturned { get; set; }

    /// <summary>The event loop, which finishes when the document closes or the model is released. Tests await it.</summary>
    internal Task ObservationTask => observation.Task;

    /// <summary>
    /// Starts a selection at <paramref name="point"/>. Returns at once: the
    /// begin runs after the calls made before it, and <see cref="Error"/>
    /// clears when it runs, not when this returns. <see cref="IsSelecting"/>
    /// turns true when the engine finds text at the point, and
    /// <see cref="DragEndedAsync"/> reports whether it did. With
    /// <see cref="SelectionStart.Word"/>, a failure after the word is selected
    /// leaves it selected with <see cref="IsSelecting"/> false and the error
    /// in <see cref="Error"/>.
    /// </summary>
    public void DragBegan(PdfPoint point, PageRef page, SelectionStart start = SelectionStart.Caret) => Home.Run(() =>
    {
        // A point left by a drag that never began belongs to that drag.
        pending = null;
        _ = calls.Enqueue(() => ApplyBeginAsync(point, page, start));
    });

    /// <summary>
    /// Moves the selection's focus to <paramref name="point"/>. Points that
    /// arrive while an extension waits replace each other, and only the newest
    /// reaches the engine.
    /// </summary>
    public void DragChanged(PdfPoint point, PageRef page) => Home.Run(() =>
    {
        pending = (point, page);
        if (extensionQueued) return;
        extensionQueued = true;
        _ = calls.Enqueue(async () =>
        {
            extensionQueued = false;
            // Before a begin succeeds, the point waits for it.
            if (!isSelecting) return;
            await ApplyPendingAsync();
        });
    });

    /// <summary>
    /// Ends the drag, keeping the selection. True when the drag began on text;
    /// false when the point had no text, the begin failed, or no drag was
    /// open. <see cref="Rects"/> are final when this returns: every refresh
    /// queued before the end, a selection event's included, has been applied
    /// in order.
    /// </summary>
    public async Task<bool> DragEndedAsync()
    {
        await Home.SwitchTo();
        return await calls.Enqueue(ApplyEndAsync);
    }

    /// <summary>Clears the selection, <see cref="Rects"/> and <see cref="Error"/>.</summary>
    public async Task ClearAsync()
    {
        await Home.SwitchTo();
        await calls.Enqueue(ApplyClearAsync);
    }

    /// <summary>Ends the event subscription.</summary>
    public void Dispose()
    {
        observation.Cancel();
        GC.SuppressFinalize(this);
    }

    /// <summary>Queues the event's effect on <see cref="Rects"/> behind the calls made so far.</summary>
    void Follow(SelectionEvent item)
    {
        switch (item)
        {
            case SelectionEvent.Changed or SelectionEvent.Committed:
                // Performance: every changed or committed event queues a full
                // rects refresh, and an extension that moves the selection
                // emits one, so a drag point pays two rounds of reads.
                _ = calls.Enqueue(RefreshRectsAsync);
                break;
            case SelectionEvent.Cleared:
                _ = calls.Enqueue(() => SetRects(NoRects));
                break;
        }
    }

    async Task ApplyBeginAsync(PdfPoint point, PageRef page, SelectionStart start)
    {
        Error = null;
        try
        {
            await selection.EnsureLoadedAsync(page);
            bool began;
            if (start == SelectionStart.Word)
            {
                began = await selection.SelectWordAtAsync(page, point);
                if (began) await selection.BeginGestureAsync();
            }
            else
            {
                began = await selection.BeginGestureAtAsync(page, point);
            }
            if (!began) return;
            IsSelecting = true;
        }
        catch (Exception failure)
        {
            Error = ModelErrors.Control(failure);
            return;
        }
        // A point that arrived before or during the begin applies inside this
        // call, so an end queued behind the begin finds it applied.
        if (pending is not null) await ApplyPendingAsync();
        else await RefreshRectsAsync();
    }

    async Task ApplyPendingAsync()
    {
        if (pending is not { } next) return;
        pending = null;
        try
        {
            // Performance: one IsLoaded round trip before every extension,
            // though a drag stays on one page for most of its points.
            if (!await selection.IsLoadedAsync(next.Page)) await selection.EnsureLoadedAsync(next.Page);
            ExtensionCount++;
            await selection.ExtendToAsync(next.Page, next.Point);
        }
        catch (Exception failure)
        {
            Error = ModelErrors.Control(failure);
            return;
        }
        await RefreshRectsAsync();
    }

    async Task<bool> ApplyEndAsync()
    {
        pending = null;
        if (!isSelecting) return false;
        try { await selection.EndGestureAsync(); }
        catch (Exception failure) { Error = ModelErrors.Control(failure); }
        IsSelecting = false;
        return true;
    }

    async Task ApplyClearAsync()
    {
        Error = null;
        try { await selection.ClearAsync(); }
        catch (Exception failure) { Error = ModelErrors.Control(failure); }
        SetRects(NoRects);
    }

    async Task RefreshRectsAsync()
    {
        var next = new Dictionary<PageRef, IReadOnlyList<PdfRect>>();
        try
        {
            // Performance: one round trip for the page list and one more per
            // selected page, in sequence, on every refresh.
            foreach (var page in await selection.GetSelectedPagesAsync())
            {
                next[page] = await selection.GetRectsAsync(page);
            }
        }
        catch (Exception failure)
        {
            Error = ModelErrors.Control(failure);
            return;
        }
        if (RectsReadReturned is { } held) await held();
        SetRects(next);
    }

    void SetRects(IReadOnlyDictionary<PageRef, IReadOnlyList<PdfRect>> value)
    {
        if (value.Count == rects.Count
            && value.All(pair => rects.TryGetValue(pair.Key, out var old) && old.SequenceEqual(pair.Value))) return;
        rects = value;
        Raise(nameof(Rects));
    }
}
