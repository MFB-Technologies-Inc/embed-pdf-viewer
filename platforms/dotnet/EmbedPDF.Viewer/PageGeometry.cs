namespace EmbedPDF;

/// <summary>
/// Converts between a page's unrotated space and the page as it is shown,
/// turned clockwise by its rotation.
/// </summary>
/// <remarks>
/// Engine geometry (annotation rects, text boxes, search hits) is in
/// unrotated page space. Both spaces have a top-left origin with y pointing
/// down, in PDF points. For a page of unrotated width <c>w</c> and height
/// <c>h</c>, <see cref="ToDisplay(PdfPoint)"/> maps <c>(x, y)</c> to
/// <c>(h - y, x)</c> at 90 degrees, <c>(w - x, h - y)</c> at 180 and
/// <c>(y, w - x)</c> at 270, matching the engine's render matrix.
/// <see cref="ToPage(PdfPoint)"/> is its inverse.
/// </remarks>
/// <param name="Turn">The clockwise turn of the page as shown.</param>
/// <param name="PageSize">The unrotated page size, as <see cref="PageInfo"/> reports it.</param>
public readonly record struct PageGeometry(RenderRotation Turn, PdfSize PageSize)
{
    /// <summary>
    /// The geometry for a page turned <paramref name="rotation"/> degrees
    /// clockwise. Any value is accepted; it reduces to 0 to 359 and rounds to
    /// the nearest quarter turn.
    /// </summary>
    public PageGeometry(int rotation, PdfSize pageSize) : this(RenderRotationExtensions.FromDegrees(rotation), pageSize) { }

    /// <summary>Degrees clockwise: 0, 90, 180 or 270.</summary>
    public int Rotation => Turn.Degrees();

    /// <summary>The page size as shown: width and height swap at 90 and 270 degrees.</summary>
    public PdfSize DisplaySize => Turn.IsQuarterTurn() ? new PdfSize(PageSize.Height, PageSize.Width) : PageSize;

    /// <summary>Converts a point in unrotated page space to the page as shown.</summary>
    public PdfPoint ToDisplay(PdfPoint point)
    {
        var (w, h) = (PageSize.Width, PageSize.Height);
        return Turn switch
        {
            RenderRotation.Clockwise90 => new PdfPoint(h - point.Y, point.X),
            RenderRotation.Clockwise180 => new PdfPoint(w - point.X, h - point.Y),
            RenderRotation.Clockwise270 => new PdfPoint(point.Y, w - point.X),
            _ => point,
        };
    }

    /// <summary>Converts a point on the page as shown to unrotated page space.</summary>
    public PdfPoint ToPage(PdfPoint point)
    {
        var (w, h) = (PageSize.Width, PageSize.Height);
        return Turn switch
        {
            RenderRotation.Clockwise90 => new PdfPoint(point.Y, h - point.X),
            RenderRotation.Clockwise180 => new PdfPoint(w - point.X, h - point.Y),
            RenderRotation.Clockwise270 => new PdfPoint(w - point.Y, point.X),
            _ => point,
        };
    }

    /// <summary>Converts a rect in unrotated page space to the page as shown: the bounding box of its converted corners.</summary>
    public PdfRect ToDisplay(PdfRect rect) => Bounds(ToDisplay(new PdfPoint(rect.X, rect.Y)), ToDisplay(new PdfPoint(rect.X + rect.Width, rect.Y + rect.Height)));

    /// <summary>Converts a rect on the page as shown to unrotated page space: the bounding box of its converted corners.</summary>
    public PdfRect ToPage(PdfRect rect) => Bounds(ToPage(new PdfPoint(rect.X, rect.Y)), ToPage(new PdfPoint(rect.X + rect.Width, rect.Y + rect.Height)));

    // A quarter turn maps opposite corners to opposite corners, so two of
    // them bound the rect.
    static PdfRect Bounds(PdfPoint a, PdfPoint b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new PdfRect(x, y, Math.Max(a.X, b.X) - x, Math.Max(a.Y, b.Y) - y);
    }
}
