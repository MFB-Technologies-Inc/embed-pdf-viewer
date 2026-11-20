package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import uniffi.epdf_plugin_actions.ActionsConfig
import uniffi.epdf_plugin_actions.OpenSequence
import uniffi.epdf_plugin_form.FormConfig
import uniffi.epdf_plugin_i18n.I18nConfig
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
import uniffi.epdf_viewer_host.StandardCommandList
import java.lang.reflect.Modifier

private object SilentFeedback : PlatformFeedback {
    override fun selection() {}

    override fun impact(weight: FeedbackImpactWeight?) {}

    override fun notify(kind: FeedbackNotifyKind) {}
}

/**
 * `ViewerConfigBuilder` has one method per `ViewerConfig` field, and an unset
 * field is the generated default. [everyFieldHasARow] fails when `ViewerConfig`
 * gains a field without a row here, and a row only passes when its method sets
 * that field and no other.
 */
class ViewerConfigBuilderTest {
    private val rows: List<Pair<String, (ViewerConfigBuilder) -> ViewerConfigBuilder>> = listOf(
        "scope" to { it.scope(listOf("doc.open")) },
        "metadata" to { it.metadata() },
        "search" to { it.search(SearchConfig()) },
        "render" to { it.render(RenderConfig()) },
        "interaction" to { it.interaction() },
        "feedback" to { it.feedback(FeedbackConfig(SilentFeedback)) },
        "stage" to { it.stage(StageConfig()) },
        "selection" to { it.selection() },
        "link" to { it.link() },
        "annotation" to { it.annotation() },
        "history" to { it.history() },
        "outline" to { it.outline() },
        "attachment" to { it.attachment() },
        "form" to { it.form(FormConfig()) },
        "redaction" to { it.redaction(RedactionConfig()) },
        "measurement" to { it.measurement(MeasurementConfig()) },
        "stamp" to { it.stamp(StampConfig()) },
        "pageEdit" to { it.pageEdit() },
        "viewManager" to { it.viewManager() },
        "shell" to { it.shell() },
        "i18n" to { it.i18n(I18nConfig()) },
        "commands" to { it.commands() },
        "standardCommands" to { it.standardCommands(listOf(StandardCommandList.HISTORY)) },
        "actions" to { it.actions(ActionsConfig(openSequence = OpenSequence.OFF)) },
        "signature" to { it.signature(SignatureConfig()) },
    )

    /** Each field's value as text, in declaration order (`FeedbackConfig` holds an object, so `equals` would do too). */
    private fun fields(config: ViewerConfig): List<Pair<String, String>> =
        ViewerConfig::class.java.declaredFields
            .filter { !Modifier.isStatic(it.modifiers) }
            .map { field ->
                field.isAccessible = true
                field.name to "${field.get(config)}"
            }

    @Test
    fun everyFieldHasARow() {
        val fields = fields(ViewerConfig()).map { it.first }
        assertEquals(fields.toSet(), rows.map { it.first }.toSet())
        assertEquals(rows.size, rows.map { it.first }.toSet().size)
    }

    @Test
    fun anUnconfiguredBuilderIsTheGeneratedDefault() {
        assertEquals(ViewerConfig(), ViewerConfigBuilder().build())
        assertEquals(fields(ViewerConfig()), fields(ViewerConfigBuilder().build()))
    }

    @Test
    fun aMethodSetsItsFieldAndNothingElse() {
        val defaults = fields(ViewerConfig())
        for ((field, set) in rows) {
            val built = fields(set(ViewerConfigBuilder()).build())
            assertEquals(defaults.map { it.first }, built.map { it.first })
            for ((builtField, generated) in built.zip(defaults)) {
                if (builtField.first == field) {
                    assertNotEquals("$field was not set", generated.second, builtField.second)
                } else {
                    assertEquals("$field changed ${builtField.first}", generated.second, builtField.second)
                }
            }
        }
    }

    @Test
    fun aBuilderIsAValue() {
        val base = ViewerConfigBuilder().interaction()
        val changed = base.selection()
        assertEquals(false, base.build().selection)
        assertEquals(true, changed.build().selection)
    }

    @Test
    fun aBuilderStartsFromAnExistingConfigWithoutChangingIt() {
        val original = ViewerConfig(history = true)
        val config = ViewerConfigBuilder(original).outline().build()
        assertTrue(config.history && config.outline && !config.metadata)
        assertEquals(false, original.outline)
    }

    @Test
    fun aBuiltConfigIsNotSharedWithTheBuilder() {
        val builder = ViewerConfigBuilder().metadata()
        builder.build().metadata = false
        assertEquals(true, builder.build().metadata)
    }

    @Test
    fun theBlockFormBuildsWhatItsChainReturns() {
        assertEquals(ViewerConfig(interaction = true, selection = true), viewerConfig { interaction().selection() })
    }

    @Test
    fun aViewerTakesABuilder() = viewerTest {
        val viewer = PdfViewer(ViewerConfigBuilder().metadata(), UnusedFetcher)
        viewer.start()
        val document = viewer.open(Fixture.bytes("report"))
        viewer.call { viewer.host.metadata(document.id) }
        viewer.destroy()
    }
}
