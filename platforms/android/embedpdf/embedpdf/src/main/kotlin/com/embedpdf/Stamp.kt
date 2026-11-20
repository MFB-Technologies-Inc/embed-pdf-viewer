package com.embedpdf

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_stamp.StampAssetPreview
import uniffi.epdf_plugin_stamp.StampEventListener
import uniffi.epdf_plugin_stamp.StampHandle
import java.io.File
import java.io.IOException
import java.net.URLDecoder

public typealias StampSettings = uniffi.epdf_plugin_stamp.StampSettings

public typealias StampConfig = uniffi.epdf_plugin_stamp.StampConfig
public typealias StampLibrary = uniffi.epdf_plugin_stamp.StampLibrary
public typealias StampAsset = uniffi.epdf_plugin_stamp.StampAsset
public typealias StampAssetKind = uniffi.epdf_plugin_stamp.StampAssetKind
public typealias StampMark = uniffi.epdf_plugin_stamp.StampMark
public typealias StampMarkInk = uniffi.epdf_plugin_stamp.StampMark.Ink
public typealias StampMarkText = uniffi.epdf_plugin_stamp.StampMark.Text
public typealias StampMarkImage = uniffi.epdf_plugin_stamp.StampMark.Image
public typealias StampMarkPdf = uniffi.epdf_plugin_stamp.StampMark.Pdf
public typealias StampAddAssetInput = uniffi.epdf_plugin_stamp.StampAddAssetInput
public typealias StampImportOptions = uniffi.epdf_plugin_stamp.StampImportOptions
public typealias StampCreateLibraryOptions = uniffi.epdf_plugin_stamp.StampCreateLibraryOptions
public typealias StampLibraryPatch = uniffi.epdf_plugin_stamp.StampLibraryPatch
public typealias StampAssetPatch = uniffi.epdf_plugin_stamp.StampAssetPatch
public typealias StampAssetFilter = uniffi.epdf_plugin_stamp.StampAssetFilter
public typealias StampBatchResult = uniffi.epdf_plugin_stamp.StampBatchResult
public typealias StampBatchFailure = uniffi.epdf_plugin_stamp.StampBatchFailure
public typealias StampErrorInfo = uniffi.epdf_plugin_stamp.StampErrorInfo
public typealias StampLibraryChangeReason = uniffi.epdf_plugin_stamp.StampLibraryChangeReason
public typealias StampEvent = uniffi.epdf_plugin_stamp.StampEvent
public typealias StampEventLibraryChanged = uniffi.epdf_plugin_stamp.StampEvent.LibraryChanged
public typealias StampEventLibraryCreated = uniffi.epdf_plugin_stamp.StampEvent.LibraryCreated
public typealias StampEventLibraryUpdated = uniffi.epdf_plugin_stamp.StampEvent.LibraryUpdated
public typealias StampEventLibraryDeleted = uniffi.epdf_plugin_stamp.StampEvent.LibraryDeleted
public typealias StampEventAssetCreated = uniffi.epdf_plugin_stamp.StampEvent.AssetCreated
public typealias StampEventAssetUpdated = uniffi.epdf_plugin_stamp.StampEvent.AssetUpdated
public typealias StampEventAssetDeleted = uniffi.epdf_plugin_stamp.StampEvent.AssetDeleted
public typealias StampEventArmChanged = uniffi.epdf_plugin_stamp.StampEvent.ArmChanged
public typealias StoredStampLibrary = uniffi.epdf_plugin_stamp.StoredStampLibrary

/**
 * Where [StampCapability.restore] reads libraries and
 * [StampCapability.persist] writes them, one PDF per library id.
 * [FileStampLibraryStore] keeps them in a directory. The calls arrive on an
 * engine thread, never the main thread, and may block there; a failure
 * throws [HostException].
 */
public typealias StampLibraryStore = uniffi.epdf_plugin_stamp.StampLibraryStore

/**
 * A stamp preview: the encoded bytes, as the engine cached or rendered them,
 * and their MIME type.
 */
public class StampPreview internal constructor(preview: StampAssetPreview) {
    /** The encoded image, or `null` when the asset has none. */
    public val bytes: ByteArray? = preview.bytes

    public val mimeType: String? = preview.mimeType

    /** The decoded image, or `null` when there are no bytes or `BitmapFactory` can't read them. */
    public fun toBitmap(): Bitmap? = bytes?.let { BitmapFactory.decodeByteArray(it, 0, it.size) }
}

