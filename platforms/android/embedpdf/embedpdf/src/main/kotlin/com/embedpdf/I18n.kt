package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_i18n.I18nEventListener
import uniffi.epdf_plugin_i18n.I18nHandle
import uniffi.epdf_plugin_i18n.localePackFromJson

public typealias I18nSettings = uniffi.epdf_plugin_i18n.I18nSettings
public typealias I18nSettingsPatch = uniffi.epdf_plugin_i18n.I18nSettingsPatch
public typealias I18nConfig = uniffi.epdf_plugin_i18n.I18nConfig
public typealias LocalePack = uniffi.epdf_plugin_i18n.LocalePack
public typealias LocaleInfo = uniffi.epdf_plugin_i18n.LocaleInfo
public typealias TextDirection = uniffi.epdf_plugin_i18n.TextDirection
public typealias I18nParam = uniffi.epdf_plugin_i18n.I18nParam
public typealias I18nParamText = uniffi.epdf_plugin_i18n.I18nParam.Text
public typealias I18nParamNumber = uniffi.epdf_plugin_i18n.I18nParam.Number
public typealias I18nEvent = uniffi.epdf_plugin_i18n.I18nEvent
public typealias I18nEventLocaleChanged = uniffi.epdf_plugin_i18n.I18nEvent.LocaleChanged
public typealias I18nEventLocaleLoadFailed = uniffi.epdf_plugin_i18n.I18nEvent.LocaleLoadFailed
public typealias LocaleChangedEvent = uniffi.epdf_plugin_i18n.LocaleChangedEvent
public typealias LocaleLoadFailure = uniffi.epdf_plugin_i18n.LocaleLoadFailure

/**
 * Serves lazy locale packs. `load` is called on the viewer's kernel thread:
 * return at once, do the work elsewhere, and answer through the completion
 * from any thread. A completion dropped unanswered fails the switch with
 * `operation-failed`. [AssetLocaleLoader] serves packs from an app's assets.
 */
public typealias LocaleLoader = uniffi.epdf_plugin_i18n.LocaleLoader
public typealias LocaleLoadCompletion = uniffi.epdf_plugin_i18n.LocaleLoadCompletion

/**
 * A locale file in the web's shape (`{code, name, dir?, translations}`).
 * Throws `invalid-input` for a file that isn't one.
 */
public fun uniffi.epdf_plugin_i18n.LocalePack.Companion.decode(json: String): LocalePack =
    try {
        localePackFromJson(json)
    } catch (e: Exception) {
        throw PdfException.from(e)
    }

/**
 * Fails the load with [error], keeping its code (see [HostFailure]) in the
 * `LocaleLoadFailed` event.
 */
public fun LocaleLoadCompletion.fail(error: Throwable) {
    failWith(HostFailure(error))
}

/**
 * The viewer's locale packs and translations. There is one per viewer and it
 * works before any document opens. Get it with [PdfViewer.i18n]; the viewer
 * needs `ViewerConfig(i18n = I18nConfig(...))`. After `destroy`, every call
 * throws `instance-closed`.
 */
public class I18nCapability internal constructor(
    private val handle: I18nHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): I18nSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: I18nSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /**
     * The current locale's translation, then the fallback locale's, then
     * [fallback], then [key] itself. A number named `count` in [params] picks
     * a plural form.
     */
    public suspend fun t(key: String, params: Map<String, I18nParam> = emptyMap(), fallback: String? = null): String =
        viewer.call { handle.t(key, params, fallback) }

    public suspend fun hasKey(key: String): Boolean = viewer.call { handle.hasKey(key) }

    public suspend fun locale(): String = viewer.call { handle.locale() }

    public suspend fun direction(): TextDirection = viewer.call { handle.direction() }

    /** Registered packs, then lazy ones not loaded yet. */
    public suspend fun locales(): List<LocaleInfo> = viewer.call { handle.locales() }

    /** The code a [setLocale] is loading, if any. */
    public suspend fun loadingLocale(): String? = viewer.call { handle.loadingLocale() }

    /**
     * Returns once the locale is usable. Throws `not-found` for an unknown
     * code, `operation-failed` when the pack fails to load and
     * `operation-cancelled` when a newer call supersedes this one. Cancelling
     * the calling coroutine doesn't cancel the switch.
     */
    public suspend fun setLocale(code: String) {
        viewer.call { handle.setLocale(code) }
    }

    /** Dropping the registration keeps the pack; [Registration.unregister] removes it. */
    public suspend fun register(pack: LocalePack): Registration = Registration(viewer.call { handle.registerLocale(pack) })

    /** Flat keys (`"zoom.in"`); later keys win. */
    public suspend fun addTranslations(translations: Map<String, String>, code: String) {
        viewer.call { handle.addTranslations(code, translations) }
    }

    /**
     * Locale changes and load failures. Completes when the viewer is
     * destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<I18nEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : I18nEventListener {
                override fun onEvent(event: I18nEvent) {
                    channel.trySend(event)
                }
            },
        )
    }
}

/** The i18n capability. Throws `unsupported` when the viewer was built without the i18n plugin. */
public suspend fun PdfViewer.i18n(): I18nCapability = I18nCapability(call { host.i18n() }, this)
