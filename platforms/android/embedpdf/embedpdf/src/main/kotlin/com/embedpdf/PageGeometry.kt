package com.embedpdf

import android.graphics.Bitmap
import android.graphics.Matrix
import android.graphics.PointF
import android.graphics.RectF
import android.util.SizeF
import kotlin.math.floor

/**
 * Converts between a page's unrotated space and the page as it is shown,
 * turned clockwise by its rotation.
 *
 * Engine geometry (annotation rects, text boxes, search hits) is in unrotated
 * page space. Both spaces have a top-left origin with y pointing down, in PDF
 * points. For a page of unrotated width `w` and height `h`, [toDisplay] maps
 * `(x, y)` to `(h - y, x)` at 90 degrees, `(w - x, h - y)` at 180 and
 * `(y, w - x)` at 270, matching the engine's render matrix. [toPage] is its
 * inverse.
 *
 * @property turn the clockwise turn of the page as shown. Pass it to
 *   [RenderPageOptions] so a render matches the page as shown.
 * @property pageSize the unrotated page size, as [PageInfo] reports it.
 */
public data class PageGeometry(public val turn: RenderRotation, public val pageSize: Size) {
    /**
     * The geometry for a page turned [rotation] degrees clockwise. Any value
     * is accepted; it reduces to `0 until 360` and rounds to the nearest
     * quarter turn.
     */
    public constructor(rotation: Int, pageSize: Size) : this(RenderRotation.fromDegrees(rotation), pageSize)

    /** Degrees clockwise: 0, 90, 180 or 270. */
    public val rotation: Int get() = turn.degrees

    /** The page size as shown: width and height swap at 90 and 270 degrees. */
    public val displaySize: Size
        get() = if (turn.isQuarterTurn) Size(pageSize.height, pageSize.width) else pageSize

    /** [displaySize] as an Android value. */
    public val displaySizeF: SizeF get() = displaySize.toSizeF()

    /** Converts a point in unrotated page space to the page as shown. */
    public fun toDisplay(point: Point): Point {
        val w = pageSize.width
        val h = pageSize.height
        return when (turn) {
            RenderRotation.NONE -> point
            RenderRotation.CLOCKWISE90 -> Point(h - point.y, point.x)
            RenderRotation.CLOCKWISE180 -> Point(w - point.x, h - point.y)
            RenderRotation.CLOCKWISE270 -> Point(point.y, w - point.x)
        }
    }

    /** Converts a point on the page as shown to unrotated page space. */
    public fun toPage(point: Point): Point {
        val w = pageSize.width
        val h = pageSize.height
        return when (turn) {
            RenderRotation.NONE -> point
            RenderRotation.CLOCKWISE90 -> Point(point.y, h - point.x)
            RenderRotation.CLOCKWISE180 -> Point(w - point.x, h - point.y)
            RenderRotation.CLOCKWISE270 -> Point(w - point.y, point.x)
        }
    }

    /** Converts a rect in unrotated page space to the page as shown: the bounding box of its converted corners. */
    public fun toDisplay(rect: Rect): Rect = bounds(rect, ::toDisplay)

    /** Converts a rect on the page as shown to unrotated page space: the bounding box of its converted corners. */
    public fun toPage(rect: Rect): Rect = bounds(rect, ::toPage)

    /** [toDisplay] for an Android point. */
    public fun toDisplay(point: PointF): PointF = toDisplay(point.toPoint()).toPointF()

    /** [toPage] for an Android point. */
    public fun toPage(point: PointF): PointF = toPage(point.toPoint()).toPointF()

    /** [toDisplay] for an Android rect. */
    public fun toDisplay(rect: RectF): RectF = toDisplay(rect.toRect()).toRectF()

    /** [toPage] for an Android rect. */
    public fun toPage(rect: RectF): RectF = toPage(rect.toRect()).toRectF()

