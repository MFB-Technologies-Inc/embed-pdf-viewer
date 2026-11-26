package com.embedpdf.bindings

import java.io.File
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import uniffi.epdf_ffi.ViewerException
import uniffi.epdf_plugin_actions.ActionsConfig
import uniffi.epdf_plugin_actions.OpenSequence
import uniffi.epdf_plugin_form.FormConfig
import uniffi.epdf_plugin_form.FormValidation
import uniffi.epdf_plugin_i18n.I18nConfig
import uniffi.epdf_plugin_i18n.LocalePack
import uniffi.epdf_plugin_interaction.FeedbackConfig
import uniffi.epdf_plugin_interaction.FeedbackImpactWeight
import uniffi.epdf_plugin_interaction.FeedbackNotifyKind
import uniffi.epdf_plugin_interaction.PlatformFeedback
import uniffi.epdf_plugin_measurement.MeasurementConfig
import uniffi.epdf_plugin_redaction.RedactionConfig
import uniffi.epdf_plugin_render.RenderConfig
import uniffi.epdf_plugin_search.SearchConfig
import uniffi.epdf_plugin_signature.SignatureConfig
import uniffi.epdf_plugin_stage.StageConfig
import uniffi.epdf_plugin_stamp.StampConfig
import uniffi.epdf_viewer_host.OpenDocumentOptions
import uniffi.epdf_viewer_host.StandardCommandList
import uniffi.epdf_viewer_host.ViewerConfig
import uniffi.epdf_viewer_host.ViewerDelegate
import uniffi.epdf_viewer_host.ViewerHost

/**
 * The full viewer host, with every plugin installed, loads on the JVM through
 * the generated Kotlin the AAR ships.
 *
 * This is the Kotlin side of `every_plugin_builds_together` in
 * `crates/epdf-viewer-host/tests/viewer_host.rs`: the config, a start, one
 * document and its pages. A component whose generated Kotlin disagrees with
 * the library (a checksum, a symbol, a type layout) fails here at the first
 * call that reaches it.
 */
class ViewerHostLoadTest {
    private fun fixture(name: String): ByteArray =
        File(System.getProperty("embedpdf.fixtures"), "$name.pdf").readBytes()

    private object Silent : PlatformFeedback {
        override fun selection() {}

        override fun impact(weight: FeedbackImpactWeight?) {}

        override fun notify(kind: FeedbackNotifyKind) {}
    }

    private object Delegate : ViewerDelegate {
        override fun stateChanged(revision: ULong) {}

        override fun report(error: ViewerException) {}
    }

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

    @Test
    fun everyPluginLoadsAndADocumentOpens() = runBlocking<Unit> {
        val viewer = ViewerHost(everyPlugin(), Delegate)
        try {
            viewer.start()
            val document = viewer.openDocument(fixture("report"), OpenDocumentOptions())

            assertEquals(14, document.pageCount.toInt())
            assertEquals(14, viewer.pages(document.id).size)
            assertTrue(viewer.documents().any { it.id == document.id })

            viewer.closeDocument(document.id)
            viewer.destroyViewer()
        } finally {
            viewer.shutdown()
            viewer.close()
        }
    }
}
