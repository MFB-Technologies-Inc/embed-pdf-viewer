namespace EmbedPDF;

/// <summary>Who started an action.</summary>
public enum ActionOrigin
{
    /// <summary>The user activated something.</summary>
    User,
    /// <summary>The pointer hovered something.</summary>
    Hover,
    /// <summary>The document's lifecycle (open, close, save, print).</summary>
    Lifecycle,
}

/// <summary>What the policy does with an action type for one origin.</summary>
public enum ActionPolicyDecision
{
    /// <summary>Run it.</summary>
    Allow,
    /// <summary>Hand it to the UI handler.</summary>
    Adapter,
    /// <summary>Report it without running it.</summary>
    Report,
    /// <summary>Refuse it.</summary>
    Block,
}

/// <summary>How a dispatch as a whole went.</summary>
public enum ActionResultStatus
{
    /// <summary>Every node ran.</summary>
    Executed,
    /// <summary>Some nodes ran.</summary>
    Partial,
    /// <summary>Nothing had an effect.</summary>
    Inert,
    /// <summary>The policy or a gate refused it.</summary>
    Refused,
    /// <summary>It was cancelled.</summary>
    Cancelled,
}

/// <summary>How one node of a tree went.</summary>
public enum ActionNodeStatus
{
    /// <summary>It ran.</summary>
    Executed,
    /// <summary>The policy blocked it.</summary>
    Blocked,
    /// <summary>No executor handles its type.</summary>
    NoExecutor,
    /// <summary>It ran with no effect.</summary>
    Inert,
    /// <summary>It failed; the result's detail says why.</summary>
    Failed,
    /// <summary>It was skipped.</summary>
    Skipped,
}

/// <summary>A diagnostic the dispatcher reports.</summary>
public enum ActionDiagnosticCode
{
    /// <summary>The tree is incomplete.</summary>
    IncompleteTree,
    /// <summary>A node was blocked.</summary>
    Blocked,
    /// <summary>No executor for a node type.</summary>
    NoExecutor,
    /// <summary>No UI handler for a node that needs one.</summary>
    NoAdapter,
    /// <summary>No session sink.</summary>
    NoSessionSink,
    /// <summary>A target did not resolve.</summary>
    UnresolvedTarget,
    /// <summary>An executor was registered twice.</summary>
    DuplicateExecutor,
    /// <summary>An executor answered inert.</summary>
    ExecutorInert,
    /// <summary>An executor failed.</summary>
    ExecutorFailed,
    /// <summary>The trigger is disabled.</summary>
    TriggerDisabled,
    /// <summary>No commit sink.</summary>
    NoCommitSink,
    /// <summary>A trigger failed.</summary>
    TriggerFailed,
    /// <summary>A cascade ran past its budget.</summary>
    CascadeBudget,
    /// <summary>The open sequence was replayed.</summary>
    OpenSequenceReplayed,
    /// <summary>No submit handler.</summary>
    NoSubmitSink,
    /// <summary>No submit resolver.</summary>
    NoSubmitResolver,
    /// <summary>The submit payload was unavailable.</summary>
    SubmitPayloadUnavailable,
    /// <summary>A submit entry is unsupported.</summary>
    SubmitEntryUnsupported,
    /// <summary>A print started inside a print.</summary>
    ReentrantPrint,
}

/// <summary>A document-level trigger.</summary>
public enum DocumentTriggerEvent
{
    /// <summary>The document opened.</summary>
    Open,
    /// <summary>Before a save.</summary>
    WillSave,
    /// <summary>After a save.</summary>
    DidSave,
    /// <summary>Before a print.</summary>
    WillPrint,
    /// <summary>After a print.</summary>
    DidPrint,
    /// <summary>Before the document closes.</summary>
    WillClose,
}

/// <summary>An operation the document's will- and did- trees surround.</summary>
public enum DocumentVerb
{
    /// <summary>A save.</summary>
    Save,
    /// <summary>A print.</summary>
    Print,
}

/// <summary>What happens to the document's open actions.</summary>
public enum OpenSequence
{
    /// <summary>They run once a UI handler is installed.</summary>
    Auto,
    /// <summary>They run without a UI.</summary>
    Headless,
    /// <summary>They never run.</summary>
    Off,
}

