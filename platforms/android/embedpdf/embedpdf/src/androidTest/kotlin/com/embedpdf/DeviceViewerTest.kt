package com.embedpdf

import android.graphics.Color
import android.system.Os
import android.system.OsConstants
import android.util.Log
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * The engine on a real device: the bionic loader, the device's page size and
 * Android's own `Bitmap`, which the JVM tests replace with a Linux library and
 * Robolectric.
 */
@RunWith(AndroidJUnit4::class)
class DeviceViewerTest {
    private object Silent : PlatformFeedback {
        override fun selection() {}

        override fun impact(weight: FeedbackImpactWeight?) {}

        override fun notify(kind: FeedbackNotifyKind) {}
    }

    /** `ViewerHostLoadTest`'s config: every plugin the host has. */
    private fun everyPlugin() = ViewerConfig(
        metadata = true,
        search = SearchConfig(),
        render = RenderConfig(),
        interaction = true,
        feedback = FeedbackConfig(Silent),
        stage = StageConfig(),
        selection = true,
        link = true,
        annotation = "",
        form = FormConfig(validation = FormValidation.NONE),
        redaction = RedactionConfig(),
        measurement = MeasurementConfig(),
        stamp = StampConfig(),
        pageEdit = true,
        viewManager = true,
        shell = true,
        i18n = I18nConfig(
            locale = null,
            fallbackLocale = null,
            locales = listOf(LocalePack(code = "en", name = "English", dir = null, translations = mapOf("hello" to "Hello"))),
            lazyLocales = emptyList(),
            loader = null,
        ),
        commands = true,
        standardCommands = StandardCommandList.entries.toList(),
        actions = ActionsConfig(policy = null, triggers = null, openSequence = OpenSequence.OFF, javascript = null, utcOffsetMinutes = null),
        signature = SignatureConfig(),
        history = true,
        outline = true,
        attachment = true,
    )

    private val pageSize: Long = Os.sysconf(OsConstants._SC_PAGESIZE)

    /**
     * On a 16 KB page device the library maps only when its LOAD segments and
     * its place in the APK are 16 KB aligned; a viewer that starts proves both.
     */
    @Test
    fun theEngineLoadsOnA16KbPageDevice() = onDevice {
        Log.i(TAG, "page size $pageSize")
        assumeTrue("this device's page size is $pageSize, not 16 KB", pageSize == 16_384L)
        val viewer = PdfViewer(ViewerConfig())
        viewer.start()
        // Stored uncompressed, the library maps straight out of the APK, so
        // the mapping names the APK rather than the library.
        val maps = File("/proc/self/maps").readLines()
        val library = maps.filter { it.contains("libepdf_viewer_host.so") }
        val fromApk = maps.filter { it.contains(".apk") && it.contains(" r-xp ") }
        Log.i(TAG, "library mappings:\n${library.joinToString("\n")}\nexecutable APK mappings:\n${fromApk.joinToString("\n")}")
        assertTrue("no executable mapping of the library", library.isNotEmpty() || fromApk.isNotEmpty())
        // Every executable mapping starts on a page boundary of this device.
        for (line in library + fromApk) {
            val offset = line.split(" ").filter { it.isNotEmpty() }[2].toLong(16)
            assertEquals(line, 0L, offset % pageSize)
        }
        viewer.destroy()
    }

    @Test
    fun everyPluginOpensRendersSearchesAndSaves() = onDevice {
        val viewer = PdfViewer(everyPlugin())
        viewer.start()
        val document = viewer.open(DeviceFixture.bytes("report"), name = "report.pdf")
        assertEquals(14u, document.pageCount)
        val pages = viewer.pages(document)
        assertEquals(14, pages.size)

        val rendered = viewer.render(document).renderPage(pages[0].page, RenderPageOptions(scale = 1.0))
        val bitmap = rendered.image.toBitmap()
        assertEquals(rendered.width, bitmap.width)
        assertEquals(rendered.height, bitmap.height)
        var inked = 0
        for (y in 0 until bitmap.height step 2) {
            for (x in 0 until bitmap.width step 2) {
                val color = bitmap.getPixel(x, y)
                if (Color.alpha(color) > 128 && Color.red(color) < 128 && Color.green(color) < 128 && Color.blue(color) < 128) inked++
            }
        }
        Log.i(TAG, "page 1 is ${bitmap.width}x${bitmap.height}, $inked dark samples")
        assertTrue("page 1 rendered blank", inked > 100)

        val hits = viewer.search(document).findAll(SearchQuery(text = "Report"))
        Log.i(TAG, "${hits.size} hits for Report")
        assertTrue("no search hits", hits.isNotEmpty())

        val saved = viewer.save(document)
        val rewritten = viewer.save(document, SaveMode.REWRITE)
        for (bytes in listOf(saved, rewritten)) {
            assertEquals(14u, viewer.open(bytes).pageCount)
        }
        viewer.destroy()
    }

    @Test
    fun aPasswordProtectedDocumentReportsItsLockedState() = onDevice {
        val viewer = PdfViewer(ViewerConfig(metadata = true))
        viewer.start()
        val locked = viewer.open(DeviceFixture.bytes("encrypted-hello"))
        assertEquals(DocumentStatus.LOCKED, locked.status)
        val wrong = expectPdfCode(PdfException.Code.PERMISSION_DENIED) { viewer.unlock(locked, "nope") }
        assertEquals("incorrect document password", wrong.detail)
        viewer.unlock(locked, "hôtel")
        assertEquals(DocumentStatus.READY, viewer.documents().first { it.id == locked.id }.status)
        viewer.destroy()
    }

    private companion object {
        const val TAG = "EmbedPdfDevice"
    }
}
