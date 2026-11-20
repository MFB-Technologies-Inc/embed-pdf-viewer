package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import java.lang.reflect.Modifier
import java.net.URI
import kotlin.coroutines.cancellation.CancellationException

/**
 * A plugin left out of `ViewerConfig` is refused at its accessor, before any
 * call on a capability: the host asks the kernel whether the plugin is
 * installed ahead of any document lookup and answers `unsupported`. A
 * capability the accessor returns has its own plugin; a few of its calls still
 * refuse when a companion plugin is left out (see the Plugins section of
 * `platforms/apple/README.md`).
 *
 * One row per accessor. [everyPluginInViewerConfigHasARow] fails when
 * `ViewerConfig` gains a plugin field without a row here or in [pending].
 */
class DisabledPluginTest {
    class Accessor(
        /** The `ViewerConfig` field that installs the plugin. */
        val field: String,
        /** Installs the plugins this one needs to build, leaving it out. */
        val prerequisites: (ViewerConfig) -> ViewerConfig = { it },
        /** Installs the plugin itself. */
        val install: (ViewerConfig) -> ViewerConfig,
        /** Calls the accessor and discards the capability. */
        val call: suspend (PdfViewer, DocumentInfo) -> Unit,
    ) {
        override fun toString(): String = field

        fun config(installed: Boolean): ViewerConfig {
            val base = prerequisites(ViewerConfig())
            return if (installed) install(base) else base
        }
    }

    private val accessors = listOf(
        Accessor("metadata", install = { it.copy(metadata = true) }) { viewer, document -> viewer.metadata(document) },
        Accessor("search", install = { it.copy(search = SearchConfig()) }) { viewer, document -> viewer.search(document) },
    )

    /** Plugins whose accessors later waves of the port add. */
    private val pending = setOf(
        "render", "interaction", "feedback", "stage", "selection", "link", "annotation", "history",
        "outline", "attachment", "form", "redaction", "measurement", "stamp", "pageEdit", "viewManager", "shell",
        "i18n", "commands", "actions", "signature",
    )

    @Test
    fun everyPluginInViewerConfigHasARow() {
        val fields = ViewerConfig::class.java.declaredFields
            .filter { !Modifier.isStatic(it.modifiers) }
            .map { it.name }
            .toSet()
            // Not plugins: the permission scope, and the lists the commands plugin takes.
            .minus(setOf("scope", "standardCommands"))
        val rows = accessors.map { it.field }
        assertEquals(fields, rows.toSet() + pending)
        assertEquals(rows.size, rows.toSet().size)
        assertEquals(emptySet<String>(), rows.toSet().intersect(pending))
    }

    private suspend fun opened(accessor: Accessor, installed: Boolean): Pair<PdfViewer, DocumentInfo> {
        val viewer = started(accessor.config(installed))
        return viewer to viewer.open(Fixture.bytes("report"))
    }

    @Test
    fun aPluginLeftOutIsUnsupportedAtTheAccessor() = viewerTest {
        for (accessor in accessors) {
            val (viewer, document) = opened(accessor, installed = false)
            expectCode(PdfException.Code.UNSUPPORTED, "$accessor") { accessor.call(viewer, document) }
            viewer.destroy()
        }
    }

    @Test
    fun anInstalledPluginsAccessorSucceeds() = viewerTest {
        for (accessor in accessors) {
            val (viewer, document) = opened(accessor, installed = true)
            accessor.call(viewer, document)
            viewer.destroy()
        }
    }

    /**
     * The refusal does not depend on the document: a document the viewer has
     * closed is still `unsupported`, not `not-found`, when the plugin is left out.
     */
    @Test
    fun aPluginLeftOutIsUnsupportedForAClosedDocument() = viewerTest {
        for (accessor in accessors) {
            val (viewer, document) = opened(accessor, installed = false)
            viewer.close(document)
            expectCode(PdfException.Code.UNSUPPORTED, "$accessor") { accessor.call(viewer, document) }
            viewer.destroy()
        }
    }

    /** A document still loading is `unsupported`, not `not-ready`, when the plugin is left out. */
    @Test
    fun aPluginLeftOutIsUnsupportedForALoadingDocument() = viewerTest {
        for (accessor in accessors) {
            val viewer = PdfViewer(accessor.config(installed = false), HeldFetcher())
            viewer.start()
            val opening = async(start = CoroutineStart.UNDISPATCHED) {
                viewer.open(URI("https://embedpdf.test/held.pdf").toURL())
            }
            var loading: DocumentInfo? = null
            for (attempt in 0 until 500) {
                loading = viewer.documents().firstOrNull()
                if (loading != null) break
                delay(10)
            }
            val document = loading ?: error("no loading tab")
            assertEquals(DocumentStatus.LOADING, document.status)
            expectCode(PdfException.Code.UNSUPPORTED, "$accessor") { accessor.call(viewer, document) }
            opening.cancel()
            try {
                opening.await()
                fail("a cancelled open finished")
            } catch (e: CancellationException) {
                // Expected.
            }
            viewer.destroy()
        }
    }
}

/**
 * Answers no request, so an open through it stays pending and its tab stays
 * `LOADING` until the open is cancelled.
 */
class HeldFetcher : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation =
        object : DocumentFetchCancellation {
            override fun cancel() {
                completion.failWith(HostFailure(name = "AbortError", code = "operation-cancelled", detail = "operation cancelled"))
            }
        }
}
