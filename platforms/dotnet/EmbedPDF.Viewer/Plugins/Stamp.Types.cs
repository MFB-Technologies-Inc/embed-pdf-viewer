namespace EmbedPDF;

/// <summary>A stamp add asset input.</summary>
public sealed record StampAddAssetInput
{
    /// <summary>The library id.</summary>
    public string? LibraryId { get; init; }
    /// <summary>The name.</summary>
    public string? Name { get; init; }
    /// <summary>The label.</summary>
    public string? Label { get; init; }
    /// <summary>The kind.</summary>
    public StampAssetKind? Kind { get; init; }
    /// <summary>The subject.</summary>
    public string? Subject { get; init; }
    /// <summary>The categories.</summary>
    public IReadOnlyList<string>? Categories { get; init; }
    /// <summary>The artwork as bytes (PDF, PNG or JPEG). Give this or mark.</summary>
    public ReadOnlyMemory<byte>? Source { get; init; }
    /// <summary>The mark.</summary>
    public StampMark? Mark { get; init; }
    /// <summary>The preview.</summary>
    public ReadOnlyMemory<byte>? Preview { get; init; }
    /// <summary>The size.</summary>
    public PdfSize? Size { get; init; }
}

/// <summary>A stamp asset.</summary>
/// <param name="Id">libraryId:name, stable.</param>
/// <param name="LibraryId">The library id.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Name">The name.</param>
/// <param name="Label">The label.</param>
/// <param name="Size">Intrinsic size in PDF points.</param>
/// <param name="Page">The library page that is this asset.</param>
/// <param name="Subject">The subject.</param>
/// <param name="Categories">The categories.</param>
public sealed record StampAsset(string Id, string LibraryId, StampAssetKind Kind, string Name, string Label, PdfSize Size, PageRef Page, string? Subject, IReadOnlyList<string>? Categories);

/// <summary>A stamp asset filter.</summary>
public sealed record StampAssetFilter
{
    /// <summary>The library id.</summary>
    public string? LibraryId { get; init; }
    /// <summary>The kind.</summary>
    public StampAssetKind? Kind { get; init; }
    /// <summary>The category.</summary>
    public string? Category { get; init; }
}

/// <summary>clear_subject removes the subject override; subject sets it.</summary>
public sealed record StampAssetPatch
{
    /// <summary>The label.</summary>
    public string? Label { get; init; }
    /// <summary>The subject.</summary>
    public string? Subject { get; init; }
    /// <summary>The clear subject.</summary>
    public bool ClearSubject { get; init; }
    /// <summary>The categories.</summary>
    public IReadOnlyList<string>? Categories { get; init; }
}

/// <summary>A stamp batch failure.</summary>
/// <param name="Page">The page.</param>
/// <param name="Error">The error.</param>
public sealed record StampBatchFailure(PageRef Page, StampErrorInfo Error);

/// <summary>A stamp batch result.</summary>
/// <param name="Applied">The placed annotations' refs.</param>
/// <param name="Skipped">The skipped.</param>
/// <param name="Failed">The failed.</param>
public sealed record StampBatchResult(IReadOnlyList<AnnotationRef> Applied, IReadOnlyList<PageRef> Skipped, IReadOnlyList<StampBatchFailure> Failed);

/// <summary>A stamp create library options.</summary>
public sealed record StampCreateLibraryOptions
{
    /// <summary>The id.</summary>
    public string? Id { get; init; }
    /// <summary>The kind.</summary>
    public string? Kind { get; init; }
    /// <summary>The categories.</summary>
    public IReadOnlyList<string>? Categories { get; init; }
}

/// <summary>A stamp error info.</summary>
/// <param name="Code">The code.</param>
/// <param name="Detail">The detail.</param>
public sealed record StampErrorInfo(string Code, string Detail);

/// <summary>asset_name (per-page labels from a closure) is not exported; a plain PDF's assets are labelled from their page index.</summary>
public sealed record StampImportOptions
{
    /// <summary>Fallback name when the PDF has no /Title. <see langword="null"/> is Stamps.</summary>
    public string? Name { get; init; }
    /// <summary>The categories.</summary>
    public IReadOnlyList<string>? Categories { get; init; }
    /// <summary>The kind.</summary>
    public StampAssetKind? Kind { get; init; }
    /// <summary>The library kind.</summary>
    public string? LibraryKind { get; init; }
    /// <summary>The library's id, over the one the file names; a fresh one when left out or taken.</summary>
    public string? Id { get; init; }
}

/// <summary>A library: a PDF whose pages are its assets.</summary>
/// <param name="Id">Stable while the title is editable.</param>
/// <param name="Name">The name.</param>
/// <param name="Kind">stamps, signatures, or an embedder's own.</param>
/// <param name="AssetIds">Asset ids in display order.</param>
/// <param name="Locale">The locale.</param>
/// <param name="Categories">The categories.</param>
public sealed record StampLibrary(string Id, string Name, string Kind, IReadOnlyList<string> AssetIds, string? Locale, IReadOnlyList<string>? Categories);

