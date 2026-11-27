namespace EmbedPDF;

/// <summary>What to search for. Leaving an option <see langword="null"/> takes the engine's default (off).</summary>
/// <param name="Text">The literal text, or with <see cref="Regex"/> a pattern in the portable dialect. Empty clears the session.</param>
public sealed record SearchQuery(string Text)
{
    /// <summary>Treat <see cref="Text"/> as a pattern.</summary>
    public bool? Regex { get; init; }
    /// <summary>Match letter case.</summary>
    public bool? MatchCase { get; init; }
    /// <summary>Match whole words only.</summary>
    public bool? WholeWord { get; init; }
    /// <summary>Match diacritics.</summary>
    public bool? MatchDiacritics { get; init; }
    /// <summary>Ignore whitespace differences.</summary>
    public bool? IgnoreWhitespace { get; init; }
}

/// <summary>One run of a hit's characters on a line.</summary>
/// <param name="Quad">Its corners in page space, y down.</param>
/// <param name="Rect">Its bounding rect in page space.</param>
/// <param name="Advance">The reading direction along the baseline: 1 or -1.</param>
public sealed record TextSegment(PdfQuad Quad, PdfRect Rect, double Advance);

/// <summary>Text around a hit.</summary>
/// <param name="Before">Text before the match.</param>
/// <param name="Matched">The match.</param>
/// <param name="After">Text after the match.</param>
public sealed record SearchSnippet(string Before, string Matched, string After);

/// <summary>One match.</summary>
/// <param name="Page">Its page.</param>
/// <param name="PageIndex">Its page's index, from 0.</param>
/// <param name="Start">The first of the page's characters it covers: a character index as geometry and selection use it, not a UTF-16 offset into extracted text.</param>
/// <param name="Count">How many characters it covers.</param>
/// <param name="Segments">Its runs, one per line.</param>
/// <param name="Bounds">The union of the segments; <see langword="null"/> when the match has none.</param>
/// <param name="Snippet">Text around it, when snippets were asked for and allowed.</param>
public sealed record SearchHit(PageRef Page, int PageIndex, double Start, double Count, IReadOnlyList<TextSegment> Segments, PdfRect? Bounds, SearchSnippet? Snippet);

/// <summary>Where the session stands.</summary>
public enum SearchStatus
{
    /// <summary>No search.</summary>
    Idle,
    /// <summary>Scanning.</summary>
    Searching,
    /// <summary>The scan finished.</summary>
    Complete,
    /// <summary>The scan was cancelled; the hits found so far stay.</summary>
    Cancelled,
    /// <summary>The scan failed; <see cref="SearchCapability.GetErrorAsync"/> says why.</summary>
    Error,
}

/// <summary>How a scan ended.</summary>
public enum SearchResultStatus
{
    /// <summary>Every page was scanned.</summary>
    Complete,
    /// <summary>A newer search replaced it.</summary>
    Superseded,
    /// <summary>It was cancelled.</summary>
    Cancelled,
}

/// <summary>How a search returned.</summary>
/// <param name="Status">How the scan ended.</param>
/// <param name="HitCount">Hits found.</param>
public sealed record SearchResult(SearchResultStatus Status, int HitCount);

/// <summary>How far the scan has come.</summary>
/// <param name="PagesSearched">Pages scanned.</param>
/// <param name="PageCount">Pages to scan.</param>
public sealed record SearchProgress(double PagesSearched, double PageCount);

/// <summary>Why a scan stopped early.</summary>
public enum SearchCancelReason
{
    /// <summary>A newer search replaced it.</summary>
    Superseded,
    /// <summary>It was cancelled.</summary>
    Cancelled,
    /// <summary>The session was cleared.</summary>
    Cleared,
}

/// <summary>Where a revealed hit lands along one axis of the viewport.</summary>
public abstract record SearchRevealAxis
{
    private protected SearchRevealAxis() { }

    /// <summary>Don't scroll along this axis if the hit is visible.</summary>
    public sealed record Keep : SearchRevealAxis;

    /// <summary>At the start edge.</summary>
    public sealed record Start : SearchRevealAxis;

    /// <summary>Centered.</summary>
    public sealed record Center : SearchRevealAxis;

    /// <summary>At the end edge.</summary>
    public sealed record End : SearchRevealAxis;

    /// <summary>At a fraction of the viewport, 0 at the start edge and 1 at the end.</summary>
    /// <param name="Value">The fraction.</param>
    public sealed record Fraction(double Value) : SearchRevealAxis;
}

/// <summary>Where a revealed hit lands; a <see langword="null"/> axis takes the default.</summary>
public sealed record SearchRevealAnchor
{
    /// <summary>The horizontal placement.</summary>
    public SearchRevealAxis? X { get; init; }
    /// <summary>The vertical placement.</summary>
    public SearchRevealAxis? Y { get; init; }
}

/// <summary>How the stage scrolls to a hit.</summary>
public enum SearchScrollBehavior
{
    /// <summary>Animated.</summary>
    Smooth,
    /// <summary>At once.</summary>
    Instant,
}

