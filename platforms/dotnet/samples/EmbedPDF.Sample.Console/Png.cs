using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace EmbedPDF.Sample;

/// <summary>
/// Writes a <see cref="PixelBuffer"/> as an 8-bit RGBA PNG: the signature,
/// IHDR, one zlib IDAT with filter type 0 on every row, and IEND, each chunk
/// with its CRC-32. No image library, so the sample's only dependencies are
/// the EmbedPDF packages.
/// </summary>
static class Png
{
    static readonly uint[] CrcTable = BuildCrcTable();

    public static void Write(Stream output, PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(image);
        // PNG stores straight alpha in RGBA order.
        var rgba = image.ToFormat(PixelFormat.Rgba8);
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, rgba.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), rgba.Height);
        header[8] = 8; // bits per channel
        header[9] = 6; // RGBA
        WriteChunk(output, "IHDR", header);

        using var data = new MemoryStream();
        using (var zlib = new ZLibStream(data, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[1 + rgba.Width * 4];
            for (var y = 0; y < rgba.Height; y++)
            {
                var source = rgba.GetRow(y)[..(rgba.Width * 4)];
                source.CopyTo(row.AsSpan(1));
                if (rgba.IsPremultiplied) Unpremultiply(row.AsSpan(1));
                zlib.Write(row);
            }
        }
        WriteChunk(output, "IDAT", data.GetBuffer().AsSpan(0, (int)data.Length));
        WriteChunk(output, "IEND", []);
    }

    static void Unpremultiply(Span<byte> pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha is 0 or 255) continue;
            for (var c = 0; c < 3; c++) pixels[i + c] = (byte)Math.Min(255, (pixels[i + c] * 255 + alpha / 2) / alpha);
        }
    }

    static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> body)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, body.Length);
        output.Write(word);
        Span<byte> kind = stackalloc byte[4];
        Encoding.ASCII.GetBytes(type, kind);
        output.Write(kind);
        output.Write(body);
        var crc = Crc(Crc(0xFFFFFFFFu, kind), body) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }

    static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
