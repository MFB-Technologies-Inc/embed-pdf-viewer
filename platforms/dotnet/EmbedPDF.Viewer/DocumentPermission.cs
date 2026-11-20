namespace EmbedPDF;

/// <summary>
/// A permission <see cref="PdfViewer.AllowsAsync"/> asks the document's scope
/// about. <see cref="Value"/> is the wire string.
/// </summary>
/// <remarks>
/// The set is open, so this is a struct rather than an enum: the constants
/// are the engine's document capabilities (<c>doc.*</c>) and
/// <c>annotations:create</c>, a plugin's own permission is a string
/// (<c>"protected-display.view"</c>), and a newer engine can add permissions
/// without breaking a caller's <c>switch</c>.
/// </remarks>
/// <param name="Value">The wire string.</param>
public readonly record struct DocumentPermission(string Value)
{
    /// <summary>The wire string; empty for <c>default(DocumentPermission)</c>.</summary>
    public string Value { get => field ?? string.Empty; init => field = value ?? throw new ArgumentNullException(nameof(value)); } = Value;

    /// <summary><c>doc.open</c>.</summary>
    public static DocumentPermission Open { get; } = new("doc.open");
    /// <summary><c>doc.render</c>.</summary>
    public static DocumentPermission Render { get; } = new("doc.render");
    /// <summary><c>doc.render.thumbnail</c>: the thumbnail pictures only; <see cref="Render"/> implies it.</summary>
    public static DocumentPermission RenderThumbnail { get; } = new("doc.render.thumbnail");
    /// <summary><c>doc.text.select</c>.</summary>
    public static DocumentPermission TextSelect { get; } = new("doc.text.select");
    /// <summary><c>doc.text.copy</c>.</summary>
    public static DocumentPermission TextCopy { get; } = new("doc.text.copy");
    /// <summary><c>doc.text.search</c>.</summary>
    public static DocumentPermission TextSearch { get; } = new("doc.text.search");
    /// <summary><c>doc.content.copy</c>.</summary>
    public static DocumentPermission ContentCopy { get; } = new("doc.content.copy");
    /// <summary><c>doc.download</c>.</summary>
    public static DocumentPermission Download { get; } = new("doc.download");
    /// <summary><c>doc.download.flattened</c>.</summary>
    public static DocumentPermission DownloadFlattened { get; } = new("doc.download.flattened");
    /// <summary><c>doc.print</c>.</summary>
    public static DocumentPermission Print { get; } = new("doc.print");
    /// <summary><c>doc.print.high</c>.</summary>
    public static DocumentPermission PrintHigh { get; } = new("doc.print.high");
    /// <summary><c>doc.pages.modify</c>.</summary>
    public static DocumentPermission PagesModify { get; } = new("doc.pages.modify");
    /// <summary><c>doc.pages.assemble</c>.</summary>
    public static DocumentPermission PagesAssemble { get; } = new("doc.pages.assemble");
    /// <summary><c>doc.forms.read</c>.</summary>
    public static DocumentPermission FormsRead { get; } = new("doc.forms.read");
    /// <summary><c>doc.forms.fill</c>.</summary>
    public static DocumentPermission FormsFill { get; } = new("doc.forms.fill");
    /// <summary><c>doc.forms.modify</c>.</summary>
    public static DocumentPermission FormsModify { get; } = new("doc.forms.modify");
    /// <summary><c>doc.forms.import</c>.</summary>
    public static DocumentPermission FormsImport { get; } = new("doc.forms.import");
    /// <summary><c>doc.forms.script</c>.</summary>
    public static DocumentPermission FormsScript { get; } = new("doc.forms.script");
    /// <summary><c>doc.forms.submit</c>.</summary>
    public static DocumentPermission FormsSubmit { get; } = new("doc.forms.submit");
    /// <summary><c>doc.annotate.read</c>.</summary>
    public static DocumentPermission AnnotateRead { get; } = new("doc.annotate.read");
    /// <summary><c>doc.annotate.modify</c>.</summary>
    public static DocumentPermission AnnotateModify { get; } = new("doc.annotate.modify");
    /// <summary><c>doc.annotate.import</c>.</summary>
    public static DocumentPermission AnnotateImport { get; } = new("doc.annotate.import");
    /// <summary><c>doc.metadata.modify</c>.</summary>
    public static DocumentPermission MetadataModify { get; } = new("doc.metadata.modify");
    /// <summary><c>doc.attachments.modify</c>.</summary>
    public static DocumentPermission AttachmentsModify { get; } = new("doc.attachments.modify");
    /// <summary><c>doc.outline.modify</c>.</summary>
    public static DocumentPermission OutlineModify { get; } = new("doc.outline.modify");
    /// <summary><c>doc.redact</c>.</summary>
    public static DocumentPermission Redact { get; } = new("doc.redact");
    /// <summary><c>doc.sign</c>.</summary>
    public static DocumentPermission Sign { get; } = new("doc.sign");
    /// <summary><c>doc.sign.certify</c>.</summary>
    public static DocumentPermission SignCertify { get; } = new("doc.sign.certify");
    /// <summary><c>annotations:create</c>: whether this caller may create annotations of their own.</summary>
    public static DocumentPermission AnnotationsCreate { get; } = new("annotations:create");

    /// <summary>Every constant this type names, the <c>doc.*</c> ones in the engine's order.</summary>
    public static IReadOnlyList<DocumentPermission> Named { get; } =
    [
        Open, Render, RenderThumbnail, TextSelect, TextCopy, TextSearch, ContentCopy, Download,
        DownloadFlattened, Print, PrintHigh, PagesModify, PagesAssemble, FormsRead, FormsFill,
        FormsModify, FormsImport, FormsScript, FormsSubmit, AnnotateRead, AnnotateModify,
        AnnotateImport, MetadataModify, AttachmentsModify, OutlineModify, Redact, Sign,
        SignCertify, AnnotationsCreate,
    ];

    /// <summary>The permission whose wire string is <paramref name="value"/>.</summary>
    public static implicit operator DocumentPermission(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;
}
