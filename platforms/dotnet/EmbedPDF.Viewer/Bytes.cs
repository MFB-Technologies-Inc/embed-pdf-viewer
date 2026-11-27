using System.Runtime.InteropServices;

namespace EmbedPDF;

/// <summary>
/// The generated bindings take a document as a <see cref="byte"/> array.
/// These helpers reach one with as few copies as the source allows.
/// </summary>
internal static class Bytes
{
    /// <summary>The backing array itself when <paramref name="memory"/> covers all of one; a copy otherwise.</summary>
    internal static byte[] Array(ReadOnlyMemory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : memory.ToArray();

    /// <summary>
    /// The rest of <paramref name="stream"/>. A seekable stream is read into
    /// one buffer sized from its length; another one is buffered, then copied
    /// once into an exact array.
    /// </summary>
    internal static async Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining >= 0 && remaining <= System.Array.MaxLength)
            {
                var buffer = new byte[remaining];
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
                    if (n == 0) return buffer.AsSpan(0, read).ToArray();
                    read += n;
                }
                // A stream that grew while it was read still gives all of itself.
                var probe = new byte[1];
                if (await stream.ReadAsync(probe, cancellationToken).ConfigureAwait(false) == 0) return buffer;
                using var rest = new MemoryStream();
                rest.Write(buffer);
                rest.Write(probe);
                await stream.CopyToAsync(rest, cancellationToken).ConfigureAwait(false);
                return rest.ToArray();
            }
        }
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return copy.ToArray();
    }
}
