package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_link.LinkEventListener
import uniffi.epdf_plugin_link.LinkHandle

public typealias PdfLinkTarget = uniffi.epdf_ffi.PdfLinkTarget
public typealias PdfLinkTargetGoto = uniffi.epdf_ffi.PdfLinkTarget.Goto
public typealias PdfLinkTargetUri = uniffi.epdf_ffi.PdfLinkTarget.Uri
public typealias PdfLinkTargetGotoRemote = uniffi.epdf_ffi.PdfLinkTarget.GotoRemote
public typealias PdfLinkTargetLaunch = uniffi.epdf_ffi.PdfLinkTarget.Launch
public typealias PdfLinkTargetJavascript = uniffi.epdf_ffi.PdfLinkTarget.Javascript
public typealias PdfLinkTargetNamed = uniffi.epdf_ffi.PdfLinkTarget.Named
public typealias PdfLinkTargetUnsupported = uniffi.epdf_ffi.PdfLinkTarget.Unsupported
public typealias PdfDestination = uniffi.epdf_ffi.PdfDestination
public typealias PdfDestinationXyz = uniffi.epdf_ffi.PdfDestination.Xyz
public typealias PdfDestinationFit = uniffi.epdf_ffi.PdfDestination.Fit
public typealias PdfDestinationFitH = uniffi.epdf_ffi.PdfDestination.FitH
public typealias PdfDestinationFitV = uniffi.epdf_ffi.PdfDestination.FitV
public typealias PdfDestinationFitR = uniffi.epdf_ffi.PdfDestination.FitR
public typealias PdfDestinationFitB = uniffi.epdf_ffi.PdfDestination.FitB
public typealias PdfDestinationFitBh = uniffi.epdf_ffi.PdfDestination.FitBh
public typealias PdfDestinationFitBv = uniffi.epdf_ffi.PdfDestination.FitBv
public typealias PageLinkTarget = uniffi.epdf_ffi.PageLinkTarget
public typealias PageLinkTargetGoto = uniffi.epdf_ffi.PageLinkTarget.Goto
public typealias PageLinkTargetUri = uniffi.epdf_ffi.PageLinkTarget.Uri
public typealias PageLinkTargetGotoRemote = uniffi.epdf_ffi.PageLinkTarget.GotoRemote
public typealias PageLinkTargetLaunch = uniffi.epdf_ffi.PageLinkTarget.Launch
public typealias PageLinkTargetJavascript = uniffi.epdf_ffi.PageLinkTarget.Javascript
public typealias PageLinkTargetNamed = uniffi.epdf_ffi.PageLinkTarget.Named
public typealias PageLinkTargetUnsupported = uniffi.epdf_ffi.PageLinkTarget.Unsupported
public typealias PageDestination = uniffi.epdf_ffi.PageDestination
public typealias PageDestinationXyz = uniffi.epdf_ffi.PageDestination.Xyz
public typealias PageDestinationFit = uniffi.epdf_ffi.PageDestination.Fit
public typealias PageDestinationFitH = uniffi.epdf_ffi.PageDestination.FitH
public typealias PageDestinationFitV = uniffi.epdf_ffi.PageDestination.FitV
public typealias PageDestinationFitR = uniffi.epdf_ffi.PageDestination.FitR
public typealias PageDestinationFitB = uniffi.epdf_ffi.PageDestination.FitB
public typealias PageDestinationFitBh = uniffi.epdf_ffi.PageDestination.FitBh
public typealias PageDestinationFitBv = uniffi.epdf_ffi.PageDestination.FitBv
public typealias AnnotationRef = uniffi.epdf_ffi.AnnotationRef
public typealias AnnotationRefObjectNumber = uniffi.epdf_ffi.AnnotationRef.ObjectNumber
public typealias AnnotationRefIndex = uniffi.epdf_ffi.AnnotationRef.Index
public typealias LinkItem = uniffi.epdf_plugin_link.LinkItem
public typealias LinkHoverFlags = uniffi.epdf_plugin_link.LinkHoverFlags
public typealias LinkInput = uniffi.epdf_plugin_link.LinkInput
public typealias LinkInputTarget = uniffi.epdf_plugin_link.LinkInput.Target
public typealias LinkInputLink = uniffi.epdf_plugin_link.LinkInput.Link
public typealias LinkResolution = uniffi.epdf_plugin_link.LinkResolution
public typealias LinkResolutionDestination = uniffi.epdf_plugin_link.LinkResolution.Destination
public typealias LinkResolutionUri = uniffi.epdf_plugin_link.LinkResolution.Uri
public typealias LinkResolutionNamed = uniffi.epdf_plugin_link.LinkResolution.Named
public typealias LinkResolutionReported = uniffi.epdf_plugin_link.LinkResolution.Reported
public typealias LinkActivation = uniffi.epdf_plugin_link.LinkActivation
public typealias LinkActivationRevealed = uniffi.epdf_plugin_link.LinkActivation.Revealed
public typealias LinkActivationDestination = uniffi.epdf_plugin_link.LinkActivation.Destination
public typealias LinkActivationUri = uniffi.epdf_plugin_link.LinkActivation.Uri
public typealias LinkActivationNamed = uniffi.epdf_plugin_link.LinkActivation.Named
public typealias LinkActivationReported = uniffi.epdf_plugin_link.LinkActivation.Reported
public typealias LinkActivationDispatched = uniffi.epdf_plugin_link.LinkActivation.Dispatched
public typealias LinkActivationNone = uniffi.epdf_plugin_link.LinkActivation.None
public typealias LinkEvent = uniffi.epdf_plugin_link.LinkEvent
public typealias LinkEventActivated = uniffi.epdf_plugin_link.LinkEvent.Activated
public typealias LinkEventLoaded = uniffi.epdf_plugin_link.LinkEvent.Loaded
public typealias LinkActivatedEvent = uniffi.epdf_plugin_link.LinkActivatedEvent
public typealias LinkLoadedEvent = uniffi.epdf_plugin_link.LinkLoadedEvent