/**
 * The workspace's stamp libraries. Get it with [PdfViewer.stamps]; the viewer
 * needs `ViewerConfig(stamp = StampConfig())`, plus
 * `interaction = true, annotation = ""` to place. One capability for the
 * whole viewer, and it keeps the viewer alive. After the viewer is destroyed,
 * every call throws `instance-closed`.
 *
 * Libraries open on their own engine thread. On that engine an asset can come
 * from PDF bytes (a library, or [StampAddAssetInput.source]), from ink and
 * text marks, or from a document's annotations ([createAsset] with a list of
 * annotations); the last two export through the engine's
 * `page.annotations.exportAppearance`.
 *
 * Without the annotation plugin, placing throws `permission-denied` and
 * [canPlace] reads `false`.
 *
 * A library or asset change, an import, an arm or a placement that started
 * lands even when the caller is cancelled, and the caller then sees its
 * cancellation rather than the new id (see the README's Cancellation
 * section). Read [libraries] or [assets] again after a cancelled one.
 */
public class StampCapability internal constructor(
    private val handle: StampHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): StampSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: StampConfig) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /** Whether [document] (the active document when `null`) can make a stamp from its annotations. */
    public suspend fun canCreateFromAnnotations(document: DocumentInfo? = null): Boolean =
        viewer.call { handle.canCreateFromAnnotations(document?.id) }

    /** Every library, or those of the given kinds (`stamps`, `signatures`). */
    public suspend fun libraries(kinds: List<String>? = null): List<StampLibrary> = viewer.call { handle.libraries(kinds) }

    public suspend fun library(id: String): StampLibrary? = viewer.call { handle.library(id) }

    public suspend fun assets(filter: StampAssetFilter? = null): List<StampAsset> = viewer.call { handle.assets(filter) }

    public suspend fun asset(id: String): StampAsset? = viewer.call { handle.asset(id) }

    /** The preview cached at import. `null` for an unknown asset. */
    public suspend fun assetPreview(id: String): StampPreview? = viewer.call { handle.assetPreview(id) }?.let(::StampPreview)

    /** A fresh render [width] device pixels wide. Cancelling the calling coroutine cancels the render. */
    public suspend fun renderAssetPreview(id: String, width: Double): StampPreview? =
        viewer.callCancellable { token -> handle.renderAssetPreview(id, width, token) }?.let(::StampPreview)

    /** The asset's own one-page PDF. */
    public suspend fun assetBytes(id: String): ByteArray? = viewer.call { handle.readAssetBytes(id) }

    /** The library's canonical PDF. Cancelling the calling coroutine cancels the export. */
    public suspend fun exportLibrary(id: String): ByteArray = viewer.callCancellable { token -> handle.exportLibrary(id, token) }

    /** Whether [document] (the active document when `null`) can take a stamp. */
    public suspend fun canPlace(document: DocumentInfo? = null): Boolean = viewer.call { handle.canPlace(document?.id) }

    public suspend fun canImport(): Boolean = viewer.call { handle.canImport() }

    /** Returns the new library's id. */
    public suspend fun createLibrary(name: String, options: StampCreateLibraryOptions = StampCreateLibraryOptions()): String =
        viewer.callCancellable { token -> handle.createLibrary(name, options, token) }

    public suspend fun updateLibrary(id: String, patch: StampLibraryPatch) {
        viewer.callCancellable { token -> handle.updateLibrary(id, patch, token) }
    }

    /**
     * Returns the library's id. A PDF without a `/Names /Pages` registry
     * imports one asset per page. Throws `invalid-input` for bytes that aren't
     * a PDF.
     */
    public suspend fun importLibrary(bytes: ByteArray, options: StampImportOptions = StampImportOptions()): String =
        viewer.callCancellable { token -> handle.importLibrary(bytes, options, token) }

    public suspend fun deleteLibrary(id: String) {
        viewer.callCancellable { token -> handle.deleteLibrary(id, token) }
    }

    /** Returns the new asset's id. */
    public suspend fun createAsset(input: StampAddAssetInput): String =
        viewer.callCancellable { token -> handle.createAsset(input, token) }

    /**
     * [annotations] on [page] of [document] become one stamp, through the
     * engine's `page.annotations.exportAppearance`. Returns the new asset's
     * id. An empty list throws `invalid-input`.
     */
    public suspend fun createAsset(
        annotations: List<AnnotationRef>,
        document: DocumentInfo,
        page: PageRef,
        input: StampAddAssetInput = StampAddAssetInput(),
    ): String = viewer.callCancellable { token ->
        handle.createAssetFromAnnotations(document.id, page, annotations, input, token)
    }

    public suspend fun updateAsset(id: String, patch: StampAssetPatch) {
        viewer.callCancellable { token -> handle.updateAsset(id, patch, token) }
    }

    public suspend fun deleteAsset(id: String) {
        viewer.callCancellable { token -> handle.deleteAsset(id, token) }
    }

    /** Returns the asset's new id (ids carry the library). */
    public suspend fun moveAsset(id: String, libraryId: String): String =
        viewer.callCancellable { token -> handle.moveAsset(id, libraryId, token) }

    public suspend fun duplicateAsset(id: String, label: String? = null): String =
        viewer.callCancellable { token -> handle.duplicateAsset(id, label, token) }

    /** The next tap in [document] places the asset. */
    public suspend fun arm(assetId: String, document: DocumentInfo, targetWidth: Double? = null) {
        viewer.callCancellable { token -> handle.armAsset(document.id, assetId, targetWidth, token) }
    }

    /** Places the asset with its middle on [center] (page space) and returns the annotation. */
    public suspend fun place(
        assetId: String,
        document: DocumentInfo,
        page: PageRef,
        center: Point,
        targetWidth: Double? = null,
        rotation: Double? = null,
    ): AnnotationRef = viewer.callCancellable { token ->
        handle.placeAsset(document.id, assetId, page, center, targetWidth, rotation, token)
    }

    /** The same placement on every page (`null`) or those listed. */
    public suspend fun place(
        assetId: String,
        document: DocumentInfo,
        pages: List<PageRef>?,
        center: Point,
        targetWidth: Double? = null,
        rotation: Double? = null,
    ): StampBatchResult = viewer.callCancellable { token ->
        handle.placeAssetOnPages(document.id, assetId, pages, center, targetWidth, rotation, token)
    }

    public suspend fun disarm(document: DocumentInfo) {
        viewer.call { handle.disarm(document.id) }
    }

    public suspend fun armedAsset(document: DocumentInfo): StampAsset? = viewer.call { handle.armedAsset(document.id) }

    /**
     * Imports every library [store] holds and returns their ids. One that
     * fails to import is skipped; a store that throws fails the restore with
     * its [HostException] as a `HostError`.
     */
    public suspend fun restore(store: StampLibraryStore): List<String> =
        viewer.callCancellable { token -> handle.restore(store, token) }

    /**
     * Writes each changed library to [store], except the ids in [except]: one
     * line of store work per library, a burst of changes one save. Runs until
     * [Registration.unregister] (work already asked for still finishes) or
     * until the viewer is destroyed. Dropping the registration keeps it
     * writing, as every [Registration] does.
     */
    public suspend fun persist(store: StampLibraryStore, except: List<String> = emptyList()): Registration =
        Registration(viewer.call { handle.persist(store, except) })

    /**
     * Library, asset and arming changes. Completes when the viewer is
     * destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<StampEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : StampEventListener {
                override fun onEvent(event: StampEvent) {
                    channel.trySend(event)
                }
            },
        )
    }
}

/**
 * The viewer's stamp libraries. Throws `unsupported` when the viewer was
 * built without the stamp plugin.
 */
