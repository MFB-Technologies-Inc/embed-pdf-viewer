package com.embedpdf

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.launch
import uniffi.epdf_plugin_actions.ActionsEventListener
import uniffi.epdf_plugin_actions.ActionsHandle
import uniffi.epdf_plugin_actions.DocumentVerbCompletion
import uniffi.epdf_plugin_actions.DocumentVerbOperation
import java.util.TimeZone
import kotlin.coroutines.CoroutineContext
import kotlin.coroutines.cancellation.CancellationException

public typealias ActionsSettings = uniffi.epdf_plugin_actions.ActionsSettings
public typealias ActionsSettingsPatch = uniffi.epdf_plugin_actions.ActionsSettingsPatch
public typealias ActionsJavaScriptSettings = uniffi.epdf_plugin_actions.ActionsJavaScriptSettings
public typealias ActionsJavaScriptSettingsPatch = uniffi.epdf_plugin_actions.ActionsJavaScriptSettingsPatch
public typealias ActionsScriptIdentitySetting = uniffi.epdf_plugin_actions.ActionsScriptIdentitySetting
public typealias ActionsScriptIdentitySettingValue = uniffi.epdf_plugin_actions.ActionsScriptIdentitySetting.Value
public typealias ActionsScriptIdentitySettingRegisteredProvider =
    uniffi.epdf_plugin_actions.ActionsScriptIdentitySetting.RegisteredProvider
public typealias ActionsScriptIdentity = uniffi.epdf_plugin_actions.ActionsScriptIdentity
public typealias ActionTriggers = uniffi.epdf_plugin_actions.ActionTriggers

