using GF = uniffi.epdf_ffi;
using GS = uniffi.epdf_plugin_search;
using GSp = uniffi.epdf_plugin_stamp;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static GSp.StampAddAssetInput ToFfi(StampAddAssetInput r) => r is null ? throw new ArgumentException("A stamp add asset input is required.") : new(LibraryId: r.LibraryId, Name: r.Name, Label: r.Label, Kind: r.Kind is { } x3 ? (GSp.StampAssetKind)(int)Defined(x3, "kind") : null, Subject: r.Subject, Categories: r.Categories is null ? null : r.Categories.Select(x7 => x7 ?? throw new ArgumentException("categories is required.")).ToArray(), Source: r.Source is { } x9 ? Bytes.Array(x9) : null, Mark: r.Mark is null ? null : ToFfi(r.Mark), Preview: r.Preview is { } x12 ? Bytes.Array(x12) : null, Size: r.Size is { } x13 ? ToFfi(x13) : null);

    internal static StampAsset ToPublic(GSp.StampAsset r) => new(r.Id, r.LibraryId, (StampAssetKind)(int)r.Kind, r.Name, r.Label, ToPublic(r.Size), ToPublic(r.Page), r.Subject, r.Categories is null ? null : r.Categories.Select(x24 => x24).ToList());

    internal static GSp.StampAssetFilter ToFfi(StampAssetFilter r) => r is null ? throw new ArgumentException("A stamp asset filter is required.") : new(LibraryId: r.LibraryId, Kind: r.Kind is { } x27 ? (GSp.StampAssetKind)(int)Defined(x27, "kind") : null, Category: r.Category);

    internal static GSp.StampAssetPatch ToFfi(StampAssetPatch r) => r is null ? throw new ArgumentException("A stamp asset patch is required.") : new(Label: r.Label, Subject: r.Subject, ClearSubject: r.ClearSubject, Categories: r.Categories is null ? null : r.Categories.Select(x34 => x34 ?? throw new ArgumentException("categories is required.")).ToArray());

    internal static StampBatchFailure ToPublic(GSp.StampBatchFailure r) => new(ToPublic(r.Page), ToPublic(r.Error));

    internal static StampBatchResult ToPublic(GSp.StampBatchResult r) => new([.. r.Applied.Select(x38 => ToPublic(x38))], [.. r.Skipped.Select(x40 => ToPublic(x40))], [.. r.Failed.Select(x42 => ToPublic(x42))]);

    internal static GSp.StampCreateLibraryOptions ToFfi(StampCreateLibraryOptions r) => r is null ? throw new ArgumentException("A stamp create library options is required.") : new(Id: r.Id, Kind: r.Kind, Categories: r.Categories is null ? null : r.Categories.Select(x47 => x47 ?? throw new ArgumentException("categories is required.")).ToArray());

    internal static StampErrorInfo ToPublic(GSp.StampErrorInfo r) => new(r.Code, r.Detail);

    internal static GSp.StampImportOptions ToFfi(StampImportOptions r) => r is null ? throw new ArgumentException("A stamp import options is required.") : new(Name: r.Name, Categories: r.Categories is null ? null : r.Categories.Select(x53 => x53 ?? throw new ArgumentException("categories is required.")).ToArray(), Kind: r.Kind is { } x55 ? (GSp.StampAssetKind)(int)Defined(x55, "kind") : null, LibraryKind: r.LibraryKind, Id: r.Id);

    internal static StampLibrary ToPublic(GSp.StampLibrary r) => new(r.Id, r.Name, r.Kind, [.. r.AssetIds.Select(x62 => x62)], r.Locale, r.Categories is null ? null : r.Categories.Select(x66 => x66).ToList());

    internal static GSp.StampLibraryPatch ToFfi(StampLibraryPatch r) => r is null ? throw new ArgumentException("A stamp library patch is required.") : new(Name: r.Name, Categories: r.Categories is null ? null : r.Categories.Select(x70 => x70 ?? throw new ArgumentException("categories is required.")).ToArray());

    internal static StampSettings ToPublic(GSp.StampSettings r) => new(r.PreviewWidth, r.Dynamic);

    internal static StoredStampLibrary ToPublic(GSp.StoredStampLibrary r) => new(r.Id, r.Bytes);

    internal static GSp.StoredStampLibrary ToFfi(StoredStampLibrary r) => r is null ? throw new ArgumentException("A stored stamp library is required.") : new(Id: r.Id ?? throw new ArgumentException("id is required."), Bytes: Bytes.Array(r.Bytes));

    internal static StampEvent ToPublic(GSp.StampEvent u) => u switch
    {
        GSp.StampEvent.LibraryChanged c => new StampEvent.LibraryChanged(c.LibraryId, (StampLibraryChangeReason)(int)c.Reason),
        GSp.StampEvent.LibraryCreated c => new StampEvent.LibraryCreated(c.LibraryId, c.Library is null ? null : ToPublic(c.Library)),
        GSp.StampEvent.LibraryUpdated c => new StampEvent.LibraryUpdated(c.LibraryId, c.Library is null ? null : ToPublic(c.Library)),
        GSp.StampEvent.LibraryDeleted c => new StampEvent.LibraryDeleted(c.LibraryId),
        GSp.StampEvent.AssetCreated c => new StampEvent.AssetCreated(c.AssetId, c.LibraryId, c.Asset is null ? null : ToPublic(c.Asset)),
        GSp.StampEvent.AssetUpdated c => new StampEvent.AssetUpdated(c.AssetId, c.LibraryId, c.Asset is null ? null : ToPublic(c.Asset)),
        GSp.StampEvent.AssetDeleted c => new StampEvent.AssetDeleted(c.AssetId, c.LibraryId),
        GSp.StampEvent.ArmChanged c => new StampEvent.ArmChanged(c.DocumentId, c.Asset is null ? null : ToPublic(c.Asset)),
        _ => throw Unknown(u),
    };

    internal static GSp.StampMark ToFfi(StampMark u) => u switch
    {
        StampMark.Ink c => new GSp.StampMark.Ink([.. Arguments.NoNullElements(c.Strokes, "strokes").Select(x100 => x100.Select(x101 => ToFfi(x101)).ToArray())], c.Color, c.StrokeWidth),
        StampMark.Text c => new GSp.StampMark.Text(c.TextValue ?? throw new ArgumentException("text value is required."), c.FontFamily ?? throw new ArgumentException("font family is required."), c.Color, c.FontSize),
        StampMark.Image c => new GSp.StampMark.Image(Bytes.Array(c.Bytes)),
        StampMark.Pdf c => new GSp.StampMark.Pdf(Bytes.Array(c.Bytes), c.PageIndex is { } x111 ? WireInteger.ToUInt32(x111, "page index") : null),
        null => throw new ArgumentException("A stamp mark is required."),
        _ => throw Unknown(u),
    };
}