/**
 * A document's links: where they are, what they target, and activating them.
 * Get one with [PdfViewer.link]; the viewer needs
 * `ViewerConfig(interaction = true, link = true)`. Taps reach links once
 * [registerTapHandler] has run. It keeps the viewer alive. After its document
 * closes, every call throws `instance-closed`.
 *
 * The plugin never opens a URL. A [LinkActivationUri], returned or on
 * [observeEvents], is the app's to open.
 *
 * Targets cross as the engine's JSON: [LinkItem.targetJson] is the
 * page-space [PageLinkTarget] record (`{"kind": "uri", "uri": …}`), and a
 * destination resolution or activation carries the [PageDestination] record.
 */
public class LinkCapability internal constructor(
    private val handle: LinkHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun status(page: PageRef): String = viewer.call { handle.status(page) }

    /** Empty until the page loads ([ensureLoaded]). */
    public suspend fun links(page: PageRef): List<LinkItem> = viewer.call { handle.links(page) }

    public suspend fun link(id: String, page: PageRef): LinkItem? = viewer.call { handle.link(page, id) }

    /** The smallest link under a page point. */
    public suspend fun link(point: Point, page: PageRef): LinkItem? = viewer.call { handle.linkAt(page, point) }

    public suspend fun allLinks(): List<LinkItem> = viewer.call { handle.allLinks() }

    public suspend fun ensureLoaded(page: PageRef) {
        viewer.call { handle.ensureLoaded(page) }
    }

    public suspend fun isLoaded(page: PageRef): Boolean = viewer.call { handle.isLoaded(page) }

    /** What activating a target would do, with no side effect. */
    public suspend fun resolve(targetJson: String): LinkResolution = viewer.call { handle.resolve(targetJson) }

    /** What activating a typed target would do, with no side effect. */
    public suspend fun resolve(target: AnnotationLinkTarget): LinkResolution = resolve(target.json())

    public suspend fun activate(link: LinkItem): LinkActivation =
        viewer.call { handle.activate(LinkInputLink(page = link.page, id = link.id)) }

    public suspend fun activate(targetJson: String): LinkActivation =
        viewer.call { handle.activate(LinkInputTarget(targetJson)) }

    public suspend fun activate(target: AnnotationLinkTarget): LinkActivation = activate(target.json())

    /** `null` when no link is under the point. */
    public suspend fun activate(point: Point, page: PageRef): LinkActivation? = viewer.call { handle.activateAt(page, point) }

    public suspend fun label(link: LinkItem): String =
        viewer.call { handle.label(LinkInputLink(page = link.page, id = link.id)) }

    /**
     * A label for a target that isn't a link on a page (an annotation's link,
     * an outline entry): the URL, `Go to destination`, ...
     */
    public suspend fun label(targetJson: String): String = viewer.call { handle.label(LinkInputTarget(targetJson)) }

    public suspend fun label(target: AnnotationLinkTarget): String = label(target.json())

    public suspend fun isNavigationEngaged(): Boolean = viewer.call { handle.isNavigationEngaged() }

    /**
     * Lets taps on the interaction hub follow links: under a `link-nav` tool,
     * a tap that goes down and up on one link activates it, and the outcome
     * arrives on [observeEvents] (a [LinkActivationUri] is the app's to open).
     * A reader that forwards its pointer samples with
     * [InteractionCapability.dispatchPointer] calls this once per document. A
     * drag that starts on a link still scrolls.
     */
    public suspend fun registerTapHandler(): Registration = Registration(viewer.call { handle.registerTapHandler() })

    /**
     * Activations and page loads. Completes when the document closes (at once
     * if it already has) or the viewer is destroyed. Each collection is a new
     * subscription.
     */
    public fun observeEvents(): Flow<LinkEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : LinkEventListener {
                override fun onEvent(event: LinkEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The link capability for [document]. Throws `unsupported` when the viewer was
 * built without the link plugin, whatever the document's state.
 */
public suspend fun PdfViewer.link(document: DocumentInfo): LinkCapability =
    LinkCapability(call { host.link(document.id) }, this)
