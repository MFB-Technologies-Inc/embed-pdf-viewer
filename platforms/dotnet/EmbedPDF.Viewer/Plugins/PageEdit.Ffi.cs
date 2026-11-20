using GP = uniffi.epdf_plugin_page_edit;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static GP.PageEditPlacement ToFfi(PageEditPlacement placement) => placement switch
    {
        PageEditPlacement.After a => new GP.PageEditPlacement.After(ToFfi(a.Page)),
        PageEditPlacement.Before b => new GP.PageEditPlacement.Before(ToFfi(b.Page)),
        PageEditPlacement.Start => new GP.PageEditPlacement.Start(),
        PageEditPlacement.End => new GP.PageEditPlacement.End(),
        _ => throw Unknown(placement),
    };

    internal static GP.PageEditTurn ToFfi(PageEditTurn turn) => turn switch
    {
        PageEditTurn.Clockwise => GP.PageEditTurn.Clockwise,
        PageEditTurn.CounterClockwise => GP.PageEditTurn.CounterClockwise,
        _ => GP.PageEditTurn.Half,
    };

    internal static GP.PageEditRotation ToFfi(PageEditRotation rotation) => rotation switch
    {
        PageEditRotation.None => GP.PageEditRotation.None,
        PageEditRotation.Clockwise90 => GP.PageEditRotation.Clockwise90,
        PageEditRotation.Clockwise180 => GP.PageEditRotation.Clockwise180,
        _ => GP.PageEditRotation.Clockwise270,
    };

    internal static GP.PageEditBlankOptions ToFfi(PageEditBlankOptions o) =>
        new(WireInteger.ToUInt32(o.Count, "count"), o.Size is { } size ? ToFfi(size) : null, o.Placement is null ? null : ToFfi(o.Placement));

    internal static GP.PageEditBytesOptions ToFfi(PageEditBytesOptions o) =>
        new(o.PageIndexes is null ? null : [.. o.PageIndexes.Select(i => WireInteger.ToUInt32(i, "page index"))], o.Placement is null ? null : ToFfi(o.Placement));
}
