namespace EmbedPDF;

/// <summary>Up-front argument checks that <see cref="ArgumentNullException.ThrowIfNull(object?, string?)"/> doesn't cover.</summary>
internal static class Arguments
{
    /// <summary>
    /// <paramref name="items"/> as an array, enumerated once. A null element
    /// throws <see cref="ArgumentException"/> naming <paramref name="parameter"/>
    /// and the element's position, before anything reaches the engine.
    /// </summary>
    internal static T[] NoNullElements<T>(IEnumerable<T?> items, string parameter) where T : class
    {
        var array = items.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] is null) throw new ArgumentException($"{parameter} has a null element at {i}.", parameter);
        }
        return array!;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> naming <paramref name="parameter"/>
    /// and the key when a value of <paramref name="items"/> is null, before
    /// anything reaches the engine.
    /// </summary>
    internal static void NoNullValues<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> items, string parameter)
    {
        foreach (var (key, value) in items)
        {
            if (value is null) throw new ArgumentException($"{parameter} has a null value for {key}.", parameter);
        }
    }
}