/// <summary>A page-level trigger.</summary>
public enum PageEventKind
{
    /// <summary>The page opened.</summary>
    Open,
    /// <summary>The page closed.</summary>
    Close,
    /// <summary>The page became visible.</summary>
    Visible,
    /// <summary>The page stopped being visible.</summary>
    Invisible,
}

/// <summary>An annotation-level trigger.</summary>
public enum PdfAnnotationEventKind
{
    /// <summary>The cursor entered.</summary>
    CursorEnter,
    /// <summary>The cursor left.</summary>
    CursorExit,
    /// <summary>A button went down.</summary>
    MouseDown,
    /// <summary>A button went up.</summary>
    MouseUp,
    /// <summary>It gained focus.</summary>
    Focus,
    /// <summary>It lost focus.</summary>
    Blur,
}

/// <summary>A form-field trigger.</summary>
public enum PdfFieldEventKind
{
    /// <summary>A keystroke.</summary>
    Keystroke,
    /// <summary>Formatting the value.</summary>
    Format,
    /// <summary>Validating the value.</summary>
    Validate,
    /// <summary>Calculating the value.</summary>
    Calculate,
}

/// <summary>A diagnostic a script reports.</summary>
public enum ScriptDiagnosticCode
{
    /// <summary>The script tried the network.</summary>
    BlockedNetwork,
    /// <summary>The script used an unsupported API.</summary>
    UnsupportedApi,
    /// <summary>The script set a field to an invalid value.</summary>
    InvalidFieldValue,
    /// <summary>The script threw.</summary>
    ScriptError,
    /// <summary>A UI effect was suppressed.</summary>
    UiEffectSuppressed,
}

/// <summary>Why a script failed.</summary>
public enum ScriptErrorKind
{
    /// <summary>It threw.</summary>
    Exception,
    /// <summary>It ran past its time, memory, stack or output budget.</summary>
    Budget,
    /// <summary>It produced output the engine could not read.</summary>
    InvalidOutput,
}

/// <summary>Whether a script effect came from document boot or a user action.</summary>
public enum ScriptPhase
{
    /// <summary>Document boot.</summary>
    Boot,
    /// <summary>A user action.</summary>
    User,
}

/// <summary>A PDF object reference in the engine's JSON shape, such as an annotation or a form field.</summary>
/// <param name="Json">The reference as JSON.</param>
public sealed record PdfObjectRef(string Json);

/// <summary>One node of an action tree, as an <see cref="IActionExecutor"/> receives it.</summary>
/// <param name="NodeType">The node's <c>type</c> (<c>goto</c>, <c>uri</c>, ...).</param>
/// <param name="Json">The node as JSON.</param>
public sealed record PdfActionNode(string NodeType, string Json);

/// <summary>The policy for one action type, per origin.</summary>
/// <param name="User">For <see cref="ActionOrigin.User"/>.</param>
/// <param name="Hover">For <see cref="ActionOrigin.Hover"/>.</param>
/// <param name="Lifecycle">For <see cref="ActionOrigin.Lifecycle"/>.</param>
public sealed record ActionPolicyRow(ActionPolicyDecision User, ActionPolicyDecision Hover, ActionPolicyDecision Lifecycle);

/// <summary>The action policy, one row per action type.</summary>
public sealed record ActionPolicy(
    ActionPolicyRow Goto,
    ActionPolicyRow Named,
    ActionPolicyRow Hide,
    ActionPolicyRow ResetForm,
    ActionPolicyRow JavaScript,
    ActionPolicyRow Uri,
    ActionPolicyRow Print,
    ActionPolicyRow SubmitForm);

/// <summary>A change to one <see cref="ActionPolicyRow"/>; <see langword="null"/> keeps a value.</summary>
public sealed record PartialActionPolicyRow
{
    /// <summary>For <see cref="ActionOrigin.User"/>.</summary>
    public ActionPolicyDecision? User { get; init; }

    /// <summary>For <see cref="ActionOrigin.Hover"/>.</summary>
    public ActionPolicyDecision? Hover { get; init; }

    /// <summary>For <see cref="ActionOrigin.Lifecycle"/>.</summary>
    public ActionPolicyDecision? Lifecycle { get; init; }
}

/// <summary>A change to <see cref="ActionPolicy"/>; <see langword="null"/> keeps a row.</summary>
public sealed record ActionPolicyPatch
{
    /// <summary>Go-to actions.</summary>
    public PartialActionPolicyRow? Goto { get; init; }

