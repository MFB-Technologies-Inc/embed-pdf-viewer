using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// <see cref="PixelBuffer"/>, and the image cases of <c>PageGeometryTests.swift</c>
/// over it in place of <c>CGImage</c>.
/// </summary>
public sealed class PixelBufferTests
{
    static readonly byte[] Red = [255, 0, 0, 255];
    static readonly byte[] Blue = [0, 0, 255, 255];
    static readonly byte[] Green = [0, 255, 0, 255];
    static readonly PdfSize Size = new(200, 100);

    /// <summary>A buffer from rows of pixels, each row padded by <paramref name="padding"/> bytes of 0xEE.</summary>
    static PixelBuffer Bitmap(byte[][][] rows, int padding = 0)
    {
        var width = rows[0].Length;
        var stride = width * 4 + padding;
        var bytes = Enumerable.Repeat((byte)0xEE, stride * rows.Length).ToArray();
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < width; x++) rows[y][x].CopyTo(bytes, y * stride + x * 4);
        }
        return new PixelBuffer(bytes, width, rows.Length, stride);
    }

    static byte[] Pixel(PixelBuffer image, int x, int y) => image.GetRow(y).Slice(x * 4, 4).ToArray();

    [Fact]
    public void ImageSwapsItsSidesOnAQuarterTurn()
    {
        var image = new PixelBuffer(new byte[4 * 2 * 4], 4, 2, 16);
        var turned = new PageGeometry(RenderRotation.Clockwise90, Size).ToDisplay(image);
        Assert.Equal((2, 4), (turned.Width, turned.Height));
    }

    [Fact]
    public void ImageTurnsClockwiseWithThePage()
    {
        // 2x1 bitmap: left pixel red, right pixel blue. A clockwise quarter
        // turn makes it 1x2 with red on top.
        var image = Bitmap([[Red, Blue]]);
        Assert.Same(image, new PageGeometry(0, Size).ToDisplay(image));
        var turned = new PageGeometry(90, Size).ToDisplay(image);
        Assert.Equal((1, 2), (turned.Width, turned.Height));
        Assert.Equal(Red, Pixel(turned, 0, 0));
        Assert.Equal(Blue, Pixel(turned, 0, 1));
    }

    [Fact]
    public void HalfAndThreeQuarterTurnsMoveEveryPixel()
    {
        // 2x2 with padded rows: red green / blue (0xEE padding must not leak).
        var image = Bitmap([[Red, Green], [Blue, Red]], padding: 8);
        var half = new PageGeometry(180, Size).ToDisplay(image);
        Assert.Equal(Red, Pixel(half, 0, 0));
        Assert.Equal(Blue, Pixel(half, 1, 0));
        Assert.Equal(Green, Pixel(half, 0, 1));
        var threeQuarters = new PageGeometry(270, Size).ToDisplay(image);
        Assert.Equal(Green, Pixel(threeQuarters, 0, 0));
        Assert.Equal(Red, Pixel(threeQuarters, 1, 0));
        Assert.Equal(Red, Pixel(threeQuarters, 0, 1));
        Assert.Equal(Blue, Pixel(threeQuarters, 1, 1));
        var quarter = new PageGeometry(90, Size).ToDisplay(image);
        Assert.Equal(Blue, Pixel(quarter, 0, 0));
        Assert.Equal(Red, Pixel(quarter, 1, 0));
        Assert.Equal(8, quarter.Stride);
        Assert.DoesNotContain((byte)0xEE, quarter.Pixels.ToArray());
    }

    [Fact]
    public void APaddedBufferKeepsItsStrideAndPacksOnRequest()
    {
        var image = Bitmap([[Red], [Blue]], padding: 4);
        Assert.Equal(8, image.Stride);
        Assert.Equal(16, image.Pixels.Length);
        Assert.Equal(Blue, image.GetRow(1).ToArray());
        var packed = image.ToPacked();
        Assert.Equal(4, packed.Stride);
        Assert.Equal([.. Red, .. Blue], packed.Pixels.ToArray());
        Assert.Same(packed, packed.ToPacked());
    }

    [Fact]
    public void PremultiplyingHalvesColorAtHalfAlpha()
    {
        var image = Bitmap([[[255, 128, 0, 128], Red]]);
        var premultiplied = image.ToPremultiplied();
        Assert.True(premultiplied.IsPremultiplied);
        Assert.Equal([128, 64, 0, 128], Pixel(premultiplied, 0, 0));
        Assert.Equal(Red, Pixel(premultiplied, 1, 0));
        Assert.Same(premultiplied, premultiplied.ToPremultiplied());
    }

    [Fact]
    public void BgraSwapsRedAndBlue()
    {
        var image = Bitmap([[Red, Green]], padding: 4);
        var bgra = image.ToFormat(PixelFormat.Bgra8);
        Assert.Equal(PixelFormat.Bgra8, bgra.Format);
        Assert.Equal(Blue, Pixel(bgra, 0, 0));
        Assert.Equal(Green, Pixel(bgra, 1, 0));
        Assert.Same(image, image.ToFormat(PixelFormat.Rgba8));
        Assert.Equal(Red, Pixel(image, 0, 0));
    }

    [Fact]
    public void ShortStridesAndArraysAreRefused()
    {
        Assert.Throws<ArgumentException>(() => new PixelBuffer(new byte[16], 2, 2, 7));
        Assert.Throws<ArgumentException>(() => new PixelBuffer(new byte[15], 2, 2, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(new byte[0], -1, 0, 0));
        var image = new PixelBuffer(new byte[16], 2, 2, 8);
        Assert.Throws<ArgumentException>(() => image.CopyPixelsTo(new byte[15], 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetRow(2));
    }

    [Fact]
    public void AnEngineRasterMapsItsFormatAndRefusesAnUnknownOne()
    {
        var rgba = Ffi.ToPublic(new uniffi.epdf_plugin_render.RenderedImage(1, 1, 4, 2, uniffi.epdf_plugin_render.RenderPixelFormat.Rgba8, [1, 2, 3, 4]));
        Assert.Equal((PixelFormat.Rgba8, false, 2.0), (rgba.Image.Format, rgba.Image.IsPremultiplied, rgba.Scale));
        var unknown = new uniffi.epdf_plugin_render.RenderedImage(1, 1, 4, 1, (uniffi.epdf_plugin_render.RenderPixelFormat)7, [1, 2, 3, 4]);
        Assert.Equal(PdfErrorCode.InvalidState, Assert.Throws<PdfException>(() => Ffi.ToPublic(unknown)).Code);
        var short_ = new uniffi.epdf_plugin_render.RenderedImage(2, 1, 4, 1, uniffi.epdf_plugin_render.RenderPixelFormat.Rgba8, [1, 2, 3, 4]);
        Assert.Equal(PdfErrorCode.InvalidState, Assert.Throws<PdfException>(() => Ffi.ToPublic(short_)).Code);
    }

    [Fact]
    public void AZeroSizedBufferConvertsAndTurns()
    {
        var empty = new PixelBuffer([], 0, 0, 0);
        Assert.Equal(0, empty.Pixels.Length);
        Assert.Same(empty, empty.ToPacked());
        Assert.Equal((0, 0), (empty.ToFormat(PixelFormat.Bgra8).Width, empty.ToPremultiplied().Height));
        Assert.Equal((0, 0), (new PageGeometry(90, Size).ToDisplay(empty).Width, new PageGeometry(90, Size).ToDisplay(empty).Height));
        empty.CopyPixelsTo([], 0);
        var noRows = new PixelBuffer(new byte[8], 2, 0, 8);
        Assert.Equal(0, noRows.ToPacked().Pixels.Length);
        var turned = new PageGeometry(90, Size).ToDisplay(noRows);
        Assert.Equal((0, 2), (turned.Width, turned.Height));
    }

    [Fact]
    public void PremultiplyingAtAlphaZeroClearsColor()
    {
        var image = Bitmap([[[255, 200, 100, 0]]]);
        Assert.Equal([0, 0, 0, 0], Pixel(image.ToPremultiplied(), 0, 0));
    }

    [Fact]
    public void AnAlreadyConvertedBufferComesBackPaddedAsItIs()
    {
        var padded = Bitmap([[Red], [Blue]], padding: 4);
        Assert.Same(padded, padded.ToFormat(PixelFormat.Rgba8));
        Assert.Equal(8, padded.ToFormat(PixelFormat.Rgba8).Stride);
        var premultiplied = new PixelBuffer(padded.Pixels.ToArray(), 1, 2, 8, PixelFormat.Rgba8, isPremultiplied: true);
        Assert.Same(premultiplied, premultiplied.ToPremultiplied());
        Assert.Equal(8, premultiplied.ToPremultiplied().Stride);
    }
}
