namespace EmbedPDF.Models;

/// <summary>What a <see cref="PdfReaderModel"/> offers beyond showing and scrolling pages. Each feature installs the plugins it needs and nothing else.</summary>
[Flags]
public enum ReaderFeatures
{
    /// <summary>Pages only.</summary>
    None = 0,

    /// <summary>Text search: <see cref="ReaderSession.Search"/>.</summary>
    Search = 1 << 0,

    /// <summary>Text selection: <see cref="ReaderSession.Selection"/>.</summary>
    Selection = 1 << 1,

    /// <summary>
    /// The interaction tools, the annotation tools included:
    /// <see cref="ReaderSession.Tools"/>. A drag under a tool other than the
    /// default goes to that tool, and the built-in pan tool scrolls.
    /// </summary>
    Tools = 1 << 2,

    /// <summary>Links: a tap on one activates it, and the outcome arrives on the session reader's <see cref="StageReaderModel.ObserveLinkActivations"/>.</summary>
    Links = 1 << 3,

    /// <summary>Every feature.</summary>
    All = Search | Selection | Tools | Links,
}

/// <summary>Where a <see cref="PdfReaderModel"/> stands.</summary>
public abstract record ReaderPhase
{
    private protected ReaderPhase() { }

    /// <summary>Nothing is open: before the first open and after a close.</summary>
    public sealed record Idle : ReaderPhase;

    /// <summary>An open is under way.</summary>
    public sealed record Loading : ReaderPhase;

    /// <summary>The document needs a password; <see cref="PdfReaderModel.UnlockAsync"/> takes it.</summary>
    /// <param name="Document">The locked document.</param>
    public sealed record Locked(DocumentInfo Document) : ReaderPhase;

    /// <summary>The document is open.</summary>
    /// <param name="Session">The open document and the models over it.</param>
    public sealed record Ready(ReaderSession Session) : ReaderPhase;

    /// <summary>The open, or what followed it, failed. Nothing is left open, and the next open starts again.</summary>
    /// <param name="Error">Why.</param>
    public sealed record Failed(PdfException Error) : ReaderPhase;
}

/// <summary>
/// One open document and the models over it. A session belongs to that
/// document: the next open, and a close, replace it with a new value and
/// never change this one. When the phase leaves it, the reader model
/// disposes its models and the capabilities under them, so read the session
/// from <see cref="PdfReaderModel.Phase"/> each time instead of keeping one.
/// </summary>
/// <param name="Viewer">The model's viewer.</param>
/// <param name="Document">The open document.</param>
/// <param name="Pages">The document's pages as they were when it opened.</param>
/// <param name="Reader">The reader model a page view draws.</param>
/// <param name="Search"><c>null</c> without <see cref="ReaderFeatures.Search"/>.</param>
/// <param name="Selection"><c>null</c> without <see cref="ReaderFeatures.Selection"/>.</param>
/// <param name="Tools"><c>null</c> without <see cref="ReaderFeatures.Tools"/>.</param>
public sealed record ReaderSession(
    PdfViewer Viewer, DocumentInfo Document, IReadOnlyList<PageInfo> Pages, StageReaderModel Reader,
    SearchModel? Search, TextSelectionModel? Selection, ToolModel? Tools)
{
    /// <summary>The <see cref="PageInfo"/> of a page the stage shows, or <c>null</c> for a page added after the document opened.</summary>
    public PageInfo? GetPageInfo(StageVisiblePage visiblePage)
    {
        ArgumentNullException.ThrowIfNull(visiblePage);
        var index = visiblePage.PageIndex;
        if (index >= 0 && index < Pages.Count && Pages[index].Page == visiblePage.Page) return Pages[index];
        // A page edit moved the page since the document opened.
        // Performance: a scan of every page per call; a dictionary by PageRef
        // built with the session makes it constant if documents whose pages
        // move grow large.
        return Pages.FirstOrDefault(page => page.Page == visiblePage.Page);
    }
}
