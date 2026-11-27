namespace EmbedPDF.Models;

/// <summary>
/// Value comparisons for facade records that hold lists. A C# record
/// compares a list member by reference, and every read returns new lists,
/// so the models compare those members item by item, as Swift's
/// <c>Equatable</c> arrays do.
/// </summary>
internal static class Structural
{
    public static bool SameList<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, Func<T, T, bool> same)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!same(a[i], b[i])) return false;
        }
        return true;
    }

    public static bool SameHit(SearchHit a, SearchHit b) =>
        a with { Segments = b.Segments } == b && a.Segments.SequenceEqual(b.Segments);

    public static bool SameHits(IReadOnlyList<SearchHit> a, IReadOnlyList<SearchHit> b) => SameList(a, b, SameHit);

    public static bool SameTool(InteractionTool a, InteractionTool b) =>
        a with { Enables = b.Enables } == b && a.Enables.SequenceEqual(b.Enables);

    public static bool SameTools(IReadOnlyList<InteractionTool> a, IReadOnlyList<InteractionTool> b) => SameList(a, b, SameTool);

    public static bool SameLabel(HistoryChangeLabel? a, HistoryChangeLabel? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.Key == b.Key && a.Values.Count == b.Values.Count
            && a.Values.All(pair => b.Values.TryGetValue(pair.Key, out var other) && Equals(pair.Value, other));
    }

    public static bool SameItem(OutlineItem a, OutlineItem b)
    {
        if (a with { Action = b.Action } != b) return false;
        // Each read lifts a new handle, so equal actions compare by their JSON.
        return (a.Action, b.Action) switch
        {
            (null, null) => true,
            ({ } x, { } y) => ReferenceEquals(x, y) || x.Json == y.Json,
            _ => false,
        };
    }

    public static bool SameRow(OutlineRow a, OutlineRow b) =>
        a.Depth == b.Depth && a.HasChildren == b.HasChildren && a.Expanded == b.Expanded && SameItem(a.Item, b.Item);
}
