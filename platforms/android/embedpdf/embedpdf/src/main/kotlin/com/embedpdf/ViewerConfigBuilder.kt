package com.embedpdf

import uniffi.epdf_plugin_actions.ActionsConfig
import uniffi.epdf_plugin_i18n.I18nConfig
import uniffi.epdf_plugin_interaction.FeedbackConfig
import uniffi.epdf_plugin_measurement.MeasurementConfig
import uniffi.epdf_plugin_redaction.RedactionConfig
import uniffi.epdf_plugin_render.RenderConfig
import uniffi.epdf_plugin_search.SearchConfig
import uniffi.epdf_plugin_signature.SignatureConfig
import uniffi.epdf_plugin_stage.StageConfig
import uniffi.epdf_plugin_stamp.StampConfig
import uniffi.epdf_viewer_host.StandardCommandList

/**
 * A chained way to build a [ViewerConfig]. Every method returns a changed copy,
 * so a builder is a plain value:
 *
 * ```kotlin
 * val viewer = PdfViewer(ViewerConfigBuilder().interaction().annotation().search(SearchConfig()))
 * ```
 *
 * An unset field is the engine's default for an absent field: a plugin is left
 * out (`false` or `null`, read by `Session::build` in
 * `crates/epdf-viewer-host/src/session.rs`, which installs a plugin only when
 * it is set) and `scope` is `null`, which the engine takes as `["*"]`. A
 * builder with no calls builds a config equal to `ViewerConfig()`.
 *
 * The builder exists so that a plugin field added to `ViewerConfig` is one new
 * method here and no change for a caller. `ViewerConfig(...)` with named
 * arguments keeps working. `ViewerConfigBuilderTest` fails when the two drift
 * apart.
 */
public class ViewerConfigBuilder(
    /** The config to start from, to change a few of its fields; `ViewerConfig()` leaves every plugin out. */
    config: ViewerConfig = ViewerConfig(),
) {
    // A copy, since `ViewerConfig`'s fields are mutable.
    private val config = config.copy()

    /** The [ViewerConfig] the calls so far describe. */
    public fun build(): ViewerConfig = config.copy()

    private inline fun changing(change: ViewerConfig.() -> ViewerConfig): ViewerConfigBuilder =
        ViewerConfigBuilder(config.change())

    /** The capability scope every document opens with. `null` is `["*"]`. */
    public fun scope(scope: List<String>?): ViewerConfigBuilder = changing { copy(scope = scope) }

    /** Installs the metadata plugin ([enabled]), or leaves it out. */
    public fun metadata(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(metadata = enabled) }

    public fun search(config: SearchConfig): ViewerConfigBuilder = changing { copy(search = config) }

    public fun render(config: RenderConfig): ViewerConfigBuilder = changing { copy(render = config) }

    /** The pointer hub that selection, link, annotation, form and measurement need. */
    public fun interaction(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(interaction = enabled) }

    public fun feedback(config: FeedbackConfig): ViewerConfigBuilder = changing { copy(feedback = config) }

    public fun stage(config: StageConfig): ViewerConfigBuilder = changing { copy(stage = config) }

    /** Needs [interaction]. */
    public fun selection(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(selection = enabled) }

    /** Needs [interaction]. */
    public fun link(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(link = enabled) }

    /**
     * Installs the annotation plugin. [settingsJson] is registered over the
     * defaults; `""` is the defaults. Needs [interaction].
     */
    public fun annotation(settingsJson: String = ""): ViewerConfigBuilder = changing { copy(annotation = settingsJson) }

    public fun history(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(history = enabled) }

    public fun outline(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(outline = enabled) }

    public fun attachment(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(attachment = enabled) }

    /** Needs [interaction]. */
    public fun form(config: FormConfig): ViewerConfigBuilder = changing { copy(form = config) }

    /** Needs [annotation]. */
    public fun redaction(config: RedactionConfig): ViewerConfigBuilder = changing { copy(redaction = config) }

    /** Needs [interaction] and [annotation]. */
    public fun measurement(config: MeasurementConfig): ViewerConfigBuilder = changing { copy(measurement = config) }

    public fun stamp(config: StampConfig): ViewerConfigBuilder = changing { copy(stamp = config) }

    public fun pageEdit(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(pageEdit = enabled) }

    public fun viewManager(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(viewManager = enabled) }

    public fun shell(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(shell = enabled) }

    public fun i18n(config: I18nConfig): ViewerConfigBuilder = changing { copy(i18n = config) }

    public fun commands(enabled: Boolean = true): ViewerConfigBuilder = changing { copy(commands = enabled) }

    /**
     * The owners' standard command lists, joined in upstream's order. Needs
     * [commands]; the clipboard and file parts go to the viewer's commands
     * platform.
     */
    public fun standardCommands(lists: List<StandardCommandList>): ViewerConfigBuilder =
        changing { copy(standardCommands = lists) }

    public fun actions(config: ActionsConfig): ViewerConfigBuilder = changing { copy(actions = config) }

    /** Needs [form]. */
    public fun signature(config: SignatureConfig): ViewerConfigBuilder = changing { copy(signature = config) }
}

/**
 * A [ViewerConfig] described by builder calls chained in [block]:
 * `viewerConfig { interaction().selection() }`. Each call returns a new
 * builder, so the block's result is what is built.
 */
public fun viewerConfig(block: ViewerConfigBuilder.() -> ViewerConfigBuilder): ViewerConfig =
    ViewerConfigBuilder().block().build()
