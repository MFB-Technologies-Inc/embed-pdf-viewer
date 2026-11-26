package com.embedpdf.compose

import androidx.compose.foundation.layout.Spacer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.isSupported
import androidx.compose.ui.unit.dp
import com.embedpdf.PageGeometry
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.Quad
import com.embedpdf.SearchBlendMode
import com.embedpdf.SearchHit
import com.embedpdf.StageVisiblePage
import com.embedpdf.models.SearchModel
import com.embedpdf.models.geometry
import com.embedpdf.Rect as PageRect

/**
 * Highlights the search's hits on one page: each line of each hit on [page]
 * is filled with the search settings' highlight color, and the lines of the
 * active hit with its active color, blended with the page under them as the
 * settings' blend mode says. The engine paints no hits into the page's
 * tiles, so a reader that shows search draws them with this.
 *
 * The overlay fills its frame and maps the whole frame onto the page as
 * shown, as [TextSelectionOverlay] does, so size it to the page and place it
 * over the page's image. An upright line is a rect with 2 dp corners; a
 * turned one is its four corners. It reads [SearchModel.hits],
 * [SearchModel.activeHit] and [SearchModel.highlight] and redraws when any of
 * them changes, a settings update included.
 *
 * It does not run [model]: one search model serves every page, so run it
 * once, as the [SearchBar] showing it does. The overlay is decoration only.
 * It has no semantics, so TalkBack never focuses it, and takes no pointer
 * input, so touches reach whatever is under it.
 *
 * In a [StageReader] of your own, return it from the page overlay, under
 * anything that takes touches:
 *
 * ```kotlin
 * StageReader(reader) { visiblePage ->
 *     val info = pages.firstOrNull { it.page == visiblePage.page } ?: return@StageReader
 *     Box(Modifier.fillMaxSize()) {
 *         SearchHighlightOverlay(search, visiblePage, info, Modifier.fillMaxSize())
 *         TextSelectionOverlay(selection, visiblePage, info, Modifier.fillMaxSize())
 *     }
 * }
 * ```
 *
 * The colors and blend mode come from the settings unless given here, as a
 * web layer's CSS variables win over them. A setting's color is read as CSS:
 * `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`, `rgb()` and `rgba()`, the 16 basic
 * color names, `orange` and `transparent`. A color it can't read draws the
 * plugin's default for that slot, `#ffd500` or `#ff9632`. Below API 29 a
 * blend mode the platform can't draw falls back to multiply.
 *
 * @param model the search whose hits are drawn.
 * @param page the page under the overlay.
 * @param geometry the page's turn and unrotated size; the frame covers the
 *   page turned this way.
 * @param color the fill of a hit, over the setting's `color`.
 * @param activeColor the fill of the active hit, over the setting's `activeColor`.
 * @param blendMode how the fills blend with the page, over the setting's `blendMode`.
 */
@Composable
public fun SearchHighlightOverlay(
    model: SearchModel,
    page: PageRef,
    geometry: PageGeometry,
    modifier: Modifier = Modifier,
    color: Color? = null,
    activeColor: Color? = null,
    blendMode: BlendMode? = null,
) {
    val hits by model.hits.collectAsState()
    val active by model.activeHit.collectAsState()
    val highlight by model.highlight.collectAsState()
    // Performance: every page filters the whole list, once per change of it.
    val pageHits = remember(hits, page) { hits.filter { it.page == page } }
    val fill = color ?: cssColor(highlight.color) ?: DEFAULT_COLOR
    val activeFill = activeColor ?: cssColor(highlight.activeColor) ?: DEFAULT_ACTIVE_COLOR
    val blend = blendMode ?: drawableBlendMode(highlight.blendMode)
    Spacer(
        modifier.drawBehind {
            if (pageHits.isEmpty()) return@drawBehind
            val corner = CornerRadius(2.dp.toPx())
            for (hit in pageHits) {
                val tint = if (hit == active) activeFill else fill
                for (segment in hit.segments) {
                    val quad = segment.quad
                    if (quad.isUpright()) {
                        val box = localRect(quad.box(), size, geometry)
                        drawRoundRect(tint, box.topLeft, box.size, corner, blendMode = blend)
                    } else {
                        val path = Path().apply {
                            val corners = listOf(quad.upperLeft, quad.upperRight, quad.lowerRight, quad.lowerLeft)
                            corners.forEachIndexed { i, point ->
                                val at = localPoint(point, size, geometry)
                                if (i == 0) moveTo(at.x, at.y) else lineTo(at.x, at.y)
                            }
                            close()
                        }
                        drawPath(path, tint, blendMode = blend)
                    }
                }
            }
        },
    )
}

/**
 * An overlay for a page a stage shows, turned as the stage turns it, view
 * rotation included. [info] is the same page's `PageInfo`, for its
 * unrotated size.
 */
@Composable
public fun SearchHighlightOverlay(
    model: SearchModel,
    visiblePage: StageVisiblePage,
    info: PageInfo,
    modifier: Modifier = Modifier,
    color: Color? = null,
    activeColor: Color? = null,
    blendMode: BlendMode? = null,
) {
    SearchHighlightOverlay(model, visiblePage.page, visiblePage.geometry(info), modifier, color, activeColor, blendMode)
}

/** The plugin's default fills, for a setting this can't read. */
internal val DEFAULT_COLOR = Color(0xFFFFD500)
internal val DEFAULT_ACTIVE_COLOR = Color(0xFFFF9632)

