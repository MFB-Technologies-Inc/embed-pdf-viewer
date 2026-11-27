namespace EmbedPDF.Models;

/// <summary>
/// One pointer event as a view reports it to <see cref="StageReaderModel"/>:
/// a point in the reader's viewport, in the same units as the viewport size
/// the view passes to <see cref="StageReaderModel.SizeChangedAsync"/>, with
/// the origin at the viewport's top left and y growing downward.
/// </summary>
/// <remarks>
/// The model looks up the page under the point and builds the
/// <see cref="PointerSample"/> the interaction hub takes; the phase comes from
/// the method the view calls. <see cref="Kind"/> is
/// <see cref="PointerKind.Touch"/> unless the view says otherwise, as for a
/// mouse or a pen.
/// </remarks>
/// <param name="X">The viewport x.</param>
/// <param name="Y">The viewport y.</param>
public readonly record struct PointerInput(double X, double Y)
{
    /// <summary>What made the event.</summary>
    public PointerKind Kind { get; init; } = PointerKind.Touch;

    /// <summary>The keys held during the event.</summary>
    public PointerModifiers Modifiers { get; init; }

    internal PdfPoint Point => new(X, Y);
}
