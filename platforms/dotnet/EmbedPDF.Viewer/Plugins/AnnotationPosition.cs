namespace EmbedPDF;

/// <summary>Where <c>AnnotationCapability.ReorderAsync</c> moves annotations among their page's annotations.</summary>
public abstract record AnnotationPosition
{
    private protected AnnotationPosition() { }

    /// <summary>The engine's <c>AnnotationPosition</c> JSON.</summary>
    public abstract string Json { get; }

    /// <summary>First, drawn under the rest.</summary>
    public sealed record Start : AnnotationPosition
    {
        /// <inheritdoc/>
        public override string Json => "\"start\"";
    }

    /// <summary>Last, drawn over the rest.</summary>
    public sealed record End : AnnotationPosition
    {
        /// <inheritdoc/>
        public override string Json => "\"end\"";
    }

    /// <summary>Just before a neighbour on the same page.</summary>
    /// <param name="Annotation">The neighbour.</param>
    public sealed record Before(AnnotationRef Annotation) : AnnotationPosition
    {
        /// <inheritdoc/>
        public override string Json => "{\"before\":" + RefJson(Annotation) + "}";
    }

    /// <summary>Just after a neighbour on the same page.</summary>
    /// <param name="Annotation">The neighbour.</param>
    public sealed record After(AnnotationRef Annotation) : AnnotationPosition
    {
        /// <inheritdoc/>
        public override string Json => "{\"after\":" + RefJson(Annotation) + "}";
    }

    static string PageJson(PageRef page) => "{\"kind\":\"objectNumber\",\"objectNumber\":" + WireInteger.ToUInt32(page.PageObjectNumber, "page object number") + "}";

    internal static string RefJson(AnnotationRef annotation) => annotation switch
    {
        AnnotationRef.ObjectNumber n => "{\"kind\":\"objectNumber\",\"page\":" + PageJson(n.Page) + ",\"objectNumber\":" + WireInteger.ToUInt32(n.AnnotObjectNumber, "annotation object number") + "}",
        AnnotationRef.Index i => "{\"kind\":\"baseIndex\",\"page\":" + PageJson(i.Page) + ",\"baseIndex\":" + WireInteger.ToUInt32(i.Value, "annotation index") + "}",
        null => throw new ArgumentNullException(nameof(annotation)),
        _ => throw Ffi.Unknown(annotation),
    };
}
