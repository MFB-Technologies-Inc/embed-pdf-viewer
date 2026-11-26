package com.embedpdf.models

import android.graphics.Bitmap
import com.embedpdf.PageImage
import com.embedpdf.PdfException

/**
 * Bitmaps kept for reuse, at most [maxBytes] of them, so a page's tiles,
 * which come and go as the user scrolls and zooms, are drawn into bitmaps
 * already allocated instead of new ones. Thread-safe.
 *
 * [acquire] hands out a pooled `ARGB_8888` bitmap of the size asked for, or
 * a new one. [release] takes one back when it fits in the budget, evicting
 * the bitmaps released longest ago to make room; one that is larger than the
 * whole budget, immutable or recycled is dropped for the garbage collector.
 * The pool never recycles a bitmap itself.
 *
 * Release a bitmap only once nothing draws it any more: the pool hands it to
 * the next [acquire], which overwrites its pixels.
 */
public class BitmapPool(public val maxBytes: Long) {
    init {
        require(maxBytes >= 0) { "maxBytes $maxBytes is below zero" }
    }

    private val lock = Any()
    private val free = ArrayDeque<Bitmap>()
    private var bytes = 0L

    /** The bytes of the bitmaps the pool holds now. */
    public val pooledBytes: Long get() = synchronized(lock) { bytes }

    /** A mutable `ARGB_8888` bitmap of [width] by [height] pixels, from the pool when it holds one. */
    public fun acquire(width: Int, height: Int): Bitmap {
        synchronized(lock) {
            val index = free.indexOfFirst { it.width == width && it.height == height && !it.isRecycled }
            if (index >= 0) {
                val bitmap = free.removeAt(index)
                bytes -= bitmap.allocationByteCount
                return bitmap
            }
        }
        return Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
    }

    /** Takes [bitmap] back for a later [acquire] when it fits the budget. */
    public fun release(bitmap: Bitmap) {
        if (!bitmap.isMutable || bitmap.isRecycled || bitmap.config != Bitmap.Config.ARGB_8888) return
        val size = bitmap.allocationByteCount.toLong()
        if (size > maxBytes) return
        synchronized(lock) {
            if (free.any { it === bitmap }) return
            while (bytes + size > maxBytes) {
                val oldest = free.removeFirstOrNull() ?: break
                bytes -= oldest.allocationByteCount
            }
            free.addLast(bitmap)
            bytes += size
        }
    }

    /** Drops every pooled bitmap. */
    public fun clear() {
        synchronized(lock) {
            free.clear()
            bytes = 0
        }
    }
}

/**
 * These pixels in a bitmap from [pool], premultiplied as `Bitmap.setPixels`
 * premultiplies them, so a translucent pixel keeps its color as
 * [PageImage.toBitmap] keeps it. An empty raster throws `invalid-input`.
 */
public fun PageImage.toBitmap(pool: BitmapPool): Bitmap {
    if (width == 0 || height == 0) {
        throw PdfException(PdfException.Code.INVALID_INPUT, "a ${width}x$height raster has no bitmap", name = "InvalidInput")
    }
    val pixels = copyPixels()
    val colors = IntArray(width * height)
    var index = 0
    for (y in 0 until height) {
        var offset = y * stride
        repeat(width) {
            val r = pixels[offset].toInt() and 0xff
            val g = pixels[offset + 1].toInt() and 0xff
            val b = pixels[offset + 2].toInt() and 0xff
            val a = pixels[offset + 3].toInt() and 0xff
            colors[index++] = (a shl 24) or (r shl 16) or (g shl 8) or b
            offset += 4
        }
    }
    return pool.acquire(width, height).apply { setPixels(colors, 0, width, 0, 0, width, height) }
}
