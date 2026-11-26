package com.embedpdf.compose

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.toArgb
import com.embedpdf.AnnotationColor
import com.embedpdf.PageImage

/**
 * These pixels as an [ImageBitmap], through [PageImage.toBitmap]: a new
 * `ARGB_8888` bitmap of `width * height * 4` bytes, premultiplied so a
 * translucent pixel keeps its colour. An empty raster throws `invalid-input`.
 */
public fun PageImage.toImageBitmap(): ImageBitmap = toBitmap().asImageBitmap()

/** The annotation colour as an opaque Compose [Color]. */
public fun AnnotationColor.toColor(): Color = Color(toColorInt())

/**
 * The red, green and blue of this colour as an annotation colour, converted
 * to sRGB first. Its alpha is dropped: an annotation's opacity is its own
 * member.
 */
public fun Color.toAnnotationColor(): AnnotationColor = AnnotationColor.fromColorInt(toArgb())
