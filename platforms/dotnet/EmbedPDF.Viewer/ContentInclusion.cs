namespace EmbedPDF;

/// <summary>
/// Whether a call includes a part the session may not be allowed to read: a
/// search hit's snippet, or a page picture's annotations or form fields.
/// </summary>
/// <remarks>
/// The engine's default is <see cref="WhenPermitted"/>, which no
/// <see cref="bool"/> can say: what it includes follows the session's scope,
/// so the same call draws annotations in one viewer and leaves them out in another.
/// </remarks>
public enum ContentInclusion
{
    /// <summary>Include the part when the session may read it, and leave it out otherwise. The engine's default.</summary>
    WhenPermitted,
    /// <summary>Include the part. A call that produces it throws <c>permission-denied</c> when the session may not read it.</summary>
    Always,
    /// <summary>Leave the part out.</summary>
    Never,
}

internal static class ContentInclusionExtensions
{
    /// <summary>The engine's spelling: <see langword="null"/> is <see cref="ContentInclusion.WhenPermitted"/>.</summary>
    internal static bool? ToEngine(this ContentInclusion inclusion) => inclusion switch
    {
        ContentInclusion.Always => true,
        ContentInclusion.Never => false,
        ContentInclusion.WhenPermitted => null,
        _ => throw Ffi.Undefined(inclusion, "content inclusion"),
    };
}
