package com.embedpdf.compose

import android.content.ContentResolver
import android.net.Uri
import androidx.activity.result.contract.ActivityResultContracts
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.FileNotFoundException

/**
 * Saves an attachment's bytes to a document the user creates with the
 * system's document picker, as [AttachmentPanel] does for "Save copy…".
 *
 * Arbitrary attachment bytes are offered as [MIME_TYPE] whatever the
 * attachment's own type; the suggested file name keeps the attachment's
 * extension.
 */
public object AttachmentExportDocument {
    /** The type every saved copy is offered as. */
    public const val MIME_TYPE: String = "application/octet-stream"

    /** The type a copy of an attachment of [mimeType] is offered as: always [MIME_TYPE]. */
    @Suppress("UNUSED_PARAMETER")
    public fun mimeType(mimeType: String?): String = MIME_TYPE

    /** The picker contract: its input is the suggested file name, its result the created document or `null`. */
    public fun contract(): ActivityResultContracts.CreateDocument = ActivityResultContracts.CreateDocument(MIME_TYPE)

    /** Writes [data] to the document at [uri], replacing what it held, on `Dispatchers.IO`. */
    public suspend fun write(data: ByteArray, uri: Uri, resolver: ContentResolver): Unit = withContext(Dispatchers.IO) {
        val stream = resolver.openOutputStream(uri, "wt") ?: throw FileNotFoundException("no document at $uri")
        stream.use { it.write(data) }
    }
}
