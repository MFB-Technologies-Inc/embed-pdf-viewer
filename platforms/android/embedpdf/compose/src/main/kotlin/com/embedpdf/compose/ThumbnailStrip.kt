package com.embedpdf.compose

import android.graphics.Bitmap
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.geometry
import com.embedpdf.models.ThumbnailModel

/**
 * A scrolling strip of page thumbnails over a [ThumbnailModel].
 *
 * The strip is a lazy row, or a lazy column with [Orientation.Vertical].
 * Each cell is 96 dp across the strip and as long as the page's shape as
 * shown makes it, so a cell keeps its size when its image lands. A cell
 * loads its thumbnail when it is composed and again when the engine
 * invalidates its page; when it leaves, its load is cancelled, which cancels
 * the render unless another load of that page still waits on it. Until an
 * image lands the cell is a plain placeholder. The cell for [current] has an
 * outline in the theme's primary colour. The strip runs the model while it
 * is composed, so the app does not run it as well.
 *
 * Each cell is a button that calls [onSelect] with its page. TalkBack reads a
 * cell as "Page" and its one-based number, and [current] as selected. The
 * strip shows no error and does not scroll to [current]; the app does both
 * where it needs them. Under a right-to-left layout a row starts at the
 * right.
 *
 * Give the strip the same [pages] the model was made from, and a new model
 * when the page list changes.
 */
@Composable
public fun ThumbnailStrip(
    model: ThumbnailModel,
    pages: List<PageInfo>,
    current: PageRef?,
    onSelect: (PageInfo) -> Unit,
    modifier: Modifier = Modifier,
    orientation: Orientation = Orientation.Horizontal,
) {
    ModelScope(model) { thumbnails ->
        LaunchedEffect(thumbnails) { thumbnails.run() }
        val images by thumbnails.images.collectAsState()
        // Performance: one map holds every page's revision, so one page's
        // invalidation recomposes every cell the lazy list has made, not
        // only that page's. A flow per page limits it to its own cell.
        val revisions by thumbnails.pageRevisions.collectAsState()
        val cells: LazyListScope.() -> Unit = {
            items(pages, key = { it.page.pageObjectNumber.toLong() }) { page ->
                ThumbnailCell(
                    model = thumbnails,
                    page = page,
                    image = images[page.page],
                    revision = revisions[page.page] ?: 0,
                    isCurrent = page.page == current,
                    orientation = orientation,
                    onClick = { onSelect(page) },
                )
            }
        }
        val padding = PaddingValues(CELL_SPACING)
        val spacing = Arrangement.spacedBy(CELL_SPACING)
        when (orientation) {
            Orientation.Horizontal -> LazyRow(modifier, contentPadding = padding, horizontalArrangement = spacing, content = cells)
            Orientation.Vertical -> LazyColumn(modifier, contentPadding = padding, verticalArrangement = spacing, content = cells)
        }
    }
}

@Composable
private fun ThumbnailCell(
    model: ThumbnailModel,
    page: PageInfo,
    image: Bitmap?,
    revision: Long,
    isCurrent: Boolean,
    orientation: Orientation,
    onClick: () -> Unit,
) {
    LaunchedEffect(page.page, revision) { model.load(page.page) }
    val label = stringResource(R.string.embedpdf_page_number, page.index.toInt() + 1)
    val shown = page.geometry.displaySize
    val ratio = if (shown.width > 0 && shown.height > 0) (shown.width / shown.height).toFloat() else 1f
    val outline = if (isCurrent) MaterialTheme.colorScheme.primary else Color.Transparent
    val bitmap = remember(image) { image?.asImageBitmap() }
    Box(
        Modifier
            .then(if (orientation == Orientation.Horizontal) Modifier.height(CELL_BREADTH) else Modifier.width(CELL_BREADTH))
            .aspectRatio(ratio)
            .clickable(role = Role.Button, onClick = onClick)
            .semantics {
                contentDescription = label
                selected = isCurrent
            }
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .border(2.dp, outline),
    ) {
        if (bitmap != null) {
            Image(bitmap, contentDescription = null, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.FillBounds)
        }
    }
}

/** Between cells and around the strip. */
private val CELL_SPACING = 8.dp

/** A cell's length across the strip. */
private val CELL_BREADTH = 96.dp
