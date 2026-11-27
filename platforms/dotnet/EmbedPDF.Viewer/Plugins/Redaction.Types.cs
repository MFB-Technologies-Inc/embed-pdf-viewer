namespace EmbedPDF;

/// <summary>A redaction apply result.</summary>
/// <param name="Scope">The scope.</param>
/// <param name="Results">The results.</param>
/// <param name="RemovedAnnotationCount">The removed annotation count.</param>
/// <param name="OpId">The mutation's operation id (meta.opId).</param>
public sealed record RedactionApplyResult(RedactionApplyScope Scope, IReadOnlyList<RedactionPageResult> Results, int RemovedAnnotationCount, string? OpId);

/// <summary>What an apply would destroy beyond the marks: a client-side estimate. The apply result is authoritative.</summary>
/// <param name="Count">The count.</param>
/// <param name="Refs">The refs.</param>
public sealed record RedactionCollateral(int Count, IReadOnlyList<AnnotationRef> Refs);

/// <summary>A label edit. overlay_text sets the label, clear_overlay_text removes it (setting both is refused), and repeat tiles it across the region.</summary>
public sealed record RedactionLabelPatch
{
    /// <summary>The overlay text.</summary>
    public string? OverlayText { get; init; }
    /// <summary>The clear overlay text.</summary>
    public bool ClearOverlayText { get; init; }
    /// <summary>The repeat.</summary>
    public bool? Repeat { get; init; }
}

/// <summary>One pending mark: a live projection of a redact annotation. Delete the annotation and the mark is gone.</summary>
/// <param name="AnnotationRef">The annotation ref.</param>
/// <param name="Page">The page.</param>
/// <param name="PageIndex">Display index; <see langword="null"/> when the page registry lacks the page.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Bounds">Page space.</param>
/// <param name="OverlayText">/OverlayText, when set.</param>
/// <param name="Repeat">Whether the label tiles the area.</param>
public sealed record RedactionMark(AnnotationRef AnnotationRef, PageRef Page, int? PageIndex, RedactionMarkKind Kind, PdfRect Bounds, string? OverlayText, bool Repeat);

/// <summary>A redaction overlay text.</summary>
public sealed record RedactionOverlayText
{
    /// <summary>CSS hex colour of the label. White when <see langword="null"/>.</summary>
    public string? Color { get; init; }
    /// <summary>The font family.</summary>
    public string? FontFamily { get; init; }
    /// <summary>Points; 0 fits the label to the region.</summary>
    public double? FontSize { get; init; }
}

/// <summary>A redaction page result.</summary>
/// <param name="Page">The page.</param>
/// <param name="Status">The status.</param>
/// <param name="RemovedAnnotationCount">Annotations removed alongside the marks (popups, detached widgets), not the marks themselves.</param>
public sealed record RedactionPageResult(PageRef Page, RedactionPageStatus Status, int RemovedAnnotationCount);

/// <summary>A redaction settings.</summary>
/// <param name="Fill">The fill.</param>
/// <param name="OverlayText">The overlay text.</param>
public sealed record RedactionSettings(string Fill, RedactionTextSettings OverlayText);

/// <summary>A redaction skipped.</summary>
/// <param name="AnnotationRef">The annotation ref.</param>
/// <param name="Reason">The reason.</param>
public sealed record RedactionSkipped(AnnotationRef AnnotationRef, string Reason);

/// <summary>A redaction text settings.</summary>
/// <param name="Color">The color.</param>
/// <param name="FontFamily">The font family.</param>
/// <param name="FontSize">The font size.</param>
public sealed record RedactionTextSettings(string Color, string FontFamily, double FontSize);

/// <summary>A mark unmark could not remove, with the PluginErrorInfo's members.</summary>
/// <param name="AnnotationRef">The annotation ref.</param>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
/// <param name="Capability">The capability.</param>
/// <param name="Permission">The permission.</param>
public sealed record RedactionUnmarkFailure(AnnotationRef AnnotationRef, string Code, string Message, string Capability, string? Permission);

/// <summary>What unmark and clear_pending did: marks removed, refs that weren't pending marks, and marks that could not be removed.</summary>
/// <param name="Applied">The applied.</param>
/// <param name="Skipped">The skipped.</param>
/// <param name="Failed">The failed.</param>
public sealed record RedactionUnmarkResult(IReadOnlyList<AnnotationRef> Applied, IReadOnlyList<RedactionSkipped> Skipped, IReadOnlyList<RedactionUnmarkFailure> Failed);

/// <summary>A redaction apply scope.</summary>
public abstract record RedactionApplyScope
{
    private protected RedactionApplyScope() { }

    /// <summary>Every mark on each page.</summary>
    /// <param name="PagesValue">The pages value.</param>
    public sealed record Pages(IReadOnlyList<PageRef> PagesValue) : RedactionApplyScope;

    /// <summary>Exactly these marks.</summary>
    /// <param name="Refs">The refs.</param>
    public sealed record Annotations(IReadOnlyList<AnnotationRef> Refs) : RedactionApplyScope;

}

/// <summary>A redaction event.</summary>
public abstract record RedactionEvent
{
    private protected RedactionEvent() { }

    /// <summary>A confirmed apply, this session's or a remote one.</summary>
    /// <param name="Result">The result.</param>
    public sealed record Applied(RedactionApplyResult Result) : RedactionEvent;

    /// <summary>Marks were created, changed or removed on these pages.</summary>
    /// <param name="Pages">The pages.</param>
    public sealed record PendingChanged(IReadOnlyList<PageRef> Pages) : RedactionEvent;

}

/// <summary>A redaction mark kind.</summary>
public enum RedactionMarkKind
{
    /// <summary>A rectangle (a marquee, a whole page).</summary>
    Area,
    /// <summary>Per-line quads over text (a selection, a search hit).</summary>
    Text,
}

/// <summary>A redaction page status.</summary>
public enum RedactionPageStatus
{
    /// <summary>Applied.</summary>
    Applied,
    /// <summary>The page had no marks, which is not a failure.</summary>
    Unchanged,
}
