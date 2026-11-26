package com.embedpdf.models

import android.graphics.PointF
import android.graphics.RectF
import com.embedpdf.PageGeometry
import com.embedpdf.PageInfo
import com.embedpdf.Point
import com.embedpdf.RenderRotation
import com.embedpdf.Size
import com.embedpdf.StageVisiblePage
import com.embedpdf.fromDegrees

/**
 * The viewport point at [local], a point in this page's overlay with its
 * origin at the page's top left. Pass it to `StageCapability.viewportToPage`
 * for the page point.
 */
public fun StageVisiblePage.viewportPoint(local: PointF): Point = Point(screenX + local.x, screenY + local.y)

/**
 * The geometry of the page as the stage shows it. Its turn comes from
 * `transform.rotation`, which includes the stage's view rotation;
 * `PageInfo.geometry` has only the page's own rotation.
 *
 * @param info this page's [PageInfo], for its unrotated size.
 */
public fun StageVisiblePage.geometry(info: PageInfo): PageGeometry =
    PageGeometry(RenderRotation.fromDegrees(transform.rotation), Size(info.width, info.height))

/**
 * The point in unrotated page space at [local], a point in this page's
 * overlay with its origin at the page's top left. Both axes scale by the
 * page's width as shown over `transform.viewWidth`.
 *
 * When `transform.viewWidth` is not greater than zero, every [local] maps to
 * the page point at the top left of the page as shown, so the result is
 * finite for a page the stage has not sized.
 */
public fun StageVisiblePage.pagePoint(local: PointF, info: PageInfo): Point {
    val geometry = geometry(info)
    val scale = ratio(geometry.displaySize.width, transform.viewWidth)
    return geometry.toPage(Point(local.x * scale, local.y * scale))
}

/**
 * Where [pageRect], a rect in unrotated page space, sits in this page's
 * overlay, with the origin at the page's top left. The inverse of
 * [pagePoint].
 *
 * When the page's width as shown is not greater than zero, the result is an
 * empty rect at the origin, and it is too when `transform.viewWidth` is zero.
 */
public fun StageVisiblePage.localRect(pageRect: com.embedpdf.Rect, info: PageInfo): RectF {
    val geometry = geometry(info)
    val scale = ratio(transform.viewWidth, geometry.displaySize.width)
    val shown = geometry.toDisplay(pageRect)
    val left = (shown.x * scale).toFloat()
    val top = (shown.y * scale).toFloat()
    return RectF(left, top, left + (shown.width * scale).toFloat(), top + (shown.height * scale).toFloat())
}

/** `length / divisor`, or 0 when [divisor] is not greater than zero. */
private fun ratio(length: Double, divisor: Double): Double = if (divisor > 0) length / divisor else 0.0