public typealias ActionsConfig = uniffi.epdf_plugin_actions.ActionsConfig
public typealias ActionsJavaScriptConfig = uniffi.epdf_plugin_actions.ActionsJavaScriptConfig
public typealias ActionsScriptBudget = uniffi.epdf_plugin_actions.ActionsScriptBudget
public typealias ActionPolicy = uniffi.epdf_plugin_actions.ActionPolicy
public typealias ActionPolicyRow = uniffi.epdf_plugin_actions.ActionPolicyRow
public typealias ActionPolicyPatch = uniffi.epdf_plugin_actions.ActionPolicyPatch
public typealias PartialActionPolicyRow = uniffi.epdf_plugin_actions.PartialActionPolicyRow
public typealias ActionPolicyDecision = uniffi.epdf_plugin_actions.ActionPolicyDecision
public typealias TriggerGates = uniffi.epdf_plugin_actions.TriggerGates
public typealias OpenSequence = uniffi.epdf_plugin_actions.OpenSequence
public typealias PdfActionTree = uniffi.epdf_plugin_actions.PdfActionTree
public typealias PdfActionNode = uniffi.epdf_plugin_actions.PdfActionNode
public typealias PdfObjectRef = uniffi.epdf_plugin_actions.PdfObjectRef
public typealias ActionOrigin = uniffi.epdf_plugin_actions.ActionOrigin
public typealias ActionSource = uniffi.epdf_plugin_actions.ActionSource
public typealias ActionSourceWidget = uniffi.epdf_plugin_actions.ActionSource.Widget
public typealias ActionSourceLink = uniffi.epdf_plugin_actions.ActionSource.Link
public typealias ActionSourceOutline = uniffi.epdf_plugin_actions.ActionSource.Outline
public typealias ActionSourceAnnotation = uniffi.epdf_plugin_actions.ActionSource.Annotation
public typealias ActionSourcePage = uniffi.epdf_plugin_actions.ActionSource.Page
public typealias ActionSourceDocument = uniffi.epdf_plugin_actions.ActionSource.Document
public typealias ActionSourceApi = uniffi.epdf_plugin_actions.ActionSource.Api
public typealias ActionContext = uniffi.epdf_plugin_actions.ActionContext
public typealias PartialActionContext = uniffi.epdf_plugin_actions.PartialActionContext
public typealias ActionTriggerEvent = uniffi.epdf_plugin_actions.ActionTriggerEvent
public typealias ActionTriggerEventActivate = uniffi.epdf_plugin_actions.ActionTriggerEvent.Activate
public typealias ActionTriggerEventAnnotation = uniffi.epdf_plugin_actions.ActionTriggerEvent.Annotation
public typealias ActionTriggerEventPage = uniffi.epdf_plugin_actions.ActionTriggerEvent.Page
public typealias ActionTriggerEventDocument = uniffi.epdf_plugin_actions.ActionTriggerEvent.Document
public typealias ActionTrigger = uniffi.epdf_plugin_actions.ActionTrigger
public typealias ActionTriggerActivate = uniffi.epdf_plugin_actions.ActionTrigger.Activate
public typealias ActionTriggerAnnotation = uniffi.epdf_plugin_actions.ActionTrigger.Annotation
public typealias ActionTriggerPage = uniffi.epdf_plugin_actions.ActionTrigger.Page
public typealias ActionTriggerDocument = uniffi.epdf_plugin_actions.ActionTrigger.Document
public typealias ActionTreeSource = uniffi.epdf_plugin_actions.ActionTreeSource
public typealias ActionTreeSourceAnnotation = uniffi.epdf_plugin_actions.ActionTreeSource.Annotation
public typealias ActionTreeSourceField = uniffi.epdf_plugin_actions.ActionTreeSource.Field
public typealias ActionTreeSourcePage = uniffi.epdf_plugin_actions.ActionTreeSource.Page
public typealias ActionTreeSourceDocument = uniffi.epdf_plugin_actions.ActionTreeSource.Document
public typealias ActionTreeSourceOutline = uniffi.epdf_plugin_actions.ActionTreeSource.Outline
public typealias ActionAnnotationTreeEvent = uniffi.epdf_plugin_actions.ActionAnnotationTreeEvent
public typealias ActionAnnotationTreeEventActivate = uniffi.epdf_plugin_actions.ActionAnnotationTreeEvent.Activate
public typealias ActionAnnotationTreeEventEvent = uniffi.epdf_plugin_actions.ActionAnnotationTreeEvent.Event
public typealias DocumentTriggerEvent = uniffi.epdf_plugin_actions.DocumentTriggerEvent
public typealias PdfAnnotationEventKind = uniffi.epdf_plugin_actions.PdfAnnotationEventKind
public typealias PageEventKind = uniffi.epdf_plugin_actions.PageEventKind
public typealias PdfFieldEventKind = uniffi.epdf_plugin_actions.PdfFieldEventKind
public typealias ActionNodeStatus = uniffi.epdf_plugin_actions.ActionNodeStatus
public typealias ActionNodeResult = uniffi.epdf_plugin_actions.ActionNodeResult
public typealias ActionResultStatus = uniffi.epdf_plugin_actions.ActionResultStatus
public typealias ActionDispatchResult = uniffi.epdf_plugin_actions.ActionDispatchResult
public typealias ActionStepResult = uniffi.epdf_plugin_actions.ActionStepResult
public typealias ActionTriggerResult = uniffi.epdf_plugin_actions.ActionTriggerResult
public typealias ActionDiagnostic = uniffi.epdf_plugin_actions.ActionDiagnostic
public typealias ActionDiagnosticCode = uniffi.epdf_plugin_actions.ActionDiagnosticCode
public typealias OpenSequenceCompleted = uniffi.epdf_plugin_actions.OpenSequenceCompleted
public typealias ScriptDiagnostic = uniffi.epdf_plugin_actions.ScriptDiagnostic
public typealias ScriptDiagnosticCode = uniffi.epdf_plugin_actions.ScriptDiagnosticCode
public typealias ScriptExecutionError = uniffi.epdf_plugin_actions.ScriptExecutionError
public typealias ScriptErrorKind = uniffi.epdf_plugin_actions.ScriptErrorKind
public typealias ActionsEvent = uniffi.epdf_plugin_actions.ActionsEvent
public typealias ActionsEventExecuted = uniffi.epdf_plugin_actions.ActionsEvent.Executed
public typealias ActionsEventDiagnosticReported = uniffi.epdf_plugin_actions.ActionsEvent.DiagnosticReported
public typealias ActionsEventScriptDiagnosticReported = uniffi.epdf_plugin_actions.ActionsEvent.ScriptDiagnosticReported
public typealias ActionsEventScriptFailed = uniffi.epdf_plugin_actions.ActionsEvent.ScriptFailed
public typealias ActionsEventOpenSequenceCompleted = uniffi.epdf_plugin_actions.ActionsEvent.OpenSequenceCompleted
public typealias ActionExecutedEvent = uniffi.epdf_plugin_actions.ActionExecutedEvent
public typealias ActionDiagnosticReported = uniffi.epdf_plugin_actions.ActionDiagnosticReported
public typealias ScriptFailed = uniffi.epdf_plugin_actions.ScriptFailed

