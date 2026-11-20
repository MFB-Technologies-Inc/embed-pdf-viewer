package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import java.io.File
import java.nio.file.Files

// Robolectric for the preview's `BitmapFactory` decode; native graphics so
// the decode reads the PNG's real pixels.
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class StampTest {
    private val directories = mutableListOf<File>()

    private fun temporaryDirectory(): File = Files.createTempDirectory("stamps").toFile().also { directories += it }

    @After
    fun removeDirectories() {
        directories.forEach { it.deleteRecursively() }
    }

    private suspend fun started(): Pair<PdfViewer, StampCapability> {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", stamp = StampConfig()))
        return viewer to viewer.stamps()
    }

    data class Imported(val viewer: PdfViewer, val stamps: StampCapability, val library: String, val asset: StampAsset)

    /** A viewer with the shipped English library imported, and its first asset. */
    private suspend fun imported(): Imported {
        val (viewer, stamps) = started()
        val library = stamps.importLibrary(Fixture.bytes("stamps"))
        val asset = stamps.assets(StampAssetFilter(libraryId = library)).first()
        return Imported(viewer, stamps, library, asset)
    }

    @Test
    fun aLibraryImportsWithPreviews() = viewerTest {
        val (viewer, stamps, library, asset) = imported()
        assertEquals(asset.id, stamps.library(library)?.assetIds?.first())
        assertEquals(StampAssetKind.STAMP, asset.kind)
        val preview = stamps.assetPreview(asset.id) ?: error("no preview")
        assertEquals("image/png", preview.mimeType)
        val bitmap = preview.toBitmap()
        assertNotNull(bitmap)
        assertTrue(bitmap!!.width > 0)
        viewer.destroy()
    }

    @Test
    fun aPlainPdfImportsOneAssetPerPage() = viewerTest {
        val (viewer, stamps) = started()
        val library = stamps.importLibrary(Fixture.bytes("colors"))
        val pages = viewer.pages(viewer.open(Fixture.bytes("colors")))
        assertEquals(pages.size, stamps.assets(StampAssetFilter(libraryId = library)).size)
        viewer.destroy()
    }

    @Test
    fun importingAPngThrows() = viewerTest {
        val (viewer, stamps) = started()
        val png = byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
        expectCode(PdfException.Code.INVALID_INPUT) { stamps.importLibrary(png) }
        assertEquals(0, stamps.libraries().size)
        viewer.destroy()
    }

    @Test
    fun aPlacedStampIsAnAnnotation() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val ref = stamps.place(asset.id, document, page, Point(x = 100.0, y = 100.0))
        assertNotNull(viewer.annotations(document).get(ref))
        val batch = stamps.place(asset.id, document, pages = listOf(page), center = Point(x = 200.0, y = 200.0))
        assertEquals(1, batch.applied.size)
        viewer.destroy()
    }

    /** The companion refusal: without the annotation plugin placing is denied. */
    @Test
    fun placingWithoutTheAnnotationPluginIsDenied() = viewerTest {
        val viewer = started(ViewerConfig(stamp = StampConfig()))
        val stamps = viewer.stamps()
        val library = stamps.importLibrary(Fixture.bytes("stamps"))
        val asset = stamps.assets(StampAssetFilter(libraryId = library)).first()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        assertFalse(stamps.canPlace(document))
        expectCode(PdfException.Code.PERMISSION_DENIED) { stamps.place(asset.id, document, page, Point(x = 10.0, y = 10.0)) }
        viewer.destroy()
    }

    @Test
    fun placingIntoAClosedDocumentThrows() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        stamps.arm(asset.id, document)
        assertEquals(asset.id, stamps.armedAsset(document)?.id)
        viewer.close(document)
        // A closed document grants nothing: the placement's
        // `annotations:create` check refuses it first.
        expectCode(PdfException.Code.PERMISSION_DENIED) { stamps.place(asset.id, document, page, Point(x = 10.0, y = 10.0)) }
        assertNull(stamps.armedAsset(document))
        viewer.destroy()
    }

    @Test
    fun armingIsObservedAndDisarmingClearsIt() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        val events = subscribed(stamps.observeEvents())
        stamps.arm(asset.id, document)
        val armed = events.nextMapped { it as? StampEventArmChanged }
        assertEquals(document.id, armed.documentId)
        assertEquals(asset.id, armed.asset?.id)
        assertEquals(asset.id, stamps.armedAsset(document)?.id)
        stamps.disarm(document)
        assertNull(stamps.armedAsset(document))
        viewer.destroy()
    }

    @Test
    fun createAssetFromAnnotationsExportsThroughTheNativeEngine() = viewerTest {
        val (viewer, stamps) = started()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        // An empty list is refused.
        expectCode(PdfException.Code.INVALID_INPUT) { stamps.createAsset(emptyList(), document, page) }
        val square = viewer.annotations(document).create(
            """{"subtype": "square", "box": {"x": 72, "y": 72, "width": 40, "height": 40}}""",
            page,
        )
        val asset = stamps.createAsset(listOf(square), document, page)
        val bytes = stamps.assetBytes(asset) ?: error("no bytes")
        assertEquals("%PDF", String(bytes.copyOfRange(0, 4), Charsets.US_ASCII))
        viewer.destroy()
    }

    @Test
    fun aLibraryPersistsThroughAFileStore() = viewerTest {
        val store = FileStampLibraryStore(temporaryDirectory())
        val (first, stamps) = started()
        val persistence = stamps.persist(store)
        stamps.importLibrary(Fixture.bytes("stamps"))
        delay(200)
        persistence.unregister()
        first.destroy()
        val (viewer, again) = started()
        val restored = again.restore(store)
        assertEquals(1, restored.size)
        assertFalse(again.assets(StampAssetFilter(libraryId = restored.first())).isEmpty())
        viewer.destroy()
    }

    @Test
    fun aDroppedPersistRegistrationKeepsWriting() = viewerTest {
        val store = FileStampLibraryStore(temporaryDirectory())
        val (viewer, stamps) = started()
        stamps.persist(store)
        stamps.importLibrary(Fixture.bytes("stamps"))
        var written = emptyList<StoredStampLibrary>()
        for (attempt in 0 until 50) {
            if (written.isNotEmpty()) break
            delay(20)
            written = store.list()
        }
        assertEquals(1, written.size)
        viewer.destroy()
    }

    @Test
    fun theFileStoreRoundTripsIdsAndDeletes() {
        val directory = temporaryDirectory()
        val store = FileStampLibraryStore(directory.resolve("nested"))
        store.put("a/b c", byteArrayOf(1, 2))
        store.put("plain", byteArrayOf(3))
        assertEquals(listOf("a%2Fb%20c.pdf", "plain.pdf"), directory.resolve("nested").list()!!.sorted())
        assertEquals(listOf("a/b c", "plain"), store.list().map { it.id })
        store.put("plain", byteArrayOf(4))
        assertEquals(listOf(4.toByte()), store.list().last().bytes.toList())
        store.delete("plain")
        store.delete("missing")
        assertEquals(listOf("a/b c"), store.list().map { it.id })
    }

    @Test
    fun aDestroyedViewersPersistThrows() = viewerTest {
        val store = FileStampLibraryStore(temporaryDirectory())
        val (viewer, stamps) = started()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { stamps.persist(store) }
    }

    @Test
    fun aStoreThatThrowsFailsTheRestore() = viewerTest {
        val file = temporaryDirectory()
        val store = FileStampLibraryStore(file)
        // A file where the store expects its directory.
        file.deleteRecursively()
        file.writeBytes(ByteArray(0))
        val (viewer, stamps) = started()
        val error = expectCode(PdfException.Code.OPERATION_FAILED) { stamps.restore(store) }
        assertEquals("HostError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheStreamUnsubscribes() = viewerTest {
        val (viewer, stamps) = started()
        val job = launch(start = CoroutineStart.UNDISPATCHED) { stamps.observeEvents().collect {} }
        delay(20)
        job.cancel()
        job.join()
        stamps.importLibrary(Fixture.bytes("stamps"))
        viewer.destroy()
    }

    @Test
    fun destroyingTheViewerFinishesTheEvents() = viewerTest {
        val (viewer, stamps) = started()
        val events = collecting(stamps.observeEvents())
        delay(50)
        viewer.destroy()
        assertTrue(finishes(events))
    }

    @Test
    fun aDestroyedViewersCapabilityThrows() = viewerTest {
        val (viewer, stamps) = started()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { stamps.libraries() }
    }

    // One per member that takes a `CancelToken`, each called in an
    // already-cancelled coroutine.

    @Test
    fun importLibraryCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps) = started()
        val bytes = Fixture.bytes("stamps")
        expectCancelledBeforeItStarts { stamps.importLibrary(bytes) }
        assertEquals(0, stamps.libraries().size)
        viewer.destroy()
    }

    @Test
    fun createLibraryCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps) = started()
        expectCancelledBeforeItStarts { stamps.createLibrary("Mine") }
        viewer.destroy()
    }

    @Test
    fun updateLibraryCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, library, _) = imported()
        expectCancelledBeforeItStarts { stamps.updateLibrary(library, StampLibraryPatch(name = "Renamed")) }
        viewer.destroy()
    }

    @Test
    fun deleteLibraryCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, library, _) = imported()
        expectCancelledBeforeItStarts { stamps.deleteLibrary(library) }
        assertNotNull(stamps.library(library))
        viewer.destroy()
    }

    @Test
    fun exportLibraryCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, library, _) = imported()
        expectCancelledBeforeItStarts { stamps.exportLibrary(library) }
        viewer.destroy()
    }

    @Test
    fun renderAssetPreviewCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        expectCancelledBeforeItStarts { stamps.renderAssetPreview(asset.id, 64.0) }
        viewer.destroy()
    }

    @Test
    fun createAssetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, library, _) = imported()
        val source = Fixture.bytes("colors")
        expectCancelledBeforeItStarts { stamps.createAsset(StampAddAssetInput(libraryId = library, source = source)) }
        viewer.destroy()
    }

    @Test
    fun createAssetFromAnnotationsCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps) = started()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        expectCancelledBeforeItStarts { stamps.createAsset(emptyList(), document, page) }
        viewer.destroy()
    }

    @Test
    fun updateAssetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        expectCancelledBeforeItStarts { stamps.updateAsset(asset.id, StampAssetPatch(label = "Renamed")) }
        viewer.destroy()
    }

    @Test
    fun deleteAssetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        expectCancelledBeforeItStarts { stamps.deleteAsset(asset.id) }
        assertNotNull(stamps.asset(asset.id))
        viewer.destroy()
    }

    @Test
    fun moveAssetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val other = stamps.createLibrary("Other")
        expectCancelledBeforeItStarts { stamps.moveAsset(asset.id, other) }
        viewer.destroy()
    }

    @Test
    fun duplicateAssetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        expectCancelledBeforeItStarts { stamps.duplicateAsset(asset.id) }
        viewer.destroy()
    }

    @Test
    fun armCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        expectCancelledBeforeItStarts { stamps.arm(asset.id, document) }
        assertNull(stamps.armedAsset(document))
        viewer.destroy()
    }

    @Test
    fun placeCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        expectCancelledBeforeItStarts { stamps.place(asset.id, document, page, Point(x = 10.0, y = 10.0)) }
        viewer.destroy()
    }

    @Test
    fun placeOnPagesCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps, _, asset) = imported()
        val document = viewer.open(Fixture.bytes("report"))
        expectCancelledBeforeItStarts { stamps.place(asset.id, document, pages = null, center = Point(x = 10.0, y = 10.0)) }
        viewer.destroy()
    }

    @Test
    fun restoreCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, stamps) = started()
        val store = FileStampLibraryStore(temporaryDirectory())
        expectCancelledBeforeItStarts { stamps.restore(store) }
        viewer.destroy()
    }

    /** `SignatureTransportTests.stampImportOptionsIdNamesTheLibrary`. */
    @Test
    fun stampImportOptionsIdNamesTheLibrary() = viewerTest {
        val (viewer, stamps) = started()
        assertEquals("kotlin-library", stamps.importLibrary(Fixture.bytes("stamps"), StampImportOptions(id = "kotlin-library")))
        viewer.destroy()
    }
}
