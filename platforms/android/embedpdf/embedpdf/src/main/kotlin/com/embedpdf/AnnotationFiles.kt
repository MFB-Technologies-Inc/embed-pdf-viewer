package com.embedpdf

import android.content.ContentResolver
import android.net.Uri
import android.provider.OpenableColumns
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.FileNotFoundException

// The files a stamp, an attachment or a file picker hands the annotation
// plugin, read from a document the user picked through the storage access
// framework (a `content:` URI) or any other URI a `ContentResolver` opens.
// Each reads the whole file into memory on `Dispatchers.IO`. A URI the
// resolver can't open throws `not-found`.

/**
 * A file attachment's file from [uri]. Its name is the provider's display
 * name, or the URI's last path segment when the provider has none; its MIME
 * type is the provider's.
 */
public suspend fun AnnotationAttachmentFile(
    resolver: ContentResolver,
    uri: Uri,
    description: String? = null,
): AnnotationAttachmentFile {
    val file = resolver.readFile(uri)
    return AnnotationAttachmentFile(file.bytes, file.name ?: "attachment", file.mimeType, description)
}

/** A stamp's image from [uri], with the provider's MIME type and display name. */
public suspend fun AnnotationStampSource(
    resolver: ContentResolver,
    uri: Uri,
    subject: String? = null,
): AnnotationStampSource {
    val file = resolver.readFile(uri)
    return AnnotationStampSource(file.bytes, file.mimeType, file.name, subject)
}

/**
 * What an [AnnotationFilePicker] answers for the document the user picked at
 * [uri], with the provider's display name and MIME type.
 */
public suspend fun AnnotationPickedFile(resolver: ContentResolver, uri: Uri): AnnotationPickedFile {
    val file = resolver.readFile(uri)
    return AnnotationPickedFile(file.bytes, file.name, file.mimeType)
}

private class ResolvedFile(val bytes: ByteArray, val name: String?, val mimeType: String?)

private suspend fun ContentResolver.readFile(uri: Uri): ResolvedFile = withContext(Dispatchers.IO) {
    try {
        val bytes = openInputStream(uri)?.use { it.readBytes() }
            ?: throw FileNotFoundException("The content provider has no stream for $uri.")
        ResolvedFile(bytes, displayName(uri) ?: uri.lastPathSegment, getType(uri))
    } catch (e: FileNotFoundException) {
        throw PdfException(PdfException.Code.NOT_FOUND, readable(e), "FileNotFoundException", e)
    }
}

private fun ContentResolver.displayName(uri: Uri): String? = try {
    query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
        if (cursor.moveToFirst() && !cursor.isNull(0)) cursor.getString(0) else null
    }
} catch (e: Exception) {
    // A provider without the column, or one that refuses queries: fall back
    // to the URI's own name.
    null
}
