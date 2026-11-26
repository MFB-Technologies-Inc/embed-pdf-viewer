namespace EmbedPDF.Models;

/// <summary>
/// Maps between a visible page's overlay (a view drawn over the page, with
/// its origin at the page's top left, in viewport units) and the page's
/// unrotated page space.
/// </summary>
public static class StageVisiblePageExtensions
{
    /// <summary>
    /// The viewport point at (<paramref name="localX"/>, <paramref name="localY"/>),
    /// a point in this page's overlay. Pass it to
    /// <see cref="StageCapability.ViewportToPageAsync"/> for the page point.
    /// </summary>
    public static PdfPoint ViewportPoint(this StageVisiblePage page, double localX, double localY)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PdfPoint(page.ScreenX + localX, page.ScreenY + localY);
    }

    /// <summary>
    /// The geometry of the page as the stage shows it. Its turn comes from the
    /// transform's rotation, which includes the stage's view rotation;
    /// <see cref="PageInfo.Geometry"/> has only the page's own rotation.
    /// </summary>
    /// <param name="page">The page as the stage shows it.</param>
    /// <param name="info">This page's <see cref="PageInfo"/>, for its unrotated size.</param>
    public static PageGeometry GetGeometry(this StageVisiblePage page, PageInfo info)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(info);
        var degrees = page.Transform.Rotation;
        var rounded = double.IsFinite(degrees) ? (int)Math.Round(degrees % 360) : 0;
        return new PageGeometry(rounded, new PdfSize(info.Width, info.Height));
    }

    /// <summary>
    /// The point in unrotated page space at (<paramref name="localX"/>,
    /// <paramref name="localY"/>), a point in this page's overlay. Both axes
    /// scale by the page's width as shown over the transform's view width.
    /// When the view width is not greater than zero, every point maps to the
    /// page point at the top left of the page as shown, so the result is
    /// finite for a page the stage has not sized.
    /// </summary>
    public static PdfPoint PagePoint(this StageVisiblePage page, double localX, double localY, PageInfo info)
    {
        var geometry = page.GetGeometry(info);
        var scale = Ratio(geometry.DisplaySize.Width, page.Transform.ViewWidth);
        return geometry.ToPage(new PdfPoint(localX * scale, localY * scale));
    }

    /// <summary>
    /// Where <paramref name="pageRect"/>, a rect in unrotated page space, sits
    /// in this page's overlay, with the origin at the page's top left. The
    /// inverse of <see cref="PagePoint"/>. When the page's width as shown or
    /// the transform's view width is not greater than zero, the result is the
    /// empty rect at the origin.
    /// </summary>
    public static PdfRect LocalRect(this StageVisiblePage page, PdfRect pageRect, PageInfo info)
    {
        var geometry = page.GetGeometry(info);
        var scale = Ratio(page.Transform.ViewWidth, geometry.DisplaySize.Width);
        var shown = geometry.ToDisplay(pageRect);
        return new PdfRect(shown.X * scale, shown.Y * scale, shown.Width * scale, shown.Height * scale);
    }

    /// <summary><paramref name="length"/> over <paramref name="divisor"/>, or 0 when the divisor is not greater than zero.</summary>
    static double Ratio(double length, double divisor) => divisor > 0 ? length / divisor : 0;
}