/** Whether the quad is an axis-aligned box in page space, as the web layers test it. */
private fun Quad.isUpright(): Boolean =
    upperLeft.y == upperRight.y && lowerLeft.y == lowerRight.y && upperLeft.x == lowerLeft.x

/** The box from the upper left corner to the lower right one. */
private fun Quad.box(): PageRect =
    PageRect(upperLeft.x, upperLeft.y, lowerRight.x - upperLeft.x, lowerRight.y - upperLeft.y)

/**
 * The Compose blend mode for [mode]. Each setting has a Compose mode of the
 * same name, and `NORMAL` is source over. Below API 29 the platform draws
 * only the Porter-Duff modes, so another falls back to multiply, which keeps
 * the text under the fill readable as the default does.
 */
internal fun drawableBlendMode(mode: SearchBlendMode): BlendMode {
    val wanted = when (mode) {
        SearchBlendMode.NORMAL -> BlendMode.SrcOver
        SearchBlendMode.MULTIPLY -> BlendMode.Multiply
        SearchBlendMode.SCREEN -> BlendMode.Screen
        SearchBlendMode.OVERLAY -> BlendMode.Overlay
        SearchBlendMode.DARKEN -> BlendMode.Darken
        SearchBlendMode.LIGHTEN -> BlendMode.Lighten
        SearchBlendMode.COLOR_DODGE -> BlendMode.ColorDodge
        SearchBlendMode.COLOR_BURN -> BlendMode.ColorBurn
        SearchBlendMode.HARD_LIGHT -> BlendMode.Hardlight
        SearchBlendMode.SOFT_LIGHT -> BlendMode.Softlight
        SearchBlendMode.DIFFERENCE -> BlendMode.Difference
        SearchBlendMode.EXCLUSION -> BlendMode.Exclusion
        SearchBlendMode.HUE -> BlendMode.Hue
        SearchBlendMode.SATURATION -> BlendMode.Saturation
        SearchBlendMode.COLOR -> BlendMode.Color
        SearchBlendMode.LUMINOSITY -> BlendMode.Luminosity
    }
    return if (wanted.isSupported()) wanted else BlendMode.Multiply
}

/**
 * [text] as a CSS color: hex in 3, 4, 6 or 8 digits, `rgb()` or `rgba()`
 * with commas or spaces and an optional `/ alpha`, channels as numbers or
 * percentages, or one of [NAMED_COLORS]. `null` for anything else.
 */
internal fun cssColor(text: String): Color? {
    val css = text.trim().lowercase()
    if (css.startsWith("#")) return hexColor(css.substring(1))
    NAMED_COLORS[css]?.let { return it }
    val open = css.indexOf('(')
    if (open < 0 || !css.endsWith(")")) return null
    val name = css.substring(0, open).trim()
    if (name != "rgb" && name != "rgba") return null
    val body = css.substring(open + 1, css.length - 1)
    val (channelText, alphaText) = body.split('/').let { parts ->
        when (parts.size) {
            1 -> parts[0] to null
            2 -> parts[0] to parts[1]
            else -> return null
        }
    }
    val parts = channelText.split(',', ' ').map { it.trim() }.filter { it.isNotEmpty() }
    val channels = parts.take(3).map { channel(it, 255f) ?: return null }
    if (channels.size != 3) return null
    val alpha = when {
        alphaText != null && parts.size == 3 -> channel(alphaText.trim(), 1f) ?: return null
        alphaText == null && parts.size == 4 -> channel(parts[3], 1f) ?: return null
        alphaText == null && parts.size == 3 -> 1f
        else -> return null
    }
    return Color(channels[0] / 255f, channels[1] / 255f, channels[2] / 255f, alpha)
}

/** A channel: a number up to [max], or a percentage of it, clamped. */
private fun channel(text: String, max: Float): Float? {
    val value = if (text.endsWith("%")) text.dropLast(1).toFloatOrNull()?.let { it / 100f * max } else text.toFloatOrNull()
    return value?.takeIf { it.isFinite() }?.coerceIn(0f, max)
}

private fun hexColor(hex: String): Color? {
    if (hex.any { Character.digit(it, 16) < 0 }) return null
    val full = when (hex.length) {
        3, 4 -> hex.map { "$it$it" }.joinToString("")
        6, 8 -> hex
        else -> return null
    }
    val value = full.toLong(16)
    return if (full.length == 6) {
        Color(0xFF000000 or value)
    } else {
        // CSS puts alpha last.
        Color(((value and 0xFF) shl 24) or (value ushr 8))
    }
}

private val NAMED_COLORS: Map<String, Color> = mapOf(
    "black" to Color(0xFF000000),
    "silver" to Color(0xFFC0C0C0),
    "gray" to Color(0xFF808080),
    "grey" to Color(0xFF808080),
    "white" to Color(0xFFFFFFFF),
    "maroon" to Color(0xFF800000),
    "red" to Color(0xFFFF0000),
    "purple" to Color(0xFF800080),
    "fuchsia" to Color(0xFFFF00FF),
    "magenta" to Color(0xFFFF00FF),
    "green" to Color(0xFF008000),
    "lime" to Color(0xFF00FF00),
    "olive" to Color(0xFF808000),
    "yellow" to Color(0xFFFFFF00),
    "navy" to Color(0xFF000080),
    "blue" to Color(0xFF0000FF),
    "teal" to Color(0xFF008080),
    "aqua" to Color(0xFF00FFFF),
    "cyan" to Color(0xFF00FFFF),
    "orange" to Color(0xFFFFA500),
    "transparent" to Color(0x00000000),
)
