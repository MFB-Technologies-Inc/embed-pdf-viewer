using System.Diagnostics.CodeAnalysis;

namespace EmbedPDF.Models;

/// <summary>
/// A reader in one model: it starts a viewer with the plugins its
/// <see cref="ReaderFeatures"/> need and those its configure callback adds,
/// opens one document at a time and builds the models a reader view shows.
/// A view draws <see cref="Phase"/>.
/// </summary>
/// <remarks>
/// <para>
/// The opens, <see cref="UnlockAsync"/> and <see cref="CloseAsync"/> run one
/// at a time, in the order they are called, and none of them throws: a
/// failure is <see cref="ReaderPhase.Failed"/>. A call made while an earlier
/// open is still loading supersedes it. The earlier open then returns
/// without touching <see cref="Phase"/>, and <see cref="Phase"/> follows the
/// later call; a URL still downloading for it is cancelled.
/// </para>
/// <para>
/// The model owns its viewer. <see cref="CloseAsync"/>, and
/// <see cref="DisposeAsync"/>, dispose it and return when the engine has shut
/// down, unless an open made while it waited superseded it. A capability or
/// model taken from a session stops working then.
/// </para>
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The byte and URL opens, as in Swift.")]
public sealed class PdfReaderModel : ObservableModel, IAsyncDisposable
{
    readonly Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder>? configure;
    /// <summary>Orders the opens, the unlocks and the close, so one document's close never lands between the next one's open and its reads.</summary>
    readonly SerialCalls calls = new();
    ReaderPhase phase = new ReaderPhase.Idle();
    PdfException? unlockError;
    /// <summary>Bumped by each open and each close. A call that finds it changed after a suspension has been superseded and leaves <see cref="Phase"/> alone.</summary>
    int generation;
    /// <summary>Cancels the newest open's or close's work when the next supersedes it, so a URL still downloading stops.</summary>
    CancellationTokenSource? newest;
    /// <summary>Lets the pan tool drag the current session's reader; replaced with each session.</summary>
    Registration? scrollHandler;

