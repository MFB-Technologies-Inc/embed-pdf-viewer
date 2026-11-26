package com.embedpdf

import uniffi.epdf_plugin_interaction.FeedbackHandle

/**
 * Haptics, as the platform provides them. Every member runs on the viewer's
 * kernel thread: an implementation that needs the main thread posts there
 * itself, as [HapticFeedback] does.
 */
public typealias PlatformFeedback = uniffi.epdf_plugin_interaction.PlatformFeedback

/** `ViewerConfig.feedback`: `FeedbackConfig(provider)`, which installs the feedback plugin. */
public typealias FeedbackConfig = uniffi.epdf_plugin_interaction.FeedbackConfig
public typealias FeedbackImpactWeight = uniffi.epdf_plugin_interaction.FeedbackImpactWeight
public typealias FeedbackNotifyKind = uniffi.epdf_plugin_interaction.FeedbackNotifyKind

/**
 * The workspace's haptics. Get it with [PdfViewer.feedback]; the viewer needs
 * `ViewerConfig(feedback = FeedbackConfig(provider))`. It keeps the viewer
 * alive, and after `destroy` every call throws `instance-closed`. It has no
 * event stream: what it does reaches the [PlatformFeedback] provider.
 */
public class FeedbackCapability internal constructor(
    private val handle: FeedbackHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun selection() {
        viewer.call { handle.selection() }
    }

    /** `null` is the provider's default weight. */
    public suspend fun impact(weight: FeedbackImpactWeight? = null) {
        viewer.call { handle.impact(weight) }
    }

    public suspend fun notify(kind: FeedbackNotifyKind) {
        viewer.call { handle.notify(kind) }
    }
}

/** The feedback capability. Throws `unsupported` when the viewer was built without a feedback provider. */
public suspend fun PdfViewer.feedback(): FeedbackCapability = FeedbackCapability(call { host.feedback() }, this)
