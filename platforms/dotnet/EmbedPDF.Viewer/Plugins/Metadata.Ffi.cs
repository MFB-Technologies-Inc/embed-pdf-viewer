using GM = uniffi.epdf_plugin_metadata;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static DocumentMetadataTrapped ToPublic(GM.DocumentMetadataTrapped trapped) => trapped switch
    {
        GM.DocumentMetadataTrapped.True => DocumentMetadataTrapped.True,
        GM.DocumentMetadataTrapped.False => DocumentMetadataTrapped.False,
        _ => DocumentMetadataTrapped.Unknown,
    };

    internal static GM.DocumentMetadataTrapped ToFfi(DocumentMetadataTrapped trapped) => trapped switch
    {
        DocumentMetadataTrapped.True => GM.DocumentMetadataTrapped.True,
        DocumentMetadataTrapped.False => GM.DocumentMetadataTrapped.False,
        DocumentMetadataTrapped.Unknown => GM.DocumentMetadataTrapped.Unknown,
        _ => throw Undefined(trapped, "trapped"),
    };

    internal static DocumentMetadata ToPublic(GM.DocumentMetadata m) =>
        new(m.Title, m.Author, m.Subject, m.Keywords, m.Producer, m.Creator, m.CreatedAt, m.ModifiedAt, ToPublic(m.Trapped));

    internal static DocumentMetadata? ToPublicOrNull(GM.DocumentMetadata? m) => m is null ? null : ToPublic(m);

    internal static GM.MetadataField ToFfi(MetadataField field) => field switch
    {
        MetadataField.Title => GM.MetadataField.Title,
        MetadataField.Author => GM.MetadataField.Author,
        MetadataField.Subject => GM.MetadataField.Subject,
        MetadataField.Keywords => GM.MetadataField.Keywords,
        MetadataField.Producer => GM.MetadataField.Producer,
        MetadataField.Creator => GM.MetadataField.Creator,
        MetadataField.CreatedAt => GM.MetadataField.CreatedAt,
        MetadataField.ModifiedAt => GM.MetadataField.ModifiedAt,
        _ => throw Undefined(field, "metadata field"),
    };

    internal static GM.MetadataPatch ToFfi(MetadataPatch p) =>
        new(p.Title, p.Author, p.Subject, p.Keywords, p.Producer, p.Creator, p.CreatedAt, p.ModifiedAt,
            p.Trapped is { } trapped ? ToFfi(trapped) : null, [.. p.Clear.Select(ToFfi)]);

    internal static MetadataEvent ToPublic(GM.MetadataEvent e) => e switch
    {
        GM.MetadataEvent.Updated u => new MetadataEvent.Updated(new MetadataUpdatedEvent(ToPublic(u.Event.Metadata), ToPublicOrNull(u.Event.Previous), u.Event.ChangedKeys)),
        GM.MetadataEvent.Resynced r => new MetadataEvent.Resynced(ToPublic(r.Metadata)),
        GM.MetadataEvent.CustomUpdated c => new MetadataEvent.CustomUpdated(new CustomMetadataUpdatedEvent(c.Event.Custom, c.Event.Previous, c.Event.ChangedKeys)),
        GM.MetadataEvent.CustomResynced c => new MetadataEvent.CustomResynced(c.Custom),
        _ => throw Unknown(e),
    };
}
