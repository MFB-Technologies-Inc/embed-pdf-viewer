using GO = uniffi.epdf_plugin_outline;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static GO.OutlineItemRef ToFfi(OutlineItemRef item) => new(WireInteger.ToUInt32(item.ObjectNumber, "outline object number"));

    internal static GO.OutlineItemRef? ToFfiOrNull(OutlineItemRef? item) => item is { } value ? ToFfi(value) : null;

    internal static GO.OutlineItemRef[] ToFfi(IEnumerable<OutlineItemRef> items) => [.. items.Select(ToFfi)];

    internal static OutlineItemRef ToPublic(GO.OutlineItemRef item) => new(WireInteger.ToInt(item.ObjectNumber, "outline object number"));

    internal static OutlineItemRef? ToPublicOrNull(GO.OutlineItemRef? item) => item is null ? null : ToPublic(item);

    internal static IReadOnlyList<OutlineItemRef> ToPublic(GO.OutlineItemRef[] items) => [.. items.Select(ToPublic)];

    /// <summary>The public item takes over the action tree's native handle.</summary>
    internal static OutlineItem ToPublic(GO.OutlineItem i) =>
        new(ToPublic(i.ItemRef), ToPublicOrNull(i.Parent), i.Title, ToPublicOrNull(i.Target),
            i.Action is null ? null : new PdfActionTree(i.Action), i.Open, i.Color, i.Bold, i.Italic);

    internal static IReadOnlyList<OutlineItem> ToPublic(GO.OutlineItem[] items) => [.. items.Select(ToPublic)];

    internal static OutlineRow ToPublic(GO.OutlineRow r) => new(ToPublic(r.Item), WireInteger.ToInt(r.Depth, "depth"), r.HasChildren, r.Expanded);

    internal static GO.OutlinePosition ToFfi(OutlinePosition p) => p switch
    {
        OutlinePosition.Start => new GO.OutlinePosition.Start(),
        OutlinePosition.End => new GO.OutlinePosition.End(),
        OutlinePosition.Before b => new GO.OutlinePosition.Before(ToFfi(b.Item)),
        OutlinePosition.After a => new GO.OutlinePosition.After(ToFfi(a.Item)),
        _ => throw Unknown(p),
    };

    internal static GO.OutlineItemInput ToFfi(OutlineItemInput i) =>
        new(i.Title, ToFfiOrNull(i.Parent), i.Position is null ? null : ToFfi(i.Position), ToFfiOrNull(i.Target), i.ClearTarget, i.Color, i.Bold, i.Italic);

    internal static GO.OutlineItemPatch ToFfi(OutlineItemPatch p) =>
        new(p.Title, ToFfiOrNull(p.Target), p.Color, p.Bold, p.Italic, p.Open,
            [.. p.Clear.Select(key => key == OutlinePatchKey.Target ? GO.OutlinePatchKey.Target : GO.OutlinePatchKey.Color)]);

    internal static OutlineEvent ToPublic(GO.OutlineEvent e) => e switch
    {
        GO.OutlineEvent.Changed c => new OutlineEvent.Changed(c.Kind, ToPublic(c.Items), ToPublic(c.Origin)),
        GO.OutlineEvent.Resynced r => new OutlineEvent.Resynced(new OutlineSnapshot(ToPublic(r.Outline.Items), r.Outline.Incomplete)),
        _ => throw Unknown(e),
    };
}
