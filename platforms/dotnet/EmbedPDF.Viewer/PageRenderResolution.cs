namespace EmbedPDF;

/// <summary>
/// How sharp a view should render a page: full resolution while any part of
/// it is on screen, a small placeholder otherwise.
/// </summary>
/// <remarks>
/// Virtualizing lists keep neighbor pages built, and accessibility clients
/// build more of them. A page that renders at full resolution whenever its
/// view exists holds megabytes for pages nobody can see.
/// </remarks>
/// <param name="DeviceWidth">Device pixels across the page's displayed width.</param>
/// <param name="IsFull">Whether this is the on-screen render rather than the placeholder.</param>
public readonly record struct PageRenderResolution(double DeviceWidth, bool IsFull)
{
    /// <summary>Device pixels for a page that is built but not on screen.</summary>
    public const double OffScreenDeviceWidth = 256;

    /// <summary>Full-resolution widths round up to this many pixels, so small layout changes don't each start a new render.</summary>
    public const double DeviceWidthStep = 128;

    /// <summary>
    /// The resolution for a page laid out <paramref name="viewWidth"/> units
    /// wide. On-screen widths round up to multiples of <see cref="DeviceWidthStep"/>,
    /// at least 64 pixels. Off-screen pages always use <see cref="OffScreenDeviceWidth"/>.
    /// </summary>
    /// <param name="viewWidth">The page's displayed width in layout units.</param>
    /// <param name="displayScale">Device pixels per layout unit.</param>
    /// <param name="zoomScale">Additional magnification; values below one count as one.</param>
    /// <param name="isOnScreen">Whether any part of the page is visible.</param>
    public static PageRenderResolution ForPage(double viewWidth, double displayScale, double zoomScale = 1, bool isOnScreen = true)
    {
        if (!isOnScreen) return new PageRenderResolution(OffScreenDeviceWidth, false);
        var pixels = viewWidth * displayScale * Math.Max(zoomScale, 1);
        return new PageRenderResolution(Math.Max(DeviceWidthStep / 2, Math.Ceiling(pixels / DeviceWidthStep) * DeviceWidthStep), true);
    }

    /// <summary>
    /// <paramref name="scale"/> raised so the page's width in device pixels
    /// rounds up to a multiple of <see cref="DeviceWidthStep"/>. Scales whose
    /// widths fall in one step share a value; the result is never below
    /// <paramref name="scale"/>, and is <paramref name="scale"/> itself when
    /// either value is not positive.
    /// </summary>
    public static double BandedScale(double scale, double pageWidth)
    {
        if (!(scale > 0) || !(pageWidth > 0)) return scale;
        var pixels = Math.Ceiling(scale * pageWidth / DeviceWidthStep) * DeviceWidthStep;
        return Math.Max(scale, pixels / pageWidth);
    }

    /// <summary>
    /// <see cref="DeviceWidth"/> measured across a page whose displayed width
    /// differs from its unrotated width, for a render of the unrotated page.
    /// The denominator is clamped to at least one; halves round away from zero.
    /// </summary>
    public double UnrotatedDeviceWidth(double unrotatedWidth, double displayedWidth) =>
        Math.Round(DeviceWidth * unrotatedWidth / Math.Max(displayedWidth, 1), MidpointRounding.AwayFromZero);
}
