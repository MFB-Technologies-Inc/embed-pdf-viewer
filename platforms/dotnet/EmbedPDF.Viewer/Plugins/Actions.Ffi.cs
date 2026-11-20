using GA = uniffi.epdf_plugin_actions;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static PdfObjectRef ToPublic(GA.PdfObjectRef value) => new(value.Json());

    internal static PdfObjectRef? ToPublicOrNull(GA.PdfObjectRef? value) => value is null ? null : ToPublic(value);

    /// <summary>A reference the engine can read; a malformed one is <c>invalid-input</c>.</summary>
    internal static GA.PdfObjectRef ToFfi(PdfObjectRef value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NativeRuntime.EnsureLoaded();
        try
        {
            return GA.PdfObjectRef.FromJson(value.Json ?? throw new ArgumentException("The reference has no JSON.", nameof(value)));
        }
        catch (Exception error) when (error is not ArgumentException)
        {
            throw PdfException.From(error);
        }
    }

    static GA.PdfObjectRef? ToFfiOrNull(PdfObjectRef? value) => value is null ? null : ToFfi(value);

    internal static ActionSource ToPublic(GA.ActionSource source) => source switch
    {
        GA.ActionSource.Widget w => new ActionSource.Widget(ToPublic(w.Field), ToPublic(w.AnnotationRef), ToPublic(w.PageRef)),
        GA.ActionSource.Link l => new ActionSource.Link(ToPublicOrNull(l.AnnotationRef), ToPublicOrNull(l.PageRef)),
        GA.ActionSource.Outline o => new ActionSource.Outline(ToPublic(o.Item)),
        GA.ActionSource.Annotation a => new ActionSource.Annotation(ToPublic(a.AnnotationValue), ToPublic(a.PageRef)),
        GA.ActionSource.Page p => new ActionSource.Page(ToPublic(p.PageValue)),
        GA.ActionSource.Document => new ActionSource.Document(),
        GA.ActionSource.Api => new ActionSource.Api(),
        _ => throw Unknown(source),
    };

    static ActionSource? ToPublicOrNull(GA.ActionSource? source) => source is null ? null : ToPublic(source);

    internal static GA.ActionSource ToFfi(ActionSource source) => source switch
    {
        ActionSource.Widget w => new GA.ActionSource.Widget(ToFfi(w.Field), ToFfi(w.AnnotationRef), ToFfi(w.PageRef)),
        ActionSource.Link l => new GA.ActionSource.Link(ToFfiOrNull(l.AnnotationRef), ToFfiOrNull(l.PageRef)),
        ActionSource.Outline o => new GA.ActionSource.Outline(ToFfi(o.Item)),
        ActionSource.Annotation a => new GA.ActionSource.Annotation(ToFfi(a.AnnotationRef), ToFfi(a.PageRef)),
        ActionSource.Page p => new GA.ActionSource.Page(ToFfi(p.PageRef)),
        ActionSource.Document => new GA.ActionSource.Document(),
        ActionSource.Api => new GA.ActionSource.Api(),
        _ => throw new ArgumentException("An action source is null.", nameof(source)),
    };

    static GA.ActionSource? ToFfiOrNull(ActionSource? source) => source is null ? null : ToFfi(source);

    internal static GA.ActionTreeSource ToFfi(ActionTreeSource source) => source switch
    {
        ActionTreeSource.Annotation a => new GA.ActionTreeSource.Annotation(ToFfi(a.AnnotationRef), ToFfi(a.PageRef), a.Event switch
        {
            null => null,
            ActionAnnotationTreeEvent.Activate => new GA.ActionAnnotationTreeEvent.Activate(),
            ActionAnnotationTreeEvent.Event e => new GA.ActionAnnotationTreeEvent.Event((GA.PdfAnnotationEventKind)(int)Defined(e.Kind, "annotation event")),
            _ => throw new ArgumentException("Unknown annotation tree event.", nameof(source)),
        }),
        ActionTreeSource.Field f => new GA.ActionTreeSource.Field(ToFfi(f.FieldRef), (GA.PdfFieldEventKind)(int)Defined(f.Event, "field event")),
        ActionTreeSource.Page p => new GA.ActionTreeSource.Page(ToFfi(p.PageRef), p.Event is { } e ? (GA.PageEventKind)(int)Defined(e, "page event") : null),
        ActionTreeSource.Document d => new GA.ActionTreeSource.Document(d.Event is { } e ? (GA.DocumentTriggerEvent)(int)Defined(e, "document event") : null),
        ActionTreeSource.Outline o => new GA.ActionTreeSource.Outline(ToFfi(o.Item)),
        _ => throw new ArgumentException("An action tree source is null.", nameof(source)),
    };

    internal static ActionTreeSource ToPublic(GA.ActionTreeSource source) => source switch
    {
        GA.ActionTreeSource.Annotation a => new ActionTreeSource.Annotation(ToPublic(a.AnnotationValue), ToPublic(a.PageRef), a.Event switch
        {
            null => null,
            GA.ActionAnnotationTreeEvent.Activate => new ActionAnnotationTreeEvent.Activate(),
            GA.ActionAnnotationTreeEvent.Event e => new ActionAnnotationTreeEvent.Event((PdfAnnotationEventKind)(int)e.Kind),
            _ => throw Unknown(a.Event),
        }),
        GA.ActionTreeSource.Field f => new ActionTreeSource.Field(ToPublic(f.FieldValue), (PdfFieldEventKind)(int)f.Event),
        GA.ActionTreeSource.Page p => new ActionTreeSource.Page(ToPublic(p.PageValue), p.Event is { } e ? (PageEventKind)(int)e : null),
        GA.ActionTreeSource.Document d => new ActionTreeSource.Document(d.Event is { } e ? (DocumentTriggerEvent)(int)e : null),
        GA.ActionTreeSource.Outline o => new ActionTreeSource.Outline(ToPublic(o.Item)),
        _ => throw Unknown(source),
    };

    internal static GA.ActionTrigger ToFfi(ActionTrigger trigger) => trigger switch
    {
        ActionTrigger.Activate a => new GA.ActionTrigger.Activate(ToFfi(a.AnnotationRef), ToFfi(a.PageRef), ToFfiOrNull(a.Source)),
        ActionTrigger.Annotation a => new GA.ActionTrigger.Annotation((GA.PdfAnnotationEventKind)(int)Defined(a.Event, "annotation event"), ToFfi(a.AnnotationRef), ToFfi(a.PageRef), ToFfiOrNull(a.Source)),
        ActionTrigger.Page p => new GA.ActionTrigger.Page((GA.PageEventKind)(int)Defined(p.Event, "page event"), ToFfi(p.PageRef)),
        ActionTrigger.Document d => new GA.ActionTrigger.Document((GA.DocumentTriggerEvent)(int)Defined(d.Event, "document event")),
        _ => throw new ArgumentException("An action trigger is null.", nameof(trigger)),
    };

    internal static ActionTrigger ToPublic(GA.ActionTrigger trigger) => trigger switch
    {
        GA.ActionTrigger.Activate a => new ActionTrigger.Activate(ToPublic(a.AnnotationRef), ToPublic(a.PageRef), ToPublicOrNull(a.Source)),
        GA.ActionTrigger.Annotation a => new ActionTrigger.Annotation((PdfAnnotationEventKind)(int)a.Event, ToPublic(a.AnnotationValue), ToPublic(a.PageRef), ToPublicOrNull(a.Source)),
        GA.ActionTrigger.Page p => new ActionTrigger.Page((PageEventKind)(int)p.Event, ToPublic(p.PageValue)),
        GA.ActionTrigger.Document d => new ActionTrigger.Document((DocumentTriggerEvent)(int)d.Event),
        _ => throw Unknown(trigger),
    };

    internal static GA.ActionTriggerEvent ToFfi(ActionTriggerEvent e) => e switch
    {
        ActionTriggerEvent.Activate => new GA.ActionTriggerEvent.Activate(),
        ActionTriggerEvent.Annotation a => new GA.ActionTriggerEvent.Annotation((GA.PdfAnnotationEventKind)(int)Defined(a.Event, "annotation event")),
        ActionTriggerEvent.Page p => new GA.ActionTriggerEvent.Page((GA.PageEventKind)(int)Defined(p.Event, "page event")),
        ActionTriggerEvent.Document d => new GA.ActionTriggerEvent.Document((GA.DocumentTriggerEvent)(int)Defined(d.Event, "document event")),
        _ => throw new ArgumentException("An action trigger event is null.", nameof(e)),
    };

    internal static ActionTriggerEvent ToPublic(GA.ActionTriggerEvent e) => e switch
    {
        GA.ActionTriggerEvent.Activate => new ActionTriggerEvent.Activate(),
        GA.ActionTriggerEvent.Annotation a => new ActionTriggerEvent.Annotation((PdfAnnotationEventKind)(int)a.Event),
        GA.ActionTriggerEvent.Page p => new ActionTriggerEvent.Page((PageEventKind)(int)p.Event),
        GA.ActionTriggerEvent.Document d => new ActionTriggerEvent.Document((DocumentTriggerEvent)(int)d.Event),
        _ => throw Unknown(e),
    };

    internal static GA.ActionContext ToFfi(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new((GA.ActionOrigin)(int)Defined(context.Origin, "action origin"), ToFfi(context.Source), ToFfi(context.Event));
    }

    internal static ActionContext ToPublic(GA.ActionContext context) =>
        new((ActionOrigin)(int)context.Origin, ToPublic(context.Source), ToPublic(context.Event));

    internal static GA.PartialActionContext? ToFfi(PartialActionContext? context) => context is null ? null : new(
        Origin: context.Origin is { } o ? (GA.ActionOrigin)(int)Defined(o, "action origin") : null,
        Source: ToFfiOrNull(context.Source),
        Event: context.Event is null ? null : ToFfi(context.Event));

    static ActionPolicyRow ToPublic(GA.ActionPolicyRow row) =>
        new((ActionPolicyDecision)(int)row.User, (ActionPolicyDecision)(int)row.Hover, (ActionPolicyDecision)(int)row.Lifecycle);

    internal static ActionPolicy ToPublic(GA.ActionPolicy p) => new(
        ToPublic(p.Goto), ToPublic(p.Named), ToPublic(p.Hide), ToPublic(p.ResetForm),
        ToPublic(p.Javascript), ToPublic(p.Uri), ToPublic(p.Print), ToPublic(p.SubmitForm));

    static GA.PartialActionPolicyRow? ToFfi(PartialActionPolicyRow? row) => row is null ? null : new(
        User: row.User is { } u ? (GA.ActionPolicyDecision)(int)Defined(u, "policy decision") : null,
        Hover: row.Hover is { } h ? (GA.ActionPolicyDecision)(int)Defined(h, "policy decision") : null,
        Lifecycle: row.Lifecycle is { } l ? (GA.ActionPolicyDecision)(int)Defined(l, "policy decision") : null);

    internal static GA.ActionPolicyPatch? ToFfi(ActionPolicyPatch? p) => p is null ? null : new(
        Goto: ToFfi(p.Goto), Named: ToFfi(p.Named), Hide: ToFfi(p.Hide), ResetForm: ToFfi(p.ResetForm),
        Javascript: ToFfi(p.JavaScript), Uri: ToFfi(p.Uri), Print: ToFfi(p.Print), SubmitForm: ToFfi(p.SubmitForm));

    internal static GA.TriggerGates? ToFfi(TriggerGates? gates) => gates is null ? null : new(gates.Document, gates.Page, gates.Annotation);

    static ActionsScriptIdentity ToPublic(GA.ActionsScriptIdentity identity) => new()
    {
        Name = identity.Name, LoginName = identity.LoginName, Corporation = identity.Corporation, Email = identity.Email,
    };

    static GA.ActionsScriptIdentity? ToFfi(ActionsScriptIdentity? identity) => identity is null ? null : new(
        identity.Name, identity.LoginName, identity.Corporation, identity.Email);

    internal static ActionsSettings ToPublic(GA.ActionsSettings s) => new(
        ToPublic(s.Policy),
        new ActionTriggers(s.Triggers.Document, s.Triggers.Page, s.Triggers.Annotation),
        (OpenSequence)(int)s.OpenSequence,
        new ActionsJavaScriptSettings(s.Javascript.Enabled, s.Javascript.Identity switch
        {
            null => null,
            GA.ActionsScriptIdentitySetting.Value v => new ActionsScriptIdentitySetting.Value(ToPublic(v.Identity)),
            GA.ActionsScriptIdentitySetting.RegisteredProvider => new ActionsScriptIdentitySetting.RegisteredProvider(),
            _ => throw Unknown(s.Javascript.Identity),
        }));

    internal static GA.ActionsSettingsPatch ToFfi(ActionsSettingsPatch p) => new(
        Policy: ToFfi(p.Policy),
        Triggers: ToFfi(p.Triggers),
        OpenSequence: p.OpenSequence is { } o ? ToFfi(o) : null,
        Javascript: p.JavaScript is null ? null : new GA.ActionsJavaScriptSettingsPatch(p.JavaScript.Enabled, ToFfi(p.JavaScript.Identity), p.JavaScript.ClearIdentity));

    internal static GA.OpenSequence ToFfi(OpenSequence sequence) => (GA.OpenSequence)(int)Defined(sequence, "open sequence");

    internal static GA.DocumentVerb ToFfi(DocumentVerb verb) => (GA.DocumentVerb)(int)Defined(verb, "document verb");

    internal static GA.ActionsScriptBudget ToFfi(ActionsScriptBudget b) => new(
        WireInteger.ToUInt32(b.MaxExecutionMs, "maxExecutionMs"),
        WireInteger.ToUInt64(b.MaxMemoryBytes, "maxMemoryBytes"),
        WireInteger.ToUInt64(b.MaxStackBytes, "maxStackBytes"),
        WireInteger.ToUInt32(b.MaxEffects, "maxEffects"),
        WireInteger.ToUInt64(b.MaxOutputBytes, "maxOutputBytes"));

    internal static ActionDispatchResult ToPublic(GA.ActionDispatchResult r) => new(
        (ActionResultStatus)(int)r.Status,
        [.. r.Nodes.Select(n => new ActionNodeResult([.. n.Path.Select(i => WireInteger.ToInt(i, "path"))], n.NodeType, (ActionNodeStatus)(int)n.Status, n.Detail))],
        [.. r.Diagnostics.Select(ToPublic)]);

    static ActionDiagnostic ToPublic(GA.ActionDiagnostic d) => new((ActionDiagnosticCode)(int)d.Code, d.Message);

    internal static ActionTriggerResult ToPublic(GA.ActionTriggerResult r) => new(
        (ActionResultStatus)(int)r.Status,
        [.. r.Steps.Select(s => new ActionStepResult(ToPublic(s.Source), new PdfActionTree(s.Tree), ToPublic(s.Result)))],
        [.. r.Diagnostics.Select(ToPublic)]);

    internal static ScriptExecutionError ToPublic(GA.ScriptExecutionError e) => new((ScriptErrorKind)(int)e.Kind, e.Message, e.Stack);

    internal static ActionsEvent ToPublic(GA.ActionsEvent e) => e switch
    {
        GA.ActionsEvent.Executed x => new ActionsEvent.Executed(new PdfActionTree(x.Event.Tree), ToPublic(x.Event.Result), ToPublic(x.Event.Source)),
        GA.ActionsEvent.DiagnosticReported d => new ActionsEvent.DiagnosticReported((ActionDiagnosticCode)(int)d.Event.Code, d.Event.Action, ToPublicOrNull(d.Event.Source)),
        GA.ActionsEvent.ScriptDiagnosticReported s => new ActionsEvent.ScriptDiagnosticReported((ScriptDiagnosticCode)(int)s.Event.Code, s.Event.Message),
        GA.ActionsEvent.ScriptFailed f => new ActionsEvent.ScriptFailed(ToPublic(f.Event.Error), ToPublicOrNull(f.Event.Source)),
        GA.ActionsEvent.OpenSequenceCompleted o => new ActionsEvent.OpenSequenceCompleted(ToPublic(o.Event.Result)),
        _ => throw Unknown(e),
    };

    internal static ActionSubmitRequest ToPublic(GA.ActionSubmitRequest r) => new(
        r.Url, r.Method, r.Format, r.CharSet,
        [.. r.Entries.Select(entry => new FormSubmissionEntry(entry.Name, entry.Value switch
        {
            GA.FormSubmissionValue.Text t => new FormSubmissionValue.Text(t.Value),
            GA.FormSubmissionValue.List l => new FormSubmissionValue.List(l.Values),
            GA.FormSubmissionValue.NameOnly => new FormSubmissionValue.NameOnly(),
            _ => throw Unknown(entry.Value),
        }))],
        (ActionOrigin)(int)r.Origin, ToPublic(r.Event));
}
