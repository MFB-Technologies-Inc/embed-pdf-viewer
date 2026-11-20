package com.embedpdf

import android.graphics.PointF
import android.graphics.RectF
import android.util.SizeF

// Conversions between the engine's geometry and Android's. The Android types
// hold `Float`, so a value converted there and back keeps `Float` precision.

/** The same point, as an Android value. */
public fun Point.toPointF(): PointF = PointF(x.toFloat(), y.toFloat())

/** The same point, from an Android value. */
public fun PointF.toPoint(): Point = Point(x.toDouble(), y.toDouble())

/** The same size, as an Android value. */
public fun Size.toSizeF(): SizeF = SizeF(width.toFloat(), height.toFloat())

/** The same size, from an Android value. */
public fun SizeF.toSize(): Size = Size(width.toDouble(), height.toDouble())

/** The same rect, as an Android value: `left`, `top`, `right`, `bottom`. */
public fun Rect.toRectF(): RectF = RectF(x.toFloat(), y.toFloat(), (x + width).toFloat(), (y + height).toFloat())

/**
 * The same rect, from an Android value. A rect whose `left` is past its
 * `right`, or `top` past `bottom`, is standardized: the origin is its minimum
 * corner.
 */
public fun RectF.toRect(): Rect {
    val minX = minOf(left, right).toDouble()
    val minY = minOf(top, bottom).toDouble()
    return Rect(minX, minY, maxOf(left, right) - minX, maxOf(top, bottom) - minY)
}

/** The four corners of this rect, with y pointing down: `upperLeft` is its origin. */
public fun Rect.toQuad(): Quad = Quad(
    upperLeft = Point(x, y),
    upperRight = Point(x + width, y),
    lowerLeft = Point(x, y + height),
    lowerRight = Point(x + width, y + height),
)
