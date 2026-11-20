using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using G = uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>
/// What a <see cref="PdfViewer"/> installs, and the services it uses.
/// </summary>
/// <remarks>
/// An unset field is the engine's default for an absent field: a plugin is
/// left out (<see langword="false"/> or <see langword="null"/>), and
/// <see cref="Scope"/> is <see langword="null"/>, which the engine takes as
/// <c>["*"]</c>. A plugin left out answers <c>unsupported</c> at its accessor.
/// Options are a value: <c>with</c> or <see cref="PdfViewerOptionsBuilder"/>
/// makes a changed copy.
/// </remarks>
public sealed record PdfViewerOptions
{
    /// <summary>The capability scope every document opens with (<c>doc.open</c>, <c>doc.annotate.modify</c>, ...). <see langword="null"/> is <c>["*"]</c>: everything.</summary>
    public IReadOnlyList<string>? Scope { get; init; }

    /// <summary>Installs the metadata plugin: the document Info dict, read and edited.</summary>
    public bool Metadata { get; init; }

    /// <summary>Installs the search plugin.</summary>
    public SearchConfig? Search { get; init; }

    /// <summary>Installs the render plugin.</summary>
    public RenderConfig? Render { get; init; }

    /// <summary>Installs the pointer hub that selection, link, annotation, form and measurement need.</summary>
    public bool Interaction { get; init; }

    /// <summary>Installs the feedback plugin; <see langword="null"/> leaves it out and plugins skip feedback.</summary>
    public FeedbackConfig? Feedback { get; init; }

    /// <summary>Installs the stage plugin.</summary>
    public StageConfig? Stage { get; init; }

    /// <summary>Installs text selection. Needs <see cref="Interaction"/>.</summary>
    public bool Selection { get; init; }

    /// <summary>Installs the link plugin. Needs <see cref="Interaction"/>.</summary>
    public bool Link { get; init; }

    /// <summary>Installs the annotation plugin with these settings as JSON over the defaults; <c>""</c> is the defaults. Needs <see cref="Interaction"/>.</summary>
    public string? Annotation { get; init; }

    /// <summary>Installs undo and redo.</summary>
    public bool History { get; init; }

    /// <summary>Installs the outline plugin.</summary>
    public bool Outline { get; init; }

    /// <summary>Installs the attachment plugin.</summary>
    public bool Attachment { get; init; }

    /// <summary>Installs the form plugin. Needs <see cref="Interaction"/>.</summary>
    public FormConfig? Form { get; init; }

    /// <summary>Installs the redaction plugin. Needs <see cref="Annotation"/>.</summary>
    public RedactionConfig? Redaction { get; init; }

    /// <summary>Installs the measurement plugin. Needs <see cref="Interaction"/> and <see cref="Annotation"/>.</summary>
    public MeasurementConfig? Measurement { get; init; }

    /// <summary>Installs the stamp plugin.</summary>
    public StampConfig? Stamp { get; init; }

    /// <summary>Installs page editing.</summary>
    public bool PageEdit { get; init; }

    /// <summary>Installs the view manager: panes and tabs.</summary>
    public bool ViewManager { get; init; }

    /// <summary>Installs the shell plugin.</summary>
    public bool Shell { get; init; }

    /// <summary>Installs the i18n plugin.</summary>
    public I18nConfig? I18n { get; init; }

    /// <summary>Installs the commands plugin.</summary>
    public bool Commands { get; init; }

    /// <summary>The owners' standard command lists, joined in a fixed order. Needs <see cref="Commands"/>.</summary>
    public IReadOnlyList<StandardCommandList>? StandardCommands { get; init; }

    /// <summary>Installs the actions plugin.</summary>
    public ActionsConfig? Actions { get; init; }

    /// <summary>Installs the signature plugin. Needs <see cref="Form"/>.</summary>
    public SignatureConfig? Signature { get; init; }

    /// <summary>Where diagnostics go. Defaults to <see cref="NullLogger.Instance"/>.</summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;

    internal G.ViewerConfig ToFfi() => new(
        Scope: Scope?.ToArray(),
        Metadata: Metadata,
        Search: Search?.ToFfi(),
        Render: Render?.ToFfi(),
        Interaction: Interaction,
        Feedback: Feedback?.ToFfi(),
        Stage: Stage?.ToFfi(),
        Selection: Selection,
        Link: Link,
        Annotation: Annotation,
        History: History,
        Outline: Outline,
        Attachment: Attachment,
        Form: Form?.ToFfi(),
        Redaction: Redaction?.ToFfi(),
        Measurement: Measurement?.ToFfi(),
        Stamp: Stamp?.ToFfi(),
        PageEdit: PageEdit,
        ViewManager: ViewManager,
        Shell: Shell,
        I18n: I18n?.ToFfi(),
        Commands: Commands,
        StandardCommands: StandardCommands?.Select(list => list.ToFfi()).ToArray(),
        Actions: Actions?.ToFfi(),
        Signature: Signature?.ToFfi());
}