    /// <summary>Named actions.</summary>
    public PartialActionPolicyRow? Named { get; init; }

    /// <summary>Hide actions.</summary>
    public PartialActionPolicyRow? Hide { get; init; }

    /// <summary>Reset-form actions.</summary>
    public PartialActionPolicyRow? ResetForm { get; init; }

    /// <summary>JavaScript actions.</summary>
    public PartialActionPolicyRow? JavaScript { get; init; }

    /// <summary>URI actions.</summary>
    public PartialActionPolicyRow? Uri { get; init; }

    /// <summary>Print actions.</summary>
    public PartialActionPolicyRow? Print { get; init; }

    /// <summary>Submit-form actions.</summary>
    public PartialActionPolicyRow? SubmitForm { get; init; }
}

/// <summary>Which trigger families may run; <see langword="null"/> keeps a value.</summary>
public sealed record TriggerGates
{
    /// <summary>Document triggers.</summary>
    public bool? Document { get; init; }

    /// <summary>Page triggers.</summary>
    public bool? Page { get; init; }

    /// <summary>Annotation triggers.</summary>
    public bool? Annotation { get; init; }
}

/// <summary>Which trigger families may run.</summary>
/// <param name="Document">Document triggers.</param>
/// <param name="Page">Page triggers.</param>
/// <param name="Annotation">Annotation triggers.</param>
public sealed record ActionTriggers(bool Document, bool Page, bool Annotation);

/// <summary>The identity scripts see as <c>identity</c>.</summary>
public sealed record ActionsScriptIdentity
{
    /// <summary>The user's name.</summary>
    public string? Name { get; init; }

    /// <summary>The login name.</summary>
    public string? LoginName { get; init; }

    /// <summary>The organization.</summary>
    public string? Corporation { get; init; }

    /// <summary>The email address.</summary>
    public string? Email { get; init; }
}

/// <summary>Where the script identity comes from.</summary>
public abstract record ActionsScriptIdentitySetting
{
    private protected ActionsScriptIdentitySetting() { }

    /// <summary>A fixed identity.</summary>
    /// <param name="Identity">The identity.</param>
    public sealed record Value(ActionsScriptIdentity Identity) : ActionsScriptIdentitySetting;

    /// <summary>A provider registered with the engine.</summary>
    public sealed record RegisteredProvider : ActionsScriptIdentitySetting;
}

/// <summary>JavaScript settings.</summary>
/// <param name="Enabled">Whether document JavaScript runs.</param>
/// <param name="Identity">Where the script identity comes from.</param>
public sealed record ActionsJavaScriptSettings(bool Enabled, ActionsScriptIdentitySetting? Identity);

/// <summary>A change to <see cref="ActionsJavaScriptSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record ActionsJavaScriptSettingsPatch
{
    /// <summary>New <see cref="ActionsJavaScriptSettings.Enabled"/>.</summary>
    public bool? Enabled { get; init; }

    /// <summary>A fixed identity.</summary>
    public ActionsScriptIdentity? Identity { get; init; }

    /// <summary>Clears the identity.</summary>
    public bool ClearIdentity { get; init; }
}

/// <summary>Actions settings.</summary>
/// <param name="Policy">The policy.</param>
/// <param name="Triggers">The trigger gates.</param>
/// <param name="OpenSequence">What happens to open actions.</param>
/// <param name="JavaScript">JavaScript settings.</param>
public sealed record ActionsSettings(ActionPolicy Policy, ActionTriggers Triggers, OpenSequence OpenSequence, ActionsJavaScriptSettings JavaScript);

/// <summary>A change to <see cref="ActionsSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record ActionsSettingsPatch
{
    /// <summary>Policy rows to change.</summary>
    public ActionPolicyPatch? Policy { get; init; }

    /// <summary>Trigger gates to change.</summary>
    public TriggerGates? Triggers { get; init; }

    /// <summary>New <see cref="ActionsSettings.OpenSequence"/>.</summary>
    public OpenSequence? OpenSequence { get; init; }

    /// <summary>JavaScript settings to change.</summary>
    public ActionsJavaScriptSettingsPatch? JavaScript { get; init; }
}