/// <summary>How navigating to a hit reveals it; <see langword="null"/> fields take the settings.</summary>
public sealed record SearchRevealOptions
{
    /// <summary>Where the hit lands.</summary>
    public SearchRevealAnchor? Anchor { get; init; }
    /// <summary>How the stage scrolls.</summary>
    public SearchScrollBehavior? Behavior { get; init; }
}

/// <summary>How highlights blend with the page.</summary>
public enum SearchBlendMode
{
    /// <summary>Normal.</summary>
    Normal,
    /// <summary>Multiply.</summary>
    Multiply,
    /// <summary>Screen.</summary>
    Screen,
    /// <summary>Overlay.</summary>
    Overlay,
    /// <summary>Darken.</summary>
    Darken,
    /// <summary>Lighten.</summary>
    Lighten,
    /// <summary>Color dodge.</summary>
    ColorDodge,
    /// <summary>Color burn.</summary>
    ColorBurn,
    /// <summary>Hard light.</summary>
    HardLight,
    /// <summary>Soft light.</summary>
    SoftLight,
    /// <summary>Difference.</summary>
    Difference,
    /// <summary>Exclusion.</summary>
    Exclusion,
    /// <summary>Hue.</summary>
    Hue,
    /// <summary>Saturation.</summary>
    Saturation,
    /// <summary>Color.</summary>
    Color,
    /// <summary>Luminosity.</summary>
    Luminosity,
}

/// <summary>Reveal settings.</summary>
/// <param name="Anchor">Where a revealed hit lands, or <see langword="null"/> for the default.</param>
/// <param name="Behavior">How the stage scrolls.</param>
public sealed record SearchRevealSettings(SearchRevealAnchor? Anchor, SearchScrollBehavior Behavior);

/// <summary>Highlight settings.</summary>
/// <param name="Color">Hit color.</param>
/// <param name="ActiveColor">The active hit's color.</param>
/// <param name="BlendMode">How highlights blend.</param>
public sealed record SearchHighlightSettings(string Color, string ActiveColor, SearchBlendMode BlendMode);

/// <summary>Search settings, shared by every document using the plugin registration.</summary>
/// <param name="Reveal">Reveal settings.</param>
/// <param name="Highlight">Highlight settings.</param>
public sealed record SearchSettings(SearchRevealSettings Reveal, SearchHighlightSettings Highlight);

/// <summary>A change to <see cref="SearchRevealSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record SearchRevealPatch
{
    /// <summary>New anchor.</summary>
    public SearchRevealAnchor? Anchor { get; init; }
    /// <summary>Remove the anchor.</summary>
    public bool ClearAnchor { get; init; }
    /// <summary>New behavior.</summary>
    public SearchScrollBehavior? Behavior { get; init; }
}

/// <summary>A change to <see cref="SearchHighlightSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record SearchHighlightPatch
{
    /// <summary>New hit color.</summary>
    public string? Color { get; init; }
    /// <summary>New active hit color.</summary>
    public string? ActiveColor { get; init; }
    /// <summary>New blend mode.</summary>
    public SearchBlendMode? BlendMode { get; init; }
}

/// <summary>A change to <see cref="SearchSettings"/>. Clear flags remove nullable values; omission preserves them.</summary>
public sealed record SearchSettingsPatch
{
    /// <summary>Reveal changes.</summary>
    public SearchRevealPatch? Reveal { get; init; }
    /// <summary>Highlight changes.</summary>
    public SearchHighlightPatch? Highlight { get; init; }
}

/// <summary>An event from <see cref="SearchCapability.ObserveEvents"/>.</summary>
public abstract record SearchEvent
{
    private protected SearchEvent() { }

    /// <summary>A scan started.</summary>
    /// <param name="Query">Its query.</param>
    public sealed record Started(SearchQuery Query) : SearchEvent;

    /// <summary>The scan moved on.</summary>
    /// <param name="PagesSearched">Pages scanned.</param>
    /// <param name="PageCount">Pages to scan.</param>
    /// <param name="HitCount">Hits so far.</param>
    public sealed record ProgressChanged(double PagesSearched, double PageCount, int HitCount) : SearchEvent;

    /// <summary>The scan finished.</summary>
    /// <param name="HitCount">Hits found.</param>
    public sealed record Completed(int HitCount) : SearchEvent;

    /// <summary>The scan stopped early.</summary>
    /// <param name="Reason">Why.</param>
    public sealed record Cancelled(SearchCancelReason Reason) : SearchEvent;

    /// <summary>The scan failed.</summary>
    /// <param name="Error">Why.</param>
    public sealed record Failed(PluginErrorInfo Error) : SearchEvent;

    /// <summary>The active hit changed.</summary>
    /// <param name="Index">Its index, or <see langword="null"/> for none.</param>
    /// <param name="Hit">The hit, or <see langword="null"/> for none.</param>
    public sealed record ActiveHitChanged(int? Index, SearchHit? Hit) : SearchEvent;
}

/// <summary>Installs the search plugin: text search, a session per document.</summary>
public sealed partial record SearchConfig
{
    /// <summary>How <see cref="SearchCapability.NextHitAsync"/>, <see cref="SearchCapability.PreviousHitAsync"/> and <see cref="SearchCapability.GoToHitAsync"/> reveal a hit; <see langword="null"/> is the engine's default.</summary>
    public SearchRevealOptions? Reveal { get; init; }
}
