package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Test
import kotlin.concurrent.thread

/**
 * Serves `<code>.json` from the test classpath under [directory], off the
 * kernel thread: the JVM stand-in for [AssetLocaleLoader], which
 * `AssetLocaleLoaderTest` covers under Robolectric.
 */
class ResourceLocaleLoader(private val directory: String = "locales") : LocaleLoader {
    override fun load(code: String, completion: LocaleLoadCompletion) {
        thread(isDaemon = true) {
            val stream = javaClass.classLoader!!.getResourceAsStream("$directory/$code.json")
            if (stream == null) {
                completion.fail("no $code.json in $directory")
                return@thread
            }
            try {
                completion.finish(LocalePack.decode(stream.use { it.readBytes().decodeToString() }))
            } catch (e: Exception) {
                completion.fail(e)
            }
        }
    }
}

class I18nTest {
    companion object {
        val english = LocalePack(code = "en", name = "English", dir = null, translations = mapOf("hello" to "Hello", "greet" to "Hi {name}"))

        suspend fun started(lazy: List<String> = listOf("es"), directory: String = "locales"): Pair<PdfViewer, I18nCapability> {
            val viewer = started(
                ViewerConfig(
                    i18n = I18nConfig(
                        locale = "en",
                        locales = listOf(english),
                        lazyLocales = lazy,
                        loader = ResourceLocaleLoader(directory),
                    ),
                ),
            )
            return viewer to viewer.i18n()
        }
    }

    @Test
    fun translatesAndInterpolates() = viewerTest {
        val (viewer, i18n) = started()
        assertEquals("Hello", i18n.t("hello"))
        assertEquals("Hi Ana", i18n.t("greet", params = mapOf("name" to I18nParamText("Ana"))))
        assertEquals("Fallback", i18n.t("nope", fallback = "Fallback"))
        viewer.destroy()
    }

    @Test
    fun aLazySwitchReadsTheLoaderAndAnnounces() = viewerTest {
        val (viewer, i18n) = started()
        val events = subscribed(i18n.observeEvents())
        i18n.setLocale("es")
        val changed = events.nextMapped { it as? I18nEventLocaleChanged }
        assertEquals("es", changed.event.locale)
        assertEquals("en", changed.event.previousLocale)
        assertEquals("2 páginas", i18n.t("pages", params = mapOf("count" to I18nParamNumber(2.0))))
        assertEquals(listOf("en", "es"), i18n.locales().map { it.code })
        viewer.destroy()
    }

    @Test
    fun aLoaderFailureKeepsItsCodeInTheEvent() = viewerTest {
        val refusing = object : LocaleLoader {
            override fun load(code: String, completion: LocaleLoadCompletion) {
                completion.fail(PdfException(PdfException.Code.PERMISSION_DENIED, "offline policy"))
            }
        }
        val viewer = started(
            ViewerConfig(i18n = I18nConfig(locale = "en", locales = listOf(english), lazyLocales = listOf("es"), loader = refusing)),
        )
        val i18n = viewer.i18n()
        val events = subscribed(i18n.observeEvents())
        expectCode(PdfException.Code.OPERATION_FAILED) { i18n.setLocale("es") }
        val failed = events.nextMapped { it as? I18nEventLocaleLoadFailed }
        assertEquals("permission-denied", failed.event.error.code)
        assertEquals("offline policy", failed.event.error.message)
        viewer.destroy()
    }

    @Test
    fun aMissingPackFailsTheSwitch() = viewerTest {
        val (viewer, i18n) = started(directory = "nowhere")
        expectCode(PdfException.Code.OPERATION_FAILED) { i18n.setLocale("es") }
        assertEquals("en", i18n.locale())
        viewer.destroy()
    }

    @Test
    fun anUnknownLocaleIsNotFound() = viewerTest {
        val (viewer, i18n) = started()
        expectCode(PdfException.Code.NOT_FOUND) { i18n.setLocale("fr") }
        viewer.destroy()
    }

    @Test
    fun aRegisteredPackIsUsableAndRemovable() = viewerTest {
        val (viewer, i18n) = started()
        val registration = i18n.register(LocalePack(code = "de", name = "Deutsch", dir = null, translations = mapOf("hello" to "Hallo")))
        i18n.setLocale("de")
        assertEquals("Hallo", i18n.t("hello"))
        i18n.setLocale("en")
        registration.unregister()
        expectCode(PdfException.Code.NOT_FOUND) { i18n.setLocale("de") }
        viewer.destroy()
    }

    @Test
    fun addedTranslationsAreUsed() = viewerTest {
        val (viewer, i18n) = started()
        i18n.addTranslations(mapOf("zoom.in" to "Zoom in"), code = "en")
        assertEquals("Zoom in", i18n.t("zoom.in"))
        assertEquals(true, i18n.hasKey("zoom.in"))
        assertEquals(TextDirection.LTR, i18n.direction())
        assertEquals(null, i18n.loadingLocale())
        viewer.destroy()
    }

    @Test
    fun aConfigWithALoaderEqualsItself() {
        val config = I18nConfig(lazyLocales = listOf("es"), loader = ResourceLocaleLoader())
        assertEquals(config, config.copy())
        assertEquals(config.hashCode(), config.copy().hashCode())
    }

    @Test
    fun aLocaleFileDecodes() {
        val json = javaClass.classLoader!!.getResourceAsStream("locales/es.json")!!.use { it.readBytes().decodeToString() }
        val pack = LocalePack.decode(json)
        assertEquals("{count} páginas", pack.translations["pages.other"])
    }

    @Test
    fun aFileThatIsNotALocaleIsInvalidInput() = viewerTest {
        expectCode(PdfException.Code.INVALID_INPUT) { LocalePack.decode("""{"code": 3}""") }
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, i18n) = started()
        val collector = collecting(i18n.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        i18n.setLocale("es")
        viewer.destroy()
    }

    @Test
    fun aDestroyedViewersCapabilityThrows() = viewerTest {
        val (viewer, i18n) = started()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { i18n.locale() }
    }
}