    /**
     * Turns an unrotated page render clockwise by [turn] so it matches the
     * page as shown. Returns the same instance when [turn] is `NONE`.
     *
     * Performance: every call for a turned page allocates a copy of the whole
     * image, 4 bytes a pixel. A caller that turns the same render again on
     * every frame keeps the turned image, keyed by the render and the turn.
     */
    public fun toDisplay(image: PageImage): PageImage {
        if (turn == RenderRotation.NONE) return image
        val source = image.copyPixels()
        val width = if (turn.isQuarterTurn) image.height else image.width
        val height = if (turn.isQuarterTurn) image.width else image.height
        val turned = ByteArray(width * height * 4)
        for (y in 0 until image.height) {
            for (x in 0 until image.width) {
                val (tx, ty) = when (turn) {
                    RenderRotation.CLOCKWISE90 -> (image.height - 1 - y) to x
                    RenderRotation.CLOCKWISE180 -> (image.width - 1 - x) to (image.height - 1 - y)
                    else -> y to (image.width - 1 - x)
                }
                System.arraycopy(source, y * image.stride + x * 4, turned, (ty * width + tx) * 4, 4)
            }
        }
        return PageImage(width, height, width * 4, turned)
    }

    /**
     * [toDisplay] for a bitmap: a new bitmap turned by [turn], or the same
     * instance when [turn] is `NONE`. The same performance note applies.
     */
    public fun toDisplay(bitmap: Bitmap): Bitmap {
        if (turn == RenderRotation.NONE) return bitmap
        val matrix = Matrix().apply { postRotate(rotation.toFloat()) }
        return Bitmap.createBitmap(bitmap, 0, 0, bitmap.width, bitmap.height, matrix, false)
    }

    private fun bounds(rect: Rect, convert: (Point) -> Point): Rect {
        // Converts the two opposite corners: a quarter turn maps a rect's
        // corners onto the result's corners, so they bound it.
        val a = convert(Point(rect.x, rect.y))
        val b = convert(Point(rect.x + rect.width, rect.y + rect.height))
        val minX = minOf(a.x, b.x)
        val minY = minOf(a.y, b.y)
        return Rect(minX, minY, maxOf(a.x, b.x) - minX, maxOf(a.y, b.y) - minY)
    }
}

/**
 * The quarter turn nearest [degrees] clockwise. Any value is accepted; it
 * reduces to `0 until 360` first, and a value halfway between two turns takes
 * the larger.
 */
public fun uniffi.epdf_plugin_render.RenderRotation.Companion.fromDegrees(degrees: Int): RenderRotation {
    var reduced = degrees % 360
    if (reduced < 0) reduced += 360
    return when ((reduced + 45) / 90 % 4) {
        1 -> RenderRotation.CLOCKWISE90
        2 -> RenderRotation.CLOCKWISE180
        3 -> RenderRotation.CLOCKWISE270
        else -> RenderRotation.NONE
    }
}

/** The quarter turn nearest [degrees] clockwise, rounded as the `Int` overload rounds. A value that is not finite is `NONE`. */
public fun uniffi.epdf_plugin_render.RenderRotation.Companion.fromDegrees(degrees: Double): RenderRotation {
    if (!degrees.isFinite()) return RenderRotation.NONE
    // The remainder is within one full turn, so the quarter count fits an Int.
    val quarters = floor((degrees % 360) / 90 + 0.5)
    return fromDegrees(quarters.toInt() * 90)
}

/** Degrees clockwise: 0, 90, 180 or 270. */
public val RenderRotation.degrees: Int
    get() = when (this) {
        RenderRotation.NONE -> 0
        RenderRotation.CLOCKWISE90 -> 90
        RenderRotation.CLOCKWISE180 -> 180
        RenderRotation.CLOCKWISE270 -> 270
    }

/** Whether the turn swaps width and height: 90 or 270 degrees. */
public val RenderRotation.isQuarterTurn: Boolean
    get() = this == RenderRotation.CLOCKWISE90 || this == RenderRotation.CLOCKWISE270

/** Converts between this page's unrotated space and the page as shown. */
public val PageInfo.geometry: PageGeometry
    get() = PageGeometry(RenderRotation.fromDegrees(rotation.toLong().toInt()), Size(width, height))