/**
 * Runs one node type. `execute` is called on the viewer's kernel thread:
 * answer through the completion, from any thread, without blocking on the
 * viewer.
 */
public typealias ActionExecutor = uniffi.epdf_plugin_actions.ActionExecutor
public typealias ActionExecutorCompletion = uniffi.epdf_plugin_actions.ActionExecutorCompletion

/**
 * The host's UI for actions: open a URI, print, alert, go to a page. Called
 * on the viewer's kernel thread; an implementation that touches a `View`
 * posts to the main thread itself.
 */
public typealias ActionUiHandler = uniffi.epdf_plugin_actions.ActionUiHandler
public typealias ActionUiSupport = uniffi.epdf_plugin_actions.ActionUiSupport
public typealias ActionUiContext = uniffi.epdf_plugin_actions.ActionUiContext
public typealias ActionUriOptions = uniffi.epdf_plugin_actions.ActionUriOptions
public typealias ActionAlertOptions = uniffi.epdf_plugin_actions.ActionAlertOptions
public typealias ScriptPhase = uniffi.epdf_plugin_actions.ScriptPhase

/** Sends a form submission. Called on the viewer's kernel thread. */
public typealias ActionSubmitHandler = uniffi.epdf_plugin_actions.ActionSubmitHandler
public typealias ActionSubmitRequest = uniffi.epdf_plugin_actions.ActionSubmitRequest
public typealias FormSubmissionEntry = uniffi.epdf_plugin_actions.FormSubmissionEntry
public typealias FormSubmissionValue = uniffi.epdf_plugin_actions.FormSubmissionValue
public typealias FormSubmissionValueText = uniffi.epdf_plugin_actions.FormSubmissionValue.Text
public typealias FormSubmissionValueValues = uniffi.epdf_plugin_actions.FormSubmissionValue.Values
public typealias FormSubmissionValueNameOnly = uniffi.epdf_plugin_actions.FormSubmissionValue.NameOnly
public typealias DocumentVerb = uniffi.epdf_plugin_actions.DocumentVerb

/**
 * A tree in the engine's shape: `{root, incomplete, warningFlags, warnings}`.
 * Throws `invalid-input` for JSON that isn't one.
 */
public fun uniffi.epdf_plugin_actions.PdfActionTree.Companion.decode(json: String): PdfActionTree =
    try {
        PdfActionTree.fromJson(json)
    } catch (e: Exception) {
        throw PdfException.from(e)
    }

/**
 * The config with `utcOffsetMinutes` set from the default time zone's offset
 * now. The offset is fixed for the viewer's lifetime.
 */
public fun uniffi.epdf_plugin_actions.ActionsConfig.Companion.forCurrentTimeZone(
    policy: ActionPolicyPatch? = null,
    triggers: TriggerGates? = null,
    openSequence: OpenSequence? = null,
    javascript: ActionsJavaScriptConfig? = null,
): ActionsConfig = ActionsConfig(
    policy = policy,
    triggers = triggers,
    openSequence = openSequence,
    javascript = javascript,
    utcOffsetMinutes = TimeZone.getDefault().getOffset(System.currentTimeMillis()) / 60_000,
)

/** Fails the verb with [error], keeping its code (see [HostFailure]). */
public fun DocumentVerbCompletion.fail(error: Throwable) {
    failWith(HostFailure(error))
}

/**
 * Runs [body] in a coroutine of [context] each time the kernel asks, and
 * answers the kernel when it ends.
 */
private class CoroutineDocumentVerb(
    private val context: CoroutineContext,
    private val body: suspend () -> Unit,
) : DocumentVerbOperation {
    override fun run(completion: DocumentVerbCompletion) {
        CoroutineScope(context).launch {
            try {
                body()
                completion.finish()
            } catch (e: CancellationException) {
                completion.fail(e)
                throw e
            } catch (e: Throwable) {
                completion.fail(e)
            }
        }
    }
}