public suspend fun PdfViewer.stamps(): StampCapability = StampCapability(call { host.stamp() }, this)

/**
 * A [StampLibraryStore] over one file per library in [directory], named
 * after the library's id with every character but an ASCII letter or digit
 * percent-encoded, plus `.pdf`. The constructor
 * creates the directory; one it can't create throws `IOException`. A write
 * goes to a temporary file first and then replaces the library's file, so a
 * reader sees the old library or the new one. Each call throws
 * [HostException] for a file it can't read, write or delete.
 */
public class FileStampLibraryStore(public val directory: File) : StampLibraryStore {
    init {
        if (!directory.isDirectory && !directory.mkdirs()) throw IOException("can't create $directory")
    }

    /** Every byte of the id's UTF-8 but an ASCII letter or digit is `%XX`; an ASCII id gets the name the Swift store gives it. */
    private fun file(id: String): File {
        val name = buildString {
            for (byte in id.toByteArray(Charsets.UTF_8)) {
                val char = (byte.toInt() and 0xff).toChar()
                if (char in 'a'..'z' || char in 'A'..'Z' || char in '0'..'9') append(char) else append("%%%02X".format(byte.toInt() and 0xff))
            }
        }
        return directory.resolve("$name.pdf")
    }

    override fun list(): List<StoredStampLibrary> = hostFailure {
        val files = directory.listFiles() ?: throw IOException("can't list $directory")
        files.filter { it.isFile && it.extension == "pdf" }
            .sortedBy { it.name }
            .map { StoredStampLibrary(URLDecoder.decode(it.nameWithoutExtension, "UTF-8"), it.readBytes()) }
    }

    override fun put(id: String, bytes: ByteArray) {
        hostFailure {
            val target = file(id)
            val temporary = File.createTempFile(".${target.name}", ".tmp", directory)
            try {
                temporary.writeBytes(bytes)
                if (!temporary.renameTo(target)) throw IOException("can't replace $target")
            } finally {
                temporary.delete()
            }
        }
    }

    override fun delete(id: String) {
        hostFailure {
            val target = file(id)
            if (target.exists() && !target.delete()) throw IOException("can't delete $target")
        }
    }

    private inline fun <T> hostFailure(body: () -> T): T = try {
        body()
    } catch (e: Exception) {
        throw HostExceptionFailed(e.toString())
    }
}
