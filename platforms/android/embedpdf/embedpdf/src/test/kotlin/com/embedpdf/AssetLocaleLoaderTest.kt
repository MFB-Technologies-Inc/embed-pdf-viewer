package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

/** Reads the packs in `src/test/assets/embedpdf/locales`. */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class AssetLocaleLoaderTest {
    private suspend fun started(loader: AssetLocaleLoader): Pair<PdfViewer, I18nCapability> {
        val viewer = started(
            ViewerConfig(
                i18n = I18nConfig(
                    locale = "en",
                    locales = listOf(I18nTest.english),
                    lazyLocales = listOf("es", "broken", "fr"),
                    loader = loader,
                ),
            ),
        )
        return viewer to viewer.i18n()
    }

    private val assets get() = RuntimeEnvironment.getApplication().assets

    @Test
    fun aLazySwitchReadsTheAsset() = viewerTest {
        val (viewer, i18n) = started(AssetLocaleLoader(assets))
        i18n.setLocale("es")
        assertEquals("Hola", i18n.t("hello"))
        assertEquals("2 páginas", i18n.t("pages", params = mapOf("count" to I18nParamNumber(2.0))))
        viewer.destroy()
    }

    @Test
    fun anotherDirectoryIsRead() = viewerTest {
        val (viewer, i18n) = started(AssetLocaleLoader(assets, directory = "embedpdf/locales/"))
        i18n.setLocale("es")
        assertEquals("Hola", i18n.t("hello"))
        viewer.destroy()
    }

    @Test
    fun aMissingAssetFailsTheSwitchAndNamesThePath() = viewerTest {
        val (viewer, i18n) = started(AssetLocaleLoader(assets))
        val events = subscribed(i18n.observeEvents())
        expectCode(PdfException.Code.OPERATION_FAILED) { i18n.setLocale("fr") }
        val failed = events.nextMapped { it as? I18nEventLocaleLoadFailed }
        assertEquals("no embedpdf/locales/fr.json in the app's assets", failed.event.error.message)
        assertEquals("en", i18n.locale())
        viewer.destroy()
    }

    @Test
    fun anAssetThatIsNotALocalePackKeepsItsCode() = viewerTest {
        val (viewer, i18n) = started(AssetLocaleLoader(assets))
        val events = subscribed(i18n.observeEvents())
        expectCode(PdfException.Code.OPERATION_FAILED) { i18n.setLocale("broken") }
        val failed = events.nextMapped { it as? I18nEventLocaleLoadFailed }
        assertEquals("invalid-input", failed.event.error.code)
        viewer.destroy()
    }
}
