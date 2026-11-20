package com.embedpdf

import android.graphics.Bitmap
import uniffi.epdf_plugin_render.RenderedImage

/**
 * A raster's owned pixels: four bytes a pixel in memory order red, green,
 * blue, alpha, with straight (not premultiplied) alpha, rows [stride] bytes
 * apart. It depends on nothing in the engine, so it outlives the document and
 * the viewer. Immutable: nothing changes the bytes after it is made.
 *
 * [toBitmap] turns it into an Android [Bitmap]; the Compose module adds the
 * `ImageBitmap` conversion.
 */
public class PageImage internal constructor(
    public val width: Int,
    public val height: Int,
    /** Bytes per row, at least `width * 4`. */
    public val stride: Int,
    private val pixels: ByteArray,
) {
    /** A copy of the pixel bytes, `stride * height` of them. */
    public fun copyPixels(): ByteArray = pixels.copyOf()

    /**
     * The pixel at ([x], [y]), counted from the top-left corner, as a straight
     * (unpremultiplied) ARGB color int, the form `android.graphics.Color` uses.
     */
    public fun pixel(x: Int, y: Int): Int {
        require(x in 0 until width && y in 0 until height) { "($x, $y) is outside $width by $height" }
        val offset = y * stride + x * 4
        val r = pixels[offset].toInt() and 0xff
        val g = pixels[offset + 1].toInt() and 0xff
        val b = pixels[offset + 2].toInt() and 0xff
        val a = pixels[offset + 3].toInt() and 0xff
        return (a shl 24) or (r shl 16) or (g shl 8) or b
    }

    /**
     * An `ARGB_8888` bitmap of these pixels. Android keeps such a bitmap
     * premultiplied; the straight colors go in through
     * `Bitmap.createBitmap(IntArray, ...)`, which premultiplies them, so a
     * translucent pixel keeps its color. Each call allocates a new bitmap of
     * `width * height * 4` bytes; a caller that draws the same render on
     * every frame keeps the bitmap.
     */
    public fun toBitmap(): Bitmap {
        val colors = IntArray(width * height)
        for (y in 0 until height) {
            for (x in 0 until width) colors[y * width + x] = pixel(x, y)
        }
        return Bitmap.createBitmap(colors, width, height, Bitmap.Config.ARGB_8888)
    }

    internal companion object {
        /**
         * Takes [rendered]'s bytes without copying: the engine hands each
         * image over once. A buffer shorter than its dimensions say is a value
         * that didn't survive the boundary, so it throws `invalid-state`.
         */
        fun from(rendered: RenderedImage): PageImage {
            rendered.format.checkRgba8()
            val width = rendered.width.toIntResult("raster width")
            val height = rendered.height.toIntResult("raster height")
            val stride = rendered.stride.toIntResult("raster stride")
            if (stride < width.toLong() * 4 || rendered.pixels.size.toLong() < stride.toLong() * height) {
                throw PdfException(
                    PdfException.Code.INVALID_STATE,
                    "a ${width}x$height raster with stride $stride came with ${rendered.pixels.size} bytes",
                    name = "InvalidState",
                )
            }
            return PageImage(width, height, stride, rendered.pixels)
        }
    }
}