/// <summary>A script's limits.</summary>
/// <param name="MaxExecutionMs">Time per run, in milliseconds.</param>
/// <param name="MaxMemoryBytes">Memory per run.</param>
/// <param name="MaxStackBytes">Stack per run.</param>
/// <param name="MaxEffects">Effects per run.</param>
/// <param name="MaxOutputBytes">Output per run.</param>
public sealed record ActionsScriptBudget(int MaxExecutionMs, long MaxMemoryBytes, long MaxStackBytes, int MaxEffects, long MaxOutputBytes);

/// <summary>What started an action.</summary>
public abstract record ActionSource
{
    private protected ActionSource() { }

    /// <summary>A form widget.</summary>
    /// <param name="Field">The field.</param>
    /// <param name="AnnotationRef">The widget annotation.</param>
    /// <param name="PageRef">Its page.</param>
    public sealed record Widget(PdfObjectRef Field, PdfObjectRef AnnotationRef, PageRef PageRef) : ActionSource;

    /// <summary>A link.</summary>
    /// <param name="AnnotationRef">The link annotation, if any.</param>
    /// <param name="PageRef">Its page, if known.</param>
    public sealed record Link(PdfObjectRef? AnnotationRef, PageRef? PageRef) : ActionSource;

    /// <summary>An outline item.</summary>
    /// <param name="Item">The item.</param>
    public sealed record Outline(PdfObjectRef Item) : ActionSource;

    /// <summary>An annotation.</summary>
    /// <param name="AnnotationRef">The annotation.</param>
    /// <param name="PageRef">Its page.</param>
    public sealed record Annotation(PdfObjectRef AnnotationRef, PageRef PageRef) : ActionSource;

    /// <summary>A page.</summary>
    /// <param name="PageRef">The page.</param>
    public sealed record Page(PageRef PageRef) : ActionSource;

    /// <summary>The document.</summary>
    public sealed record Document : ActionSource;

    /// <summary>An API call.</summary>
    public sealed record Api : ActionSource;
}

/// <summary>Which of an annotation's trees to read.</summary>
public abstract record ActionAnnotationTreeEvent
{
    private protected ActionAnnotationTreeEvent() { }

    /// <summary>Its activation tree.</summary>
    public sealed record Activate : ActionAnnotationTreeEvent;

    /// <summary>The tree for one trigger.</summary>
    /// <param name="Kind">The trigger.</param>
    public sealed record Event(PdfAnnotationEventKind Kind) : ActionAnnotationTreeEvent;
}

/// <summary>Where <see cref="ActionsCapability.GetActionTreeAsync"/> reads a tree from.</summary>
public abstract record ActionTreeSource
{
    private protected ActionTreeSource() { }

    /// <summary>An annotation's tree.</summary>
    /// <param name="AnnotationRef">The annotation.</param>
    /// <param name="PageRef">Its page.</param>
    /// <param name="Event">Which tree; <see langword="null"/> is its activation tree.</param>
    public sealed record Annotation(PdfObjectRef AnnotationRef, PageRef PageRef, ActionAnnotationTreeEvent? Event) : ActionTreeSource;

    /// <summary>A form field's tree for one trigger.</summary>
    /// <param name="FieldRef">The field.</param>
    /// <param name="Event">The trigger.</param>
    public sealed record Field(PdfObjectRef FieldRef, PdfFieldEventKind Event) : ActionTreeSource;

    /// <summary>A page's tree.</summary>
    /// <param name="PageRef">The page.</param>
    /// <param name="Event">The trigger; <see langword="null"/> is the default.</param>
    public sealed record Page(PageRef PageRef, PageEventKind? Event) : ActionTreeSource;

    /// <summary>The document's tree.</summary>
    /// <param name="Event">The trigger; <see langword="null"/> is the catalog's open action.</param>
    public sealed record Document(DocumentTriggerEvent? Event) : ActionTreeSource;

    /// <summary>An outline item's tree.</summary>
    /// <param name="Item">The item.</param>
    public sealed record Outline(PdfObjectRef Item) : ActionTreeSource;
}

/// <summary>The trigger an action ran for.</summary>
public abstract record ActionTriggerEvent
{
    private protected ActionTriggerEvent() { }

    /// <summary>An activation.</summary>
    public sealed record Activate : ActionTriggerEvent;

    /// <summary>An annotation trigger.</summary>
    /// <param name="Event">The trigger.</param>
    public sealed record Annotation(PdfAnnotationEventKind Event) : ActionTriggerEvent;

    /// <summary>A page trigger.</summary>
    /// <param name="Event">The trigger.</param>
    public sealed record Page(PageEventKind Event) : ActionTriggerEvent;