/// <summary>A stamp library patch.</summary>
public sealed record StampLibraryPatch
{
    /// <summary>The name.</summary>
    public string? Name { get; init; }
    /// <summary>The categories.</summary>
    public IReadOnlyList<string>? Categories { get; init; }
}

/// <summary>A stamp settings.</summary>
/// <param name="PreviewWidth">The preview width.</param>
/// <param name="Dynamic">The dynamic.</param>
public sealed record StampSettings(double PreviewWidth, bool Dynamic);

/// <summary>A stored stamp library.</summary>
/// <param name="Id">The id.</param>
/// <param name="Bytes">The bytes.</param>
public sealed record StoredStampLibrary(string Id, ReadOnlyMemory<byte> Bytes);

/// <summary>A stamp event.</summary>
public abstract record StampEvent
{
    private protected StampEvent() { }

    /// <summary>A library's canonical bytes changed: persist it.</summary>
    /// <param name="LibraryId">The library id.</param>
    /// <param name="Reason">The reason.</param>
    public sealed record LibraryChanged(string LibraryId, StampLibraryChangeReason Reason) : StampEvent;

    /// <summary>Library created.</summary>
    /// <param name="LibraryId">The library id.</param>
    /// <param name="Library">The library.</param>
    public sealed record LibraryCreated(string LibraryId, StampLibrary? Library) : StampEvent;

    /// <summary>Library updated.</summary>
    /// <param name="LibraryId">The library id.</param>
    /// <param name="Library">The library.</param>
    public sealed record LibraryUpdated(string LibraryId, StampLibrary? Library) : StampEvent;

    /// <summary>Library deleted.</summary>
    /// <param name="LibraryId">The library id.</param>
    public sealed record LibraryDeleted(string LibraryId) : StampEvent;

    /// <summary>Asset created.</summary>
    /// <param name="AssetId">The asset id.</param>
    /// <param name="LibraryId">The library id.</param>
    /// <param name="Asset">The asset.</param>
    public sealed record AssetCreated(string AssetId, string LibraryId, StampAsset? Asset) : StampEvent;

    /// <summary>Asset updated.</summary>
    /// <param name="AssetId">The asset id.</param>
    /// <param name="LibraryId">The library id.</param>
    /// <param name="Asset">The asset.</param>
    public sealed record AssetUpdated(string AssetId, string LibraryId, StampAsset? Asset) : StampEvent;

    /// <summary>Asset deleted.</summary>
    /// <param name="AssetId">The asset id.</param>
    /// <param name="LibraryId">The library id.</param>
    public sealed record AssetDeleted(string AssetId, string LibraryId) : StampEvent;

    /// <summary>A stamp was armed on the document, or disarmed (<see langword="null"/>).</summary>
    /// <param name="DocumentId">The document id.</param>
    /// <param name="Asset">The asset.</param>
    public sealed record ArmChanged(string DocumentId, StampAsset? Asset) : StampEvent;

}

/// <summary>How a new asset's artwork is authored.</summary>
public abstract record StampMark
{
    private protected StampMark() { }

    /// <summary>Strokes in PDF points.</summary>
    /// <param name="Strokes">The strokes.</param>
    /// <param name="Color">The color.</param>
    /// <param name="StrokeWidth">The stroke width.</param>
    public sealed record Ink(IReadOnlyList<IReadOnlyList<PdfPoint>> Strokes, string? Color, double? StrokeWidth) : StampMark;

    /// <summary>Text.</summary>
    /// <param name="TextValue">The text value.</param>
    /// <param name="FontFamily">The font family.</param>
    /// <param name="Color">The color.</param>
    /// <param name="FontSize">The font size.</param>
    public sealed record Text(string TextValue, string FontFamily, string? Color, double? FontSize) : StampMark;

    /// <summary>PNG or JPEG bytes.</summary>
    /// <param name="Bytes">The bytes.</param>
    public sealed record Image(ReadOnlyMemory<byte> Bytes) : StampMark;

    /// <summary>One page of a PDF.</summary>
    /// <param name="Bytes">The bytes.</param>
    /// <param name="PageIndex">The page index.</param>
    public sealed record Pdf(ReadOnlyMemory<byte> Bytes, int? PageIndex) : StampMark;

}

/// <summary>StampAssetKind.</summary>
public enum StampAssetKind
{
    /// <summary>Stamp.</summary>
    Stamp,
    /// <summary>Signature.</summary>
    Signature,
    /// <summary>Initials.</summary>
    Initials,
}

/// <summary>StampLibraryChangedEvent['type'].</summary>
public enum StampLibraryChangeReason
{
    /// <summary>Created.</summary>
    Created,
    /// <summary>Imported.</summary>
    Imported,
    /// <summary>Updated.</summary>
    Updated,
    /// <summary>Asset created.</summary>
    AssetCreated,
    /// <summary>Asset updated.</summary>
    AssetUpdated,
    /// <summary>Asset deleted.</summary>
    AssetDeleted,
    /// <summary>Deleted.</summary>
    Deleted,
}
