namespace EmbedPDF;

/// <summary>The byte order of a <see cref="PixelBuffer"/>'s pixels, 8 bits a channel.</summary>
public enum PixelFormat
{
    /// <summary>Red, green, blue, alpha in memory order. What the engine renders.</summary>
    Rgba8,
    /// <summary>Blue, green, red, alpha in memory order, as many UI frameworks' bitmaps want.</summary>
    Bgra8,
}

/// <summary>
/// Owned pixels: 4 bytes a pixel, rows <see cref="Stride"/> bytes apart. It
/// depends on nothing native, so it outlives the document and the viewer that
/// rendered it. Copy it into your UI framework's bitmap type; the conversions
/// here (<see cref="ToFormat"/>, <see cref="ToPremultiplied"/>,
/// <see cref="ToPacked"/>) cover the usual mismatches.
/// </summary>
/// <remarks>
/// Instances are immutable once made, so sharing one across threads is safe.
/// The engine renders <see cref="PixelFormat.Rgba8"/> with straight
/// (unpremultiplied) alpha.
/// </remarks>
public sealed class PixelBuffer
{
    readonly byte[] pixels;

    /// <summary>
    /// Wraps <paramref name="pixels"/> without copying: the buffer takes
    /// ownership, so don't change the array afterwards.
    /// </summary>
    /// <param name="pixels">At least <paramref name="stride"/> times <paramref name="height"/> bytes.</param>
    /// <param name="width">Pixels a row.</param>
    /// <param name="height">Rows.</param>
    /// <param name="stride">Bytes from one row to the next; at least <paramref name="width"/> times 4.</param>
    /// <param name="format">The channel order.</param>
    /// <param name="isPremultiplied">Whether the color channels are multiplied by alpha.</param>
    /// <exception cref="ArgumentException">A size is negative, the stride is short, or the array is too small.</exception>
    public PixelBuffer(byte[] pixels, int width, int height, int stride, PixelFormat format = PixelFormat.Rgba8, bool isPremultiplied = false)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if ((long)stride < (long)width * 4) throw new ArgumentException($"stride {stride} is less than width {width} times 4", nameof(stride));
        if (pixels.LongLength < (long)stride * height) throw new ArgumentException($"{pixels.Length} bytes are fewer than stride {stride} times height {height}", nameof(pixels));
        this.pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        IsPremultiplied = isPremultiplied;
    }

    /// <summary>Pixels a row.</summary>
    public int Width { get; }

    /// <summary>Rows.</summary>
    public int Height { get; }

    /// <summary>Bytes from one row to the next. At least <see cref="Width"/> times 4; address rows by it.</summary>
    public int Stride { get; }

    /// <summary>The channel order.</summary>
    public PixelFormat Format { get; }

    /// <summary>Whether the color channels are multiplied by alpha.</summary>
    public bool IsPremultiplied { get; }

    /// <summary>Every byte, row padding included: <see cref="Stride"/> times <see cref="Height"/>.</summary>
    public ReadOnlyMemory<byte> Pixels => pixels.AsMemory(0, Stride * Height);

    /// <summary>Row <paramref name="y"/>'s <see cref="Width"/> times 4 bytes, without padding.</summary>
    public ReadOnlySpan<byte> GetRow(int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return pixels.AsSpan(y * Stride, Width * 4);
    }

    /// <summary>Copies the pixels row by row into <paramref name="destination"/>, whose rows are <paramref name="destinationStride"/> bytes apart.</summary>
    /// <exception cref="ArgumentException">The destination stride is short or the destination too small.</exception>
    public void CopyPixelsTo(Span<byte> destination, int destinationStride)
    {
        var row = Width * 4;
        if (destinationStride < row) throw new ArgumentException($"destination stride {destinationStride} is less than {row}", nameof(destinationStride));
        if (Height > 0 && destination.Length < (long)destinationStride * (Height - 1) + row) throw new ArgumentException("the destination is too small", nameof(destination));
        for (var y = 0; y < Height; y++)
        {
            GetRow(y).CopyTo(destination[(y * destinationStride)..]);
        }
    }

    /// <summary>The same pixels with rows exactly <see cref="Width"/> times 4 bytes apart; this instance when they already are.</summary>
    public PixelBuffer ToPacked()
    {
        var row = Width * 4;
        if (Stride == row) return this;
        var packed = new byte[row * Height];
        CopyPixelsTo(packed, row);
        return new PixelBuffer(packed, Width, Height, row, Format, IsPremultiplied);
    }

    /// <summary>The pixels in <paramref name="format"/>, packed; this instance when they already are in it.</summary>
    public PixelBuffer ToFormat(PixelFormat format)
    {
        if (format == Format) return this;
        var row = Width * 4;
        var copy = new byte[row * Height];
        CopyPixelsTo(copy, row);
        for (var i = 0; i < copy.Length; i += 4)
        {
            (copy[i], copy[i + 2]) = (copy[i + 2], copy[i]);
        }
        return new PixelBuffer(copy, Width, Height, row, format, IsPremultiplied);
    }

    /// <summary>The pixels with color multiplied by alpha, packed; this instance when they already are.</summary>
    public PixelBuffer ToPremultiplied()
    {
        if (IsPremultiplied) return this;
        var row = Width * 4;
        var copy = new byte[row * Height];
        CopyPixelsTo(copy, row);
        for (var i = 0; i < copy.Length; i += 4)
        {
            var alpha = copy[i + 3];
            if (alpha == 255) continue;
            copy[i] = (byte)((copy[i] * alpha + 127) / 255);
            copy[i + 1] = (byte)((copy[i + 1] * alpha + 127) / 255);
            copy[i + 2] = (byte)((copy[i + 2] * alpha + 127) / 255);
        }
        return new PixelBuffer(copy, Width, Height, row, Format, isPremultiplied: true);
    }

    /// <summary>The pixels turned clockwise by <paramref name="turn"/>, packed. Format and alpha are kept.</summary>
    internal PixelBuffer Turned(RenderRotation turn)
    {
        if (turn == RenderRotation.None) return this;
        var quarter = turn.IsQuarterTurn();
        var (width, height) = quarter ? (Height, Width) : (Width, Height);
        var turned = new byte[width * height * 4];
        for (var y = 0; y < Height; y++)
        {
            var source = GetRow(y);
            for (var x = 0; x < Width; x++)
            {
                var (dx, dy) = turn switch
                {
                    RenderRotation.Clockwise90 => (Height - 1 - y, x),
                    RenderRotation.Clockwise180 => (Width - 1 - x, Height - 1 - y),
                    _ => (y, Width - 1 - x),
                };
                source.Slice(x * 4, 4).CopyTo(turned.AsSpan((dy * width + dx) * 4, 4));
            }
        }
        return new PixelBuffer(turned, width, height, width * 4, Format, IsPremultiplied);
    }
}