    /// <summary>A document trigger.</summary>
    /// <param name="Event">The trigger.</param>
    public sealed record Document(DocumentTriggerEvent Event) : ActionTriggerEvent;
}

/// <summary>A trigger to dispatch with <see cref="ActionsCapability.DispatchAsync"/>.</summary>
public abstract record ActionTrigger
{
    private protected ActionTrigger() { }

    /// <summary>Activates an annotation.</summary>
    /// <param name="AnnotationRef">The annotation.</param>
    /// <param name="PageRef">Its page.</param>
    /// <param name="Source">What activated it; <see langword="null"/> derives it.</param>
    public sealed record Activate(PdfObjectRef AnnotationRef, PageRef PageRef, ActionSource? Source) : ActionTrigger;

    /// <summary>An annotation trigger.</summary>
    /// <param name="Event">The trigger.</param>
    /// <param name="AnnotationRef">The annotation.</param>
    /// <param name="PageRef">Its page.</param>
    /// <param name="Source">What started it; <see langword="null"/> derives it.</param>
    public sealed record Annotation(PdfAnnotationEventKind Event, PdfObjectRef AnnotationRef, PageRef PageRef, ActionSource? Source) : ActionTrigger;

    /// <summary>A page trigger.</summary>
    /// <param name="Event">The trigger.</param>
    /// <param name="PageRef">The page.</param>
    public sealed record Page(PageEventKind Event, PageRef PageRef) : ActionTrigger;

    /// <summary>A document trigger.</summary>
    /// <param name="Event">The trigger.</param>
    public sealed record Document(DocumentTriggerEvent Event) : ActionTrigger;
}

/// <summary>The context an action tree runs in.</summary>
/// <param name="Origin">Who started it.</param>
/// <param name="Source">What started it.</param>
/// <param name="Event">The trigger.</param>
public sealed record ActionContext(ActionOrigin Origin, ActionSource Source, ActionTriggerEvent Event);

/// <summary>A context for a named action; <see langword="null"/> fields take the engine's defaults.</summary>
public sealed record PartialActionContext
{
    /// <summary>Who started it.</summary>
    public ActionOrigin? Origin { get; init; }

    /// <summary>What started it.</summary>
    public ActionSource? Source { get; init; }

    /// <summary>The trigger.</summary>
    public ActionTriggerEvent? Event { get; init; }
}

/// <summary>How one node went.</summary>
/// <param name="Path">The node's child indexes from the root.</param>
/// <param name="NodeType">Its type.</param>
/// <param name="Status">How it went.</param>
/// <param name="Detail">Why, for a failure or a block.</param>
public sealed record ActionNodeResult(IReadOnlyList<int> Path, string NodeType, ActionNodeStatus Status, string? Detail);

/// <summary>A dispatch diagnostic.</summary>
/// <param name="Code">What happened.</param>
/// <param name="Message">A description.</param>
public sealed record ActionDiagnostic(ActionDiagnosticCode Code, string Message);

/// <summary>How one tree's run went.</summary>
/// <param name="Status">Overall.</param>
/// <param name="Nodes">Each node, in run order.</param>
/// <param name="Diagnostics">What the dispatcher reported.</param>
public sealed record ActionDispatchResult(ActionResultStatus Status, IReadOnlyList<ActionNodeResult> Nodes, IReadOnlyList<ActionDiagnostic> Diagnostics);

/// <summary>One tree a trigger ran.</summary>
/// <param name="Source">What started it.</param>
/// <param name="Tree">The tree.</param>
/// <param name="Result">How it went.</param>
public sealed record ActionStepResult(ActionSource Source, PdfActionTree Tree, ActionDispatchResult Result);

/// <summary>How a trigger's trees went.</summary>
/// <param name="Status">Overall.</param>
/// <param name="Steps">Each tree, in run order.</param>
/// <param name="Diagnostics">What the dispatcher reported.</param>
public sealed record ActionTriggerResult(ActionResultStatus Status, IReadOnlyList<ActionStepResult> Steps, IReadOnlyList<ActionDiagnostic> Diagnostics);

/// <summary>A script failure.</summary>
/// <param name="Kind">Why.</param>
/// <param name="Message">A description.</param>
/// <param name="Stack">The script stack, if any.</param>
public sealed record ScriptExecutionError(ScriptErrorKind Kind, string Message, string? Stack);

