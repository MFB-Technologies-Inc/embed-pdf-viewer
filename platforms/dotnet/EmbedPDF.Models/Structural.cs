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
}
