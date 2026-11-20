using GT = uniffi.epdf_plugin_attachment;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static AttachedFile ToPublic(GT.AttachedFile f) =>
        new(f.Id, f.Home switch
            {
                GT.AttachmentHome.Document d => new AttachmentHome.Document(new AttachmentRef(d.File.Key)),
                GT.AttachmentHome.Page p => new AttachmentHome.Page(ToPublic(p.Annotation)),
                _ => throw Unknown(f.Home),
            },
            f.Name, f.MimeType, f.Description, f.SizeBytes is { } size ? WireInteger.ToLong(size, "size") : null, f.Md5, f.CreatedAt, f.ModifiedAt);

    internal static IReadOnlyList<AttachedFile> ToPublic(GT.AttachedFile[] files) => [.. files.Select(ToPublic)];

    internal static GT.AttachedFile ToFfi(AttachedFile f) =>
        new(f.Id, f.Home switch
            {
                AttachmentHome.Document d => new GT.AttachmentHome.Document(new GT.AttachmentRef(d.File.Key)),
                AttachmentHome.Page p => new GT.AttachmentHome.Page(ToFfi(p.Annotation)),
                _ => throw Unknown(f.Home),
            },
            f.Name, f.MimeType, f.Description,
            f.SizeBytes is { } size ? (size >= 0 ? (ulong)size : throw new PdfException("RangeError", PdfErrorCode.InvalidInput, $"size {size} is negative")) : null,
            f.Md5, f.CreatedAt, f.ModifiedAt);

    internal static AttachmentEvent ToPublic(GT.AttachmentEvent e) => e switch
    {
        GT.AttachmentEvent.Changed c => new AttachmentEvent.Changed(c.Kind, ToPublic(c.Files), ToPublic(c.Origin)),
        GT.AttachmentEvent.Resynced r => new AttachmentEvent.Resynced(ToPublic(r.Files)),
        _ => throw Unknown(e),
    };
}
