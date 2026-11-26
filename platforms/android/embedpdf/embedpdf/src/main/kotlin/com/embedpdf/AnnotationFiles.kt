package com.embedpdf

import android.content.ContentResolver
import android.net.Uri
import android.provider.OpenableColumns
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.FileNotFoundException
import java.io.IOException
import java.io.InputStream

// The files a stamp, an attachment or a file picker hands the annotation
// plugin, read from a document the user picked through the storage access
// framework (a `content:` URI) or any other URI a `ContentResolver` opens.
// Each reads the whole file into memory on `Dispatchers.IO`, up to
// `maxBytes` (`DEFAULT_MAX_FILE_BYTES`, 64 MiB, unless given). A URI the
// resolver can't open throws `not-found`, one the app may not read
// `permission-denied`, a failed read `operation-failed`, and a file over
// `maxBytes` `invalid-input`.

/** The most bytes a picked file reads unless the call names its own cap: 64 MiB. */
public const val DEFAULT_MAX_FILE_BYTES: Long = 64L * 1024 * 1024

/**
 * A file attachment's file from [uri]. Its name is the provider's display
 * name, or the URI's last path segment when the provider has none; its MIME
 * type is the provider's.
 */
public suspend fun AnnotationAttachmentFile(
    resolver: ContentResolver,
    uri: Uri,
    description: String? = null,
    maxBytes: Long = DEFAULT_MAX_FILE_BYTES,
): AnnotationAttachmentFile {
    val file = resolver.readFile(uri, maxBytes)
    return AnnotationAttachmentFile(file.bytes, file.name ?: "attachment", file.mimeType, description)
}

/** A stamp's image from [uri], with the provider's MIME type and display name. */
public suspend fun AnnotationStampSource(
    resolver: ContentResolver,
    uri: Uri,
    subject: String? = null,
    maxBytes: Long = DEFAULT_MAX_FILE_BYTES,
): AnnotationStampSource {
    val file = resolver.readFile(uri, maxBytes)
    return AnnotationStampSource(file.bytes, file.mimeType, file.name, subject)
}

/**
 * What an [AnnotationFilePicker] answers for the document the user picked at
 * [uri], with the provider's display name and MIME type.
 */
public suspend fun AnnotationPickedFile(
    resolver: ContentResolver,
    uri: Uri,
    maxBytes: Long = DEFAULT_MAX_FILE_BYTES,
): AnnotationPickedFile {
    val file = resolver.readFile(uri, maxBytes)
    return AnnotationPickedFile(file.bytes, file.name, file.mimeType)
}

private class ResolvedFile(val bytes: ByteArray, val name: String?, val mimeType: String?)

private suspend fun ContentResolver.readFile(uri: Uri, maxBytes: Long): ResolvedFile = withContext(Dispatchers.IO) {
    require(maxBytes >= 0) { "maxBytes $maxBytes is negative" }
    try {
        val bytes = openInputStream(uri)?.use { it.readAtMost(maxBytes, uri) }
            ?: throw FileNotFoundException("The content provider has no stream for $uri.")
        ResolvedFile(bytes, displayName(uri) ?: uri.lastPathSegment, getType(uri))
    } catch (e: FileNotFoundException) {
        throw PdfException(PdfException.Code.NOT_FOUND, readable(e), "FileNotFoundException", e)
    } catch (e: SecurityException) {
        throw PdfException(PdfException.Code.PERMISSION_DENIED, readable(e), "SecurityException", e)
    } catch (e: IllegalArgumentException) {
        // A provider answers a URI it doesn't know this way.
        throw PdfException(PdfException.Code.INVALID_INPUT, readable(e), "IllegalArgumentException", e)
    } catch (e: IOException) {
        throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), e.javaClass.simpleName, e)
    }
}

/**
 * The whole stream, or `invalid-input` once it passes [maxBytes], which is
 * clamped to [MAX_ARRAY_BYTES]: no byte array holds more.
 */
private fun InputStream.readAtMost(requestedMaxBytes: Long, uri: Uri): ByteArray {
    val maxBytes = minOf(requestedMaxBytes, MAX_ARRAY_BYTES)
    val out = ByteArrayOutputStream()
    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
    var total = 0L
    while (true) {
        val count = read(buffer)
        if (count < 0) return out.toByteArray()
        total += count
        if (total > maxBytes) {
            throw PdfException(PdfException.Code.INVALID_INPUT, "$uri is larger than $maxBytes bytes", "FileTooLarge")
        }
        out.write(buffer, 0, count)
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

/** The largest array the JVM and ART reliably allocate. */
private const val MAX_ARRAY_BYTES: Long = Int.MAX_VALUE - 8L