/// <summary>An event from <see cref="ActionsCapability.ObserveEvents"/>.</summary>
public abstract record ActionsEvent
{
    private protected ActionsEvent() { }

    /// <summary>A tree ran.</summary>
    /// <param name="Tree">The tree.</param>
    /// <param name="Result">How it went.</param>
    /// <param name="Source">What started it.</param>
    public sealed record Executed(PdfActionTree Tree, ActionDispatchResult Result, ActionSource Source) : ActionsEvent;

    /// <summary>The dispatcher reported a diagnostic.</summary>
    /// <param name="Code">What happened.</param>
    /// <param name="Action">The action type, if any.</param>
    /// <param name="Source">What started it, if known.</param>
    public sealed record DiagnosticReported(ActionDiagnosticCode Code, string? Action, ActionSource? Source) : ActionsEvent;

    /// <summary>A script reported a diagnostic.</summary>
    /// <param name="Code">What happened.</param>
    /// <param name="Message">A description.</param>
    public sealed record ScriptDiagnosticReported(ScriptDiagnosticCode Code, string Message) : ActionsEvent;

    /// <summary>A script failed.</summary>
    /// <param name="Error">How.</param>
    /// <param name="Source">What started it, if known.</param>
    public sealed record ScriptFailed(ScriptExecutionError Error, ActionSource? Source) : ActionsEvent;

    /// <summary>The document's open sequence finished.</summary>
    /// <param name="Result">How its trees went.</param>
    public sealed record OpenSequenceCompleted(ActionTriggerResult Result) : ActionsEvent;
}

/// <summary>How a URI action reaches the UI handler.</summary>
/// <param name="IsMap">Whether it is an image-map URI.</param>
/// <param name="Origin">Who started it.</param>
public sealed record ActionUriOptions(bool IsMap, ActionOrigin Origin);

/// <summary>How a script alert reaches the UI handler.</summary>
/// <param name="Origin">Who started it.</param>
/// <param name="Phase">Boot or user.</param>
/// <param name="Icon">The script's icon number.</param>
/// <param name="Title">The title, if any.</param>
public sealed record ActionAlertOptions(ActionOrigin Origin, ScriptPhase Phase, double Icon, string? Title);

/// <summary>The context of a print or page request.</summary>
/// <param name="Origin">Who started it.</param>
/// <param name="Phase">Boot or user.</param>
public sealed record ActionUiContext(ActionOrigin Origin, ScriptPhase Phase);

/// <summary>Which optional effects the UI handler supports.</summary>
/// <param name="Alert">Script alerts.</param>
/// <param name="GotoPage">Script page changes.</param>
public sealed record ActionUiSupport(bool Alert = true, bool GotoPage = true);

/// <summary>A form value to submit.</summary>
public abstract record FormSubmissionValue
{
    private protected FormSubmissionValue() { }

    /// <summary>Text.</summary>
    /// <param name="Value">The text.</param>
    public sealed record Text(string Value) : FormSubmissionValue;

    /// <summary>Several values.</summary>
    /// <param name="Values">The values.</param>
    public sealed record List(IReadOnlyList<string> Values) : FormSubmissionValue;

    /// <summary>The name with no value.</summary>
    public sealed record NameOnly : FormSubmissionValue;
}

/// <summary>One submitted field.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Value">Its value.</param>
public sealed record FormSubmissionEntry(string Name, FormSubmissionValue Value);

/// <summary>A form submission the document asked for.</summary>
/// <param name="Url">Where to, if the document says.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Format">The payload format.</param>
/// <param name="CharSet">The character set, if any.</param>
/// <param name="Entries">The fields.</param>
/// <param name="Origin">Who started it.</param>
/// <param name="Event">The trigger.</param>
public sealed record ActionSubmitRequest(string? Url, string Method, string Format, string? CharSet, IReadOnlyList<FormSubmissionEntry> Entries, ActionOrigin Origin, ActionTriggerEvent Event);

/// <summary>What an <see cref="IActionExecutor"/> did with a node.</summary>
public abstract record ActionExecutorResult
{
    private protected ActionExecutorResult() { }

    /// <summary>It ran the node.</summary>
    public sealed record Executed : ActionExecutorResult;

    /// <summary>It ran with no effect.</summary>
    /// <param name="Reason">Why.</param>
    public sealed record Inert(string Reason) : ActionExecutorResult;
}
