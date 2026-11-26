package com.embedpdf.compose

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.embedpdf.PageRef
import com.embedpdf.PageRenderResolution
import com.embedpdf.PdfException
import com.embedpdf.RenderCapability
import com.embedpdf.RenderPageOptions
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Renders one PDF page as an image, with loading and error states.
 *
 * The render restarts when [page], [scale], [isOnScreen] or [revision]
 * changes, and a cancelled render's result is dropped. The previous image
 * stays while a new scale or revision renders. A failure shows a localized
 * error only when no image is held yet, and reaches [onError] with the
 * engine's own message either way. The image is decorative: the host gives the page
 * its accessible label and any text the interface needs.
 *
 * Named for Swift's `PageImageView`, since `PageImage` is the facade's raster.
 *
 * @param render the render capability of the page's open document.
 * @param page a page of that document.
 * @param scale positive device pixels per PDF point for an on-screen page.
 * @param isOnScreen whether to render at [scale]; when false it renders
 *   [PageRenderResolution.OFF_SCREEN_DEVICE_WIDTH] pixels wide instead. A
 *   lazy list drives it with [trackingScrollVisibility].
 * @param revision a presentation revision from render invalidations; change
 *   it when the engine invalidates the page's pixels. Give the view a new
 *   `key` when switching render capabilities between documents.
 * @param onError receives each failed render's exception, on the main
 *   thread, for an app that logs or reports it.
 */
@Composable
public fun PageImageView(
    render: RenderCapability,
    page: PageRef,
    scale: Double,
    modifier: Modifier = Modifier,
    isOnScreen: Boolean = true,
    revision: Long = 0,
    onError: (PdfException) -> Unit = {},
) {
    val failed by rememberUpdatedState(onError)
    var rendered by remember { mutableStateOf<ImageBitmap?>(null) }
    var renderedPage by remember { mutableStateOf<PageRef?>(null) }
    var error by remember { mutableStateOf<PdfException?>(null) }

    LaunchedEffect(render, page, scale, isOnScreen, revision) {
        if (renderedPage != page) {
            rendered = null
            renderedPage = null
        }
        error = null
        try {
            val options = if (isOnScreen) {
                RenderPageOptions(scale = scale)
            } else {
                RenderPageOptions(width = PageRenderResolution.OFF_SCREEN_DEVICE_WIDTH)
            }
            val image = render.renderPage(page, options)
            // Off the main thread: a full page is megabytes of pixels to copy.
            val bitmap = withContext(Dispatchers.Default) { image.image.toImageBitmap() }
            rendered = bitmap
            renderedPage = page
        } catch (e: CancellationException) {
            // A newer render owns the view now.
            throw e
        } catch (e: PdfException) {
            error = e
            failed(e)
        }
    }

    Box(modifier, contentAlignment = Alignment.Center) {
        val image = rendered
        val failure = error
        when {
            image != null -> Image(
                bitmap = image,
                contentDescription = null,
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Fit,
            )
            failure != null -> Column(
                Modifier.padding(16.dp).semantics(mergeDescendants = true) {},
                verticalArrangement = Arrangement.spacedBy(4.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    stringResource(R.string.embedpdf_page_unavailable),
                    style = MaterialTheme.typography.titleSmall,
                    textAlign = TextAlign.Center,
                )
                Text(
                    stringResource(R.string.embedpdf_page_render_failed),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }
            else -> CircularProgressIndicator()
        }
    }
}