    /// <summary>Creates a reader with <paramref name="features"/>.</summary>
    /// <param name="features">What the reader offers.</param>
    /// <param name="configure">
    /// Adds what the features leave out. It gets the builder the model has
    /// prepared, with the render and stage plugins and the plugins the
    /// features need, and returns the builder to start the viewer with, as
    /// <c>b => b.History().Outline()</c>. The model calls it on the home
    /// context once for each viewer it starts: at the first open, and at the
    /// first open after each close. A callback that turns off a plugin a
    /// feature needs makes every open end in <see cref="ReaderPhase.Failed"/>
    /// with that capability's <c>unsupported</c> error.
    /// </param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public PdfReaderModel(
        ReaderFeatures features = ReaderFeatures.All,
        Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder>? configure = null,
        SynchronizationContext? context = null)
        : base(context)
    {
        Features = features;
        this.configure = configure;
    }

    /// <summary>The features the model was made with.</summary>
    public ReaderFeatures Features { get; }

    /// <summary>Where the reader stands.</summary>
    public ReaderPhase Phase { get => phase; private set => Set(ref phase, value); }

    /// <summary>
    /// Why the last <see cref="UnlockAsync"/> left the document locked: a wrong
    /// password is <c>permission-denied</c>. <c>null</c> once an unlock
    /// succeeds and after the next open or close.
    /// </summary>
    public PdfException? UnlockError { get => unlockError; private set => Set(ref unlockError, value); }

    /// <summary>The running viewer: made by the first open, disposed by a close.</summary>
    internal PdfViewer? Viewer { get; private set; }

    bool NeedsInteraction => (Features & (ReaderFeatures.Selection | ReaderFeatures.Tools | ReaderFeatures.Links)) != 0;

    /// <summary>
    /// Opens PDF bytes, closing the document that was open. Ends in
    /// <see cref="ReaderPhase.Ready"/>, in <see cref="ReaderPhase.Locked"/>
    /// for a protected document opened with no password or a wrong one, or in
    /// <see cref="ReaderPhase.Failed"/>, as for bytes that are not a PDF.
    /// <see cref="OpenDocumentOptions.Name"/> and
    /// <see cref="OpenDocumentOptions.Password"/> apply; the document is
    /// always made active.
    /// </summary>
    public Task OpenAsync(ReadOnlyMemory<byte> pdf, OpenDocumentOptions? options = null) =>
        LoadAsync((viewer, token) => viewer.OpenAsync(pdf, (options ?? new OpenDocumentOptions()) with { Activate = true }, token));

    /// <summary>
    /// Opens the PDF at <paramref name="url"/>, a file or a remote address,
    /// closing the document that was open. Ends as the byte open does; a
    /// download that fails ends in <see cref="ReaderPhase.Failed"/>.
    /// </summary>
    public Task OpenAsync(Uri url, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return LoadAsync((viewer, token) => viewer.OpenAsync(url, new OpenDocumentOptions { Name = name }, token));
    }

    /// <summary>
    /// Unlocks the document <see cref="ReaderPhase.Locked"/> holds. A wrong
    /// password leaves it locked with the reason in <see cref="UnlockError"/>;
    /// any other failure ends in <see cref="ReaderPhase.Failed"/>. Does
    /// nothing unless <see cref="Phase"/> is <see cref="ReaderPhase.Locked"/>.
    /// </summary>
    public async Task UnlockAsync(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        await Home.SwitchTo();
        if (phase is not ReaderPhase.Locked) return;
        var mine = generation;
        await calls.Enqueue(() => ApplyUnlockAsync(password, mine));
    }

    /// <summary>
    /// Closes the document and disposes the viewer. <see cref="Phase"/> is
    /// <see cref="ReaderPhase.Idle"/> at once, and the engine has shut down
    /// when this returns unless an open made before the close ran superseded
    /// it: the close then returns with the viewer still running, and that open
    /// uses it. An open made after the close has run starts a new viewer.
    /// </summary>
    public async Task CloseAsync()
    {
        await Home.SwitchTo();
        var mine = Supersede();
        Phase = new ReaderPhase.Idle();
        await RunAsync(_ => ApplyCloseAsync(mine));
    }

    /// <summary>Closes, as <see cref="CloseAsync"/> does.</summary>
    public async ValueTask DisposeAsync() => await CloseAsync();

    async Task LoadAsync(Func<PdfViewer, CancellationToken, Task<DocumentInfo>> open)
    {
        await Home.SwitchTo();
        var mine = Supersede();
        // Drops the session before its document closes, so no view draws from a closed document.
        Phase = new ReaderPhase.Loading();
        await RunAsync(token => ApplyOpenAsync(open, mine, token));
    }

    /// <summary>Makes the calls before this one stale and returns this one's generation.</summary>
    int Supersede()
    {
        generation++;
        newest?.Cancel();
        UnlockError = null;
        return generation;
    }

    async Task RunAsync(Func<CancellationToken, Task> work)
    {
        var cancel = new CancellationTokenSource();
        newest = cancel;
        try
        {
            await calls.Enqueue(() => work(cancel.Token));
        }
        finally
        {
            if (ReferenceEquals(newest, cancel)) newest = null;
            cancel.Dispose();
        }
    }

    async Task ApplyOpenAsync(Func<PdfViewer, CancellationToken, Task<DocumentInfo>> open, int mine, CancellationToken token)
    {
        if (generation != mine) return;
        try
        {
            var viewer = await StartedViewerAsync();
            // The document open before, and a failed tab an earlier open left.
            await viewer.CloseAllAsync();
            if (generation != mine) return;
            var document = await open(viewer, token);
            // The call that superseded this one closes what it opened.
            if (generation != mine) return;
            await SettleAsync(document, viewer, mine);
        }
        catch (Exception failure)
        {
            await FailAsync(failure, mine);
        }
    }

    async Task ApplyUnlockAsync(string password, int mine)
    {
        // An earlier unlock may have opened the document already.
        if (generation != mine || phase is not ReaderPhase.Locked locked || Viewer is not { } viewer) return;
        UnlockError = null;
        try
        {
            await viewer.UnlockAsync(locked.Document, password);
        }
        catch (Exception failure)
        {
            var error = ModelErrors.Control(failure);
            // A wrong password leaves the tab locked, ready for another try.
            if (error.Code != PdfErrorCode.PermissionDenied) await FailAsync(error, mine);
            else if (generation == mine) UnlockError = error;
            return;
        }
        if (generation != mine) return;
        try
        {
            var unlocked = await viewer.GetDocumentAsync(locked.Document.Id);
            if (unlocked is null)
            {
                await FailAsync(new PdfException(PdfErrorCode.NotFound, "the unlocked document is no longer open"), mine);
                return;
            }
            if (generation != mine) return;
            await SettleAsync(unlocked, viewer, mine);
        }
        catch (Exception failure)
        {
            await FailAsync(failure, mine);
        }
    }

    async Task ApplyCloseAsync(int mine)
    {
        if (generation != mine || Viewer is not { } viewer) return;
        Viewer = null;
        scrollHandler = null;
        // Closing has no way to report a failure, so one is dropped.
        try { await viewer.DisposeAsync(); }
        catch (PdfException) { }
    }

    /// <summary>The viewer, made and started on first use.</summary>
    async Task<PdfViewer> StartedViewerAsync()
    {
        if (Viewer is { } running) return running;
        var builder = new PdfViewerOptionsBuilder().Render(new RenderConfig()).Stage(new StageConfig());
        if (NeedsInteraction) builder = builder.Interaction();
        if (Features.HasFlag(ReaderFeatures.Search)) builder = builder.Search(new SearchConfig());
        if (Features.HasFlag(ReaderFeatures.Selection)) builder = builder.Selection();
        if (Features.HasFlag(ReaderFeatures.Tools)) builder = builder.Annotation();
        if (Features.HasFlag(ReaderFeatures.Links)) builder = builder.Link();
        if (configure is not null) builder = configure(builder);
        var viewer = new PdfViewer(builder.Build());
        try
        {
            await viewer.StartAsync();
        }
        catch
        {
            await viewer.DisposeAsync();
            throw;
        }
        Viewer = viewer;
        return viewer;
    }

    /// <summary>Moves to the phase the document's status calls for.</summary>
    async Task SettleAsync(DocumentInfo document, PdfViewer viewer, int mine)
    {
        switch (document.Status)
        {
            case DocumentStatus.Locked:
                Phase = new ReaderPhase.Locked(document);
                break;
            case DocumentStatus.Ready:
                var session = await SessionAsync(document, viewer);
                if (generation == mine) Phase = new ReaderPhase.Ready(session);
                break;
            default:
                throw new PdfException(
                    document.Error?.Code ?? PdfErrorCode.OperationFailed,
                    document.Error?.Message ?? "the document did not finish opening");
        }
    }

    async Task<ReaderSession> SessionAsync(DocumentInfo document, PdfViewer viewer)
    {
        // Performance: each accessor below is its own trip to the engine, made
        // one after another: up to eight per open with every feature on. They
        // do not depend on each other, so they could run together.
        var stage = await viewer.GetStageAsync(document);
        var render = await viewer.GetRenderAsync(document);
        var interaction = NeedsInteraction ? await viewer.GetInteractionAsync(document) : null;
        var link = Features.HasFlag(ReaderFeatures.Links) ? await viewer.GetLinkAsync(document) : null;
        if (Features.HasFlag(ReaderFeatures.Tools))
        {
            // Lets the built-in pan tool drag this reader. The registration
            // lasts as long as the document, which is as long as the session.
            scrollHandler = await stage.RegisterScrollHandlerAsync();
        }
        var search = Features.HasFlag(ReaderFeatures.Search) ? new SearchModel(await viewer.GetSearchAsync(document), Context) : null;
        var selection = Features.HasFlag(ReaderFeatures.Selection)
            ? new TextSelectionModel(await viewer.GetSelectionAsync(document), Context) : null;
        // Performance: every page's PageInfo crosses the bridge here, at each
        // open and after each unlock, a payload that grows with the page count.
        var pages = await viewer.GetPagesAsync(document);
        return new ReaderSession(
            viewer, document, pages, new StageReaderModel(stage, render, interaction, link, Context),
            search, selection,
            Features.HasFlag(ReaderFeatures.Tools) && interaction is not null ? new ToolModel(interaction, Context) : null);
    }

    /// <summary>Ends in <see cref="ReaderPhase.Failed"/> with nothing left open, unless a later call has taken over.</summary>
    async Task FailAsync(Exception failure, int mine)
    {
        if (generation != mine) return;
        // Bytes that are not a PDF leave a failed tab, and a failure after the
        // open leaves the document. A close that fails changes nothing: the
        // next open closes every tab again before it opens.
        if (Viewer is { } viewer)
        {
            try { await viewer.CloseAllAsync(); }
            catch (PdfException) { }
        }
        if (generation != mine) return;
        Phase = new ReaderPhase.Failed(ModelErrors.Control(failure));
    }
}
