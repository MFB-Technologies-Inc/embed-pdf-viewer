using Microsoft.Extensions.Logging;

namespace EmbedPDF;

/// <summary>
/// A chained way to build <see cref="PdfViewerOptions"/>. Every method
/// returns a changed copy, so a builder is a value:
/// <code>
/// await using var viewer = await PdfViewer.CreateAsync(
///     new PdfViewerOptionsBuilder().Interaction().Annotation().Search(new()).Build());
/// </code>
/// </summary>
/// <remarks>
/// A builder with no calls builds options equal to <c>new PdfViewerOptions()</c>.
/// A plugin added to the engine's configuration is one new method here and
/// no change for a caller; the builder tests fail when the two drift apart.
/// </remarks>
public sealed class PdfViewerOptionsBuilder
{
    readonly PdfViewerOptions options;

    /// <summary>Starts from <c>new PdfViewerOptions()</c>: every plugin left out.</summary>
    public PdfViewerOptionsBuilder() : this(new PdfViewerOptions()) { }

    /// <summary>Starts from <paramref name="options"/>, to change a few of its fields.</summary>
    public PdfViewerOptionsBuilder(PdfViewerOptions options) => this.options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>The options the calls so far describe.</summary>
    public PdfViewerOptions Build() => options;

    PdfViewerOptionsBuilder With(Func<PdfViewerOptions, PdfViewerOptions> change) => new(change(options));

    /// <summary>The capability scope every document opens with. <see langword="null"/> is <c>["*"]</c>.</summary>
    public PdfViewerOptionsBuilder Scope(IEnumerable<string>? scope) => With(o => o with { Scope = scope?.ToArray() });

    /// <summary>Installs the metadata plugin, or leaves it out.</summary>
    public PdfViewerOptionsBuilder Metadata(bool enabled = true) => With(o => o with { Metadata = enabled });

    /// <summary>Installs the search plugin.</summary>
    public PdfViewerOptionsBuilder Search(SearchConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Search = config });

    /// <summary>Installs the render plugin.</summary>
    public PdfViewerOptionsBuilder Render(RenderConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Render = config });

    /// <summary>Installs the pointer hub that selection, link, annotation, form and measurement need.</summary>
    public PdfViewerOptionsBuilder Interaction(bool enabled = true) => With(o => o with { Interaction = enabled });

    /// <summary>Installs the feedback plugin.</summary>
    public PdfViewerOptionsBuilder Feedback(FeedbackConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Feedback = config });

    /// <summary>Installs the stage plugin.</summary>
    public PdfViewerOptionsBuilder Stage(StageConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Stage = config });

    /// <summary>Installs text selection. Needs <see cref="Interaction"/>.</summary>
    public PdfViewerOptionsBuilder Selection(bool enabled = true) => With(o => o with { Selection = enabled });

    /// <summary>Installs the link plugin. Needs <see cref="Interaction"/>.</summary>
    public PdfViewerOptionsBuilder Link(bool enabled = true) => With(o => o with { Link = enabled });

    /// <summary>Installs the annotation plugin; <paramref name="settingsJson"/> is registered over the defaults, <c>""</c> is the defaults. Needs <see cref="Interaction"/>.</summary>
    public PdfViewerOptionsBuilder Annotation(string settingsJson = "") => settingsJson is null ? throw new ArgumentNullException(nameof(settingsJson)) : With(o => o with { Annotation = settingsJson });

    /// <summary>Installs undo and redo.</summary>
    public PdfViewerOptionsBuilder History(bool enabled = true) => With(o => o with { History = enabled });

    /// <summary>Installs the outline plugin.</summary>
    public PdfViewerOptionsBuilder Outline(bool enabled = true) => With(o => o with { Outline = enabled });

    /// <summary>Installs the attachment plugin.</summary>
    public PdfViewerOptionsBuilder Attachment(bool enabled = true) => With(o => o with { Attachment = enabled });

    /// <summary>Installs the form plugin. Needs <see cref="Interaction"/>.</summary>
    public PdfViewerOptionsBuilder Form(FormConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Form = config });

    /// <summary>Installs the redaction plugin. Needs <see cref="Annotation"/>.</summary>
    public PdfViewerOptionsBuilder Redaction(RedactionConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Redaction = config });

    /// <summary>Installs the measurement plugin. Needs <see cref="Interaction"/> and <see cref="Annotation"/>.</summary>
    public PdfViewerOptionsBuilder Measurement(MeasurementConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Measurement = config });

    /// <summary>Installs the stamp plugin.</summary>
    public PdfViewerOptionsBuilder Stamp(StampConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Stamp = config });

    /// <summary>Installs page editing.</summary>
    public PdfViewerOptionsBuilder PageEdit(bool enabled = true) => With(o => o with { PageEdit = enabled });

    /// <summary>Installs the view manager.</summary>
    public PdfViewerOptionsBuilder ViewManager(bool enabled = true) => With(o => o with { ViewManager = enabled });

    /// <summary>Installs the shell plugin.</summary>
    public PdfViewerOptionsBuilder Shell(bool enabled = true) => With(o => o with { Shell = enabled });

    /// <summary>Installs the i18n plugin.</summary>
    public PdfViewerOptionsBuilder I18n(I18nConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { I18n = config });

    /// <summary>Installs the commands plugin.</summary>
    public PdfViewerOptionsBuilder Commands(bool enabled = true) => With(o => o with { Commands = enabled });

    /// <summary>The owners' standard command lists. Needs <see cref="Commands"/>.</summary>
    public PdfViewerOptionsBuilder StandardCommands(IEnumerable<StandardCommandList> lists) => lists is null ? throw new ArgumentNullException(nameof(lists)) : With(o => o with { StandardCommands = lists.ToArray() });

    /// <summary>Installs the actions plugin.</summary>
    public PdfViewerOptionsBuilder Actions(ActionsConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Actions = config });

    /// <summary>Installs the signature plugin. Needs <see cref="Form"/>.</summary>
    public PdfViewerOptionsBuilder Signature(SignatureConfig config) => config is null ? throw new ArgumentNullException(nameof(config)) : With(o => o with { Signature = config });

    /// <summary>The transport for URL document sources.</summary>
    public PdfViewerOptionsBuilder DocumentFetcher(IDocumentFetcher fetcher) => fetcher is null ? throw new ArgumentNullException(nameof(fetcher)) : With(o => o with { DocumentFetcher = fetcher });

    /// <summary>Where diagnostics go.</summary>
    public PdfViewerOptionsBuilder Logger(ILogger logger) => logger is null ? throw new ArgumentNullException(nameof(logger)) : With(o => o with { Logger = logger });
}