/**
 * A document's PDF actions: policy, the dispatcher and its ports. Get one
 * with [PdfViewer.actions]; the viewer needs `ViewerConfig(actions =
 * ActionsConfig())`. It keeps the viewer alive. After its document closes,
 * every call throws `instance-closed`. No call is cancellable: a dispatch is
 * a transaction on the document's queue.
 */
public class ActionsCapability internal constructor(
    private val handle: ActionsHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): ActionsSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: ActionsSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun policy(): ActionPolicy = viewer.call { handle.policy() }

    public suspend fun updatePolicy(patch: ActionPolicyPatch) {
        viewer.call { handle.updatePolicy(patch) }
    }

    /** Whether the document's scripts run: JavaScript enabled with an engine that loaded. */
    public suspend fun isScriptingEnabled(): Boolean = viewer.call { handle.isScriptingEnabled() }

    public suspend fun execute(tree: PdfActionTree, context: ActionContext): ActionDispatchResult =
        viewer.call { handle.execute(tree, context) }

    public suspend fun canExecute(tree: PdfActionTree, context: ActionContext): Boolean =
        viewer.call { handle.canExecute(tree, context) }

    public suspend fun executeNamed(name: String, context: PartialActionContext? = null): ActionDispatchResult =
        viewer.call { handle.executeNamed(name, context) }

    public suspend fun canExecuteNamed(name: String, context: PartialActionContext? = null): Boolean =
        viewer.call { handle.canExecuteNamed(name, context) }

    /** The tree [source] holds, or `null` when it holds none. */
    public suspend fun actionTree(source: ActionTreeSource): PdfActionTree? = viewer.call { handle.actionTree(source) }

    public suspend fun dispatch(trigger: ActionTrigger): ActionTriggerResult = viewer.call { handle.dispatch(trigger) }

    public suspend fun canDispatch(trigger: ActionTrigger): Boolean = viewer.call { handle.canDispatch(trigger) }

    /** Runs the document's will-close trees. Call before [PdfViewer.close]. */
    public suspend fun prepareClose(): ActionTriggerResult = viewer.call { handle.prepareClose() }

    /** Last wins per node type. */
    public suspend fun registerExecutor(actionType: String, executor: ActionExecutor): Registration =
        Registration(viewer.call { handle.registerExecutor(actionType, executor) })

    /** `null` uninstalls. Installing one releases an `auto` open sequence. */
    public suspend fun setUiHandler(handler: ActionUiHandler?, support: ActionUiSupport = ActionUiSupport()): Registration =
        Registration(viewer.call { handle.setUiAdapter(handler, support) })

    /** `null` uninstalls. */
    public suspend fun setSubmitHandler(handler: ActionSubmitHandler?): Registration =
        Registration(viewer.call { handle.setSubmitHandler(handler) })

    /**
     * The document's will- trees, then [operation], then its did- trees.
     * [operation] runs in a new coroutine on the caller's dispatcher; what it
     * throws fails the verb with its code (a [PdfException] keeps its own)
     * and is thrown here.
     */
    public suspend fun runDocumentVerb(verb: DocumentVerb, operation: suspend () -> Unit) {
        val context = currentCoroutineContext().minusKey(Job)
        viewer.call { handle.runDocumentVerb(verb, CoroutineDocumentVerb(context, operation)) }
    }

    /**
     * Executed nodes, diagnostics, script failures and the open sequence's
     * end. Completes when the document closes (at once if it already has) or
     * the viewer is destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<ActionsEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : ActionsEventListener {
                override fun onEvent(event: ActionsEvent) {
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
 * The actions capability for [document]. Throws `unsupported` when the viewer
 * was built without the actions plugin, whatever the document's state. The
 * first call sends the script engine's held reports, such as a JavaScript
 * sandbox that isn't there (see [ScriptEngineAvailability]).
 */
public suspend fun PdfViewer.actions(document: DocumentInfo): ActionsCapability {
    reportUnavailableJavaScript()
    return ActionsCapability(call { host.actions(document.id) }, this)
}
