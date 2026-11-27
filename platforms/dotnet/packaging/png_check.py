"""Checks the structure of an 8-bit RGBA PNG without an image library.

The console sample writes its own PNG encoder, so dotnet-consumer.sh checks
what it wrote: the signature, every chunk's CRC-32, the IHDR size and format,
and that the image data inflates to one filter byte plus a row of pixels per
line.
"""
import argparse
import struct
import sys
import zlib

SIGNATURE = b"\x89PNG\r\n\x1a\n"

def check_png(data, width, height):
    if not data.startswith(SIGNATURE):
        raise ValueError("not a PNG: bad signature")
    offset = len(SIGNATURE)
    chunks = []
    while offset < len(data):
        if offset + 8 > len(data):
            raise ValueError("truncated chunk header")
        length, kind = struct.unpack(">I4s", data[offset:offset + 8])
        end = offset + 8 + length + 4
        if end > len(data):
            raise ValueError(f"truncated {kind!r} chunk")
        body = data[offset + 8:offset + 8 + length]
        (crc,) = struct.unpack(">I", data[end - 4:end])
        if zlib.crc32(kind + body) & 0xffffffff != crc:
            raise ValueError(f"CRC mismatch in {kind.decode('latin-1')} chunk")
        chunks.append((kind, body))
        offset = end
    if not chunks or chunks[0][0] != b"IHDR" or chunks[-1][0] != b"IEND":
        raise ValueError("chunks must start with IHDR and end with IEND")
    w, h, depth, colour, compression, filtering, interlace = struct.unpack(">IIBBBBB", chunks[0][1])
    if (w, h) != (width, height):
        raise ValueError(f"IHDR says {w}x{h}, expected {width}x{height}")
    if (depth, colour, compression, filtering, interlace) != (8, 6, 0, 0, 0):
        raise ValueError("IHDR is not 8-bit non-interlaced RGBA")
    raw = zlib.decompress(b"".join(body for kind, body in chunks if kind == b"IDAT"))
    stride = 1 + 4 * width
    if len(raw) != height * stride:
        raise ValueError(f"image data size {len(raw)}, expected {height * stride}")
    for row in range(height):
        if raw[row * stride] > 4:
            raise ValueError(f"row {row} has filter type {raw[row * stride]}")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("file")
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    args = parser.parse_args()
    try:
        with open(args.file, "rb") as source:
            check_png(source.read(), args.width, args.height)
    except (OSError, ValueError, zlib.error) as error:
        print(f"ERROR: {args.file}: {error}", file=sys.stderr)
        return 1
    print(f"PNG ok: {args.width}x{args.height}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
