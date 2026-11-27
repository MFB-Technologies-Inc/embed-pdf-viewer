namespace EmbedPDF;

/// <summary>Where a document's tab stands.</summary>
public enum DocumentStatus
{
    /// <summary>The source is still arriving (a URL open).</summary>
    Loading,
    /// <summary>Password-protected and not yet unlocked.</summary>
    Locked,
    /// <summary>Open and usable.</summary>
    Ready,
    /// <summary>The open failed; <see cref="DocumentInfo.Error"/> says why.</summary>
    Failed,
}

/// <summary>How <see cref="PdfViewer.SaveAsync"/> writes the document.</summary>
public enum SaveMode
{
    /// <summary>Appends the edits after the original bytes and keeps every signature valid.</summary>
    Incremental,
    /// <summary>A fresh, smaller file without the revision history. A signed document refuses it with <c>permission-denied</c>.</summary>
    Rewrite,
}

/// <summary>
/// A plugin error as the kernel's <c>toPluginErrorInfo</c> reports it: why a
/// tab failed to open.
/// </summary>
/// <param name="Code">The kernel error code.</param>
/// <param name="Message">The engine's words, with no code in front.</param>
/// <param name="Capability">The capability that failed, such as <c>documents</c>.</param>
/// <param name="Permission">The permission the session lacks, or <see langword="null"/>.</param>
public sealed record PluginErrorInfo(PdfErrorCode Code, string Message, string Capability, string? Permission);

/// <summary>A document tab. Loading, locked and failed tabs are documents too.</summary>
/// <param name="Id">The tab's identity. A retry may replace a pending tab's id.</param>
/// <param name="Name">The display name, or <see langword="null"/>.</param>
/// <param name="PageCount">Pages in the document; 0 until it is ready.</param>
/// <param name="Status">Where the tab stands.</param>
/// <param name="Error"><see cref="DocumentStatus.Failed"/> only: why the open failed. A wrong password leads the message with the engine's <c>DocPasswordIncorrect</c>.</param>
public sealed record DocumentInfo(string Id, string? Name, int PageCount, DocumentStatus Status, PluginErrorInfo? Error);

/// <summary>A page's stable identity: its PDF object number, which survives page moves.</summary>
/// <param name="PageObjectNumber">The page object's number in the PDF.</param>
public readonly record struct PageRef(int PageObjectNumber);

/// <summary>A page as the document lists it.</summary>
/// <param name="Page">The page's stable identity.</param>
/// <param name="Index">Its position, from 0.</param>
/// <param name="Width">Unrotated width in PDF points.</param>
/// <param name="Height">Unrotated height in PDF points.</param>
/// <param name="Rotation">Degrees clockwise: 0, 90, 180 or 270.</param>
public sealed record PageInfo(PageRef Page, int Index, double Width, double Height, int Rotation)
{
    /// <summary>Converts between this page's unrotated space and the page as shown.</summary>
    public PageGeometry Geometry => new(Rotation, new PdfSize(Width, Height));
}

/// <summary>
/// A change to the viewer's documents, from <see cref="PdfViewer.ObserveDocumentEvents"/>.
/// </summary>
public abstract record DocumentEvent
{
    private protected DocumentEvent() { }

    /// <summary>A document finished opening.</summary>
    /// <param name="Document">The opened document.</param>
    public sealed record Opened(DocumentInfo Document) : DocumentEvent;

    /// <summary>An open failed. <paramref name="Error"/> is the record the failed tab's <see cref="DocumentInfo.Error"/> holds.</summary>
    /// <param name="DocumentId">The failed tab.</param>
    /// <param name="Error">Why it failed.</param>
    public sealed record OpenFailed(string DocumentId, PluginErrorInfo Error) : DocumentEvent;

    /// <summary>A document opened locked.</summary>
    /// <param name="DocumentId">The locked tab.</param>
    /// <param name="PasswordProvided"><see langword="false"/> when the document wants a password, <see langword="true"/> when the one given did not unlock it.</param>
    public sealed record Locked(string DocumentId, bool PasswordProvided) : DocumentEvent;

    /// <summary>A document closed.</summary>
    /// <param name="DocumentId">The closed tab.</param>
    public sealed record Closed(string DocumentId) : DocumentEvent;

    /// <summary>The active document changed.</summary>
    /// <param name="DocumentId">The new active tab, or <see langword="null"/> for none.</param>
    public sealed record ActiveChanged(string? DocumentId) : DocumentEvent;

    /// <summary>A document's page list changed.</summary>
    /// <param name="DocumentId">The changed document.</param>
    public sealed record PagesChanged(string DocumentId) : DocumentEvent;
}
