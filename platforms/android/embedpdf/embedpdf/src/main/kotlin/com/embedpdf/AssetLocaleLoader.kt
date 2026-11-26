package com.embedpdf

import android.content.res.AssetManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import java.io.FileNotFoundException

/**
 * Serves lazy locale packs from `<code>.json` files in an app's assets, under
 * [directory], in the web's locale file shape. Pass it as
 * `I18nConfig(lazyLocales = listOf("es"), loader = AssetLocaleLoader(context.assets))`.
 * Reads off the kernel thread, on [Dispatchers.IO].
 *
 * A missing file fails the switch with `operation-failed`, and a file that
 * isn't a locale pack with `invalid-input`.
 */
public class AssetLocaleLoader(
    private val assets: AssetManager,
    private val directory: String = "embedpdf/locales",
) : LocaleLoader {
    override fun load(code: String, completion: LocaleLoadCompletion) {
        scope.launch {
            val path = if (directory.isEmpty()) "$code.json" else "${directory.trimEnd('/')}/$code.json"
            val json = try {
                assets.open(path).use { it.readBytes().decodeToString() }
            } catch (e: FileNotFoundException) {
                completion.fail("no $path in the app's assets")
                return@launch
            } catch (e: Exception) {
                completion.fail(e)
                return@launch
            }
            try {
                completion.finish(LocalePack.decode(json))
            } catch (e: Exception) {
                completion.fail(e)
            }
        }
    }

    private companion object {
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    }
}
