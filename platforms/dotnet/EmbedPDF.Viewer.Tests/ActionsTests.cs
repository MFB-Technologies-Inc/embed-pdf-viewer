using Xunit;

namespace EmbedPDF.Viewer.Tests;

sealed class RecordingUI(Exception? throwsOnOpen = null) : IActionUiHandler
{
    readonly Lock gate = new();
    readonly List<string> opened = [];
    readonly List<string> alerts = [];

    public IReadOnlyList<string> Opened { get { lock (gate) return [.. opened]; } }

    public IReadOnlyList<string> Alerts { get { lock (gate) return [.. alerts]; } }

    public void OpenUri(string uri, ActionUriOptions options)
    {
        if (throwsOnOpen is not null) throw throwsOnOpen;
        lock (gate) opened.Add(uri);
    }

    public void Print(ActionUiContext? context) { }

    public void Alert(string message, ActionAlertOptions options) { lock (gate) alerts.Add(message); }

    public void GotoPage(double page, ActionUiContext context) { }
}

sealed class RecordingExecutor(Func<PdfActionNode, Task<ActionExecutorResult>>? answer = null) : IActionExecutor
{
    readonly Lock gate = new();
    readonly List<string> types = [];

    public IReadOnlyList<string> Types { get { lock (gate) return [.. types]; } }

    public void Record(string type) { lock (gate) types.Add(type); }

    public Task<ActionExecutorResult> ExecuteAsync(PdfActionNode node, ActionContext context)
    {
        Record(node.NodeType);
        return answer?.Invoke(node) ?? Task.FromResult<ActionExecutorResult>(new ActionExecutorResult.Executed());
    }
}

public sealed class ActionsTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, ActionsCapability Actions)> Opened(PdfViewerOptions? options = null)
    {
        var viewer = await PdfViewer.CreateAsync(options ?? new PdfViewerOptions { Actions = new() { OpenSequence = OpenSequence.Off } });
        var document = await viewer.OpenAsync(Fixture.Data("document_actions"));
        return (viewer, document, await viewer.GetActionsAsync(document));
    }

    internal static readonly ActionContext User = new(ActionOrigin.User, new ActionSource.Api(), new ActionTriggerEvent.Activate());

    internal static PdfActionTree UriTree(string uri) => PdfActionTree.FromJson(
        $$"""{"root":{"type":"uri","subtype":"URI","uri":"{{uri}}","isMap":false,"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""");

    static PdfActionTree GotoTree() => PdfActionTree.FromJson(
        """{"root":{"type":"goto","subtype":"GoTo","destination":{"kind":"fit","page":{"kind":"objectNumber","pageObjectNumber":1}},"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""");

    [Fact]
    public async Task TheCatalogsOpenTreeIsJavaScript()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        using var open = await actions.GetActionTreeAsync(new ActionTreeSource.Document(null));
        Assert.Equal("javascript", open?.RootType);
        Assert.False(await actions.IsScriptingEnabledAsync());
    }

    [Fact]
    public async Task AUserUriReachesTheHandlerAndAFileUriDoesNot()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var ui = new RecordingUI();
        var registration = await actions.SetUiHandlerAsync(ui);
        await actions.ExecuteAsync(UriTree("https://a.test/"), User);
        var blocked = await actions.ExecuteAsync(UriTree("file:///etc/passwd"), User);
        Assert.Equal([ActionNodeStatus.Blocked], blocked.Nodes.Select(n => n.Status));
        Assert.Equal(["https://a.test/"], ui.Opened);
        registration.Unregister();
    }

    [Fact]
    public async Task AThrowingUiHandlerFailsTheDispatchAndTheViewerKeepsWorking()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        await actions.SetUiHandlerAsync(new RecordingUI(new InvalidOperationException("no browser")));
        // The UI handler's throw propagates into the dispatch, as the engine documents.
        var error = await Assert.ThrowsAsync<PdfException>(() => actions.ExecuteAsync(UriTree("https://a.test/"), User));
        Assert.Equal(("HostError", "no browser"), (error.Name, error.Detail));
        Assert.True(await actions.CanExecuteAsync(UriTree("https://a.test/"), User));
        await actions.SetUiHandlerAsync(new RecordingUI());
        var result = await actions.ExecuteAsync(UriTree("https://a.test/"), User);
        Assert.Equal([ActionNodeStatus.Executed], result.Nodes.Select(n => n.Status));
    }

    sealed class ThrowingSubmit : IActionSubmitHandler
    {
        public int Calls;
        public void Submit(ActionSubmitRequest request)
        {
            Calls++;
            throw new InvalidOperationException("offline");
        }
    }

    static PdfActionTree SubmitTree() => PdfActionTree.FromJson(
        """{"root":{"type":"submit-form","subtype":"SubmitForm","payload":{"url":"https://home.test/submit","fields":null,"exclude":false,"includeNoValueFields":false,"format":"html","method":"post","submitCoordinates":false,"includeAppendSaves":false,"includeAnnotations":false,"canonicalFormat":false,"exclNonUserAnnots":false,"exclFKey":false,"embedForm":false},"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""");

    [Fact]
    public async Task AThrowingSubmitHandlerFailsTheNodeAndTheViewerKeepsWorking()
    {
        var (viewer, _, actions) = await Opened(new PdfViewerOptions
        {
            Interaction = true, Form = new(), Actions = new() { OpenSequence = OpenSequence.Off },
        });
        await using var _v = viewer;
        var submit = new ThrowingSubmit();
        await actions.SetSubmitHandlerAsync(submit);
        var result = await actions.ExecuteAsync(SubmitTree(), User);
        Assert.Equal(1, submit.Calls);
        var node = Assert.Single(result.Nodes);
        Assert.Equal(ActionNodeStatus.Failed, node.Status);
        Assert.Equal("offline", node.Detail);
        await actions.GetPolicyAsync();
    }

    [Fact]
    public async Task AnExecutorRunsItsNodes()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var executor = new RecordingExecutor();
        await actions.RegisterExecutorAsync("goto", executor);
        var result = await actions.ExecuteAsync(GotoTree(), User);
        Assert.Equal([ActionNodeStatus.Executed], result.Nodes.Select(n => n.Status));
        Assert.Equal(["goto"], executor.Types);
    }

    [Fact]
    public async Task AnExecutorCanAnswerInert()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        await actions.RegisterExecutorAsync("goto", new RecordingExecutor(_ => Task.FromResult<ActionExecutorResult>(new ActionExecutorResult.Inert("later"))));
        var result = await actions.ExecuteAsync(GotoTree(), User);
        Assert.Equal([ActionNodeStatus.Inert], result.Nodes.Select(n => n.Status));
    }

    [Fact]
    public async Task AnExecutorThatFaultsFailsItsNode()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        await actions.RegisterExecutorAsync("goto", new RecordingExecutor(async _ => { await Task.Yield(); throw new IOException("no page"); }));
        var result = await actions.ExecuteAsync(GotoTree(), User);
        Assert.Equal([ActionNodeStatus.Failed], result.Nodes.Select(n => n.Status));
        Assert.Contains("no page", result.Nodes[0].Detail);
    }

    [Fact]
    public async Task PrepareCloseFindsTheWillCloseScript()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var result = await actions.PrepareCloseAsync();
        var step = Assert.Single(result.Steps);
        Assert.Equal([ActionNodeStatus.Inert], step.Result.Nodes.Select(n => n.Status));
    }

    [Fact]
    public async Task ADocumentVerbRunsItsOperation()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var ran = new RecordingExecutor();
        await actions.RunDocumentVerbAsync(DocumentVerb.Save, async () =>
        {
            await Task.Delay(5);
            ran.Record("save");
        });
        Assert.Equal(["save"], ran.Types);
    }

    [Fact]
    public async Task ADocumentVerbsThrownPdfExceptionKeepsItsCode()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => actions.RunDocumentVerbAsync(DocumentVerb.Save,
            () => throw new PdfException("PluginError", PdfErrorCode.PermissionDenied, "no doc.download")));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        Assert.Equal("no doc.download", error.Detail);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, actions) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => actions.GetPolicyAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        using (var cancel = new CancellationTokenSource())
        {
            var reading = Task.Run(async () => { await foreach (var _ in actions.ObserveEvents(cancel.Token)) { } });
            await Task.Delay(20);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        }
        await actions.PrepareCloseAsync();
    }

    [Fact]
    public async Task ExecutionIsObserved()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        await actions.SetUiHandlerAsync(new RecordingUI());
        var events = actions.ObserveEvents();
        await actions.ExecuteAsync(UriTree("https://a.test/"), User);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is not ActionsEvent.Executed executed) continue;
            Assert.Equal("uri", executed.Tree.RootType);
            Assert.IsType<ActionSource.Api>(executed.Source);
            Assert.Equal(ActionResultStatus.Executed, executed.Result.Status);
            break;
        }
    }

    [Fact]
    public async Task PolicyAndSettingsRoundTrip()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var policy = await actions.GetPolicyAsync();
        await actions.UpdatePolicyAsync(new ActionPolicyPatch { Uri = new PartialActionPolicyRow { User = ActionPolicyDecision.Block } });
        Assert.Equal(ActionPolicyDecision.Block, (await actions.GetPolicyAsync()).Uri.User);
        var blocked = await actions.ExecuteAsync(UriTree("https://a.test/"), User);
        Assert.Equal([ActionNodeStatus.Blocked], blocked.Nodes.Select(n => n.Status));
        var settings = await actions.GetSettingsAsync();
        Assert.Equal(OpenSequence.Off, settings.OpenSequence);
        Assert.False(settings.JavaScript.Enabled);
        await actions.UpdateSettingsAsync(new ActionsSettingsPatch { Triggers = new TriggerGates { Page = false } });
        Assert.False((await actions.GetSettingsAsync()).Triggers.Page);
        await actions.ResetSettingsAsync();
        Assert.Equal(policy, await actions.GetPolicyAsync());
    }

    [Fact]
    public async Task NamedActionsAndTriggers()
    {
        var (viewer, document, actions) = await Opened();
        await using var _v = viewer;
        Assert.IsType<bool>(await actions.CanExecuteNamedAsync("NextPage"));
        var named = await actions.ExecuteNamedAsync("NextPage", new PartialActionContext { Origin = ActionOrigin.User });
        Assert.NotEmpty(named.Nodes);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var trigger = new ActionTrigger.Page(PageEventKind.Open, page);
        Assert.IsType<bool>(await actions.CanDispatchAsync(trigger));
        var dispatched = await actions.DispatchAsync(trigger);
        Assert.True(Enum.IsDefined(dispatched.Status));
    }

    [Fact]
    public async Task ASubmitHandlerIsInstalledAndRemoved()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        using var registration = await actions.SetSubmitHandlerAsync(new RecordingSubmit());
        using var cleared = await actions.SetSubmitHandlerAsync(null);
        using var ui = await actions.SetUiHandlerAsync(null, new ActionUiSupport(Alert: false));
    }

    sealed class RecordingSubmit : IActionSubmitHandler
    {
        public void Submit(ActionSubmitRequest request) { }
    }

    sealed class DisposingUI(PdfViewer viewer) : IActionUiHandler
    {
        public Exception? Thrown;
        public void OpenUri(string uri, ActionUriOptions options)
        {
            try { viewer.Dispose(); }
            catch (Exception error) { Thrown = error; }
        }
        public void Print(ActionUiContext? context) { }
        public void Alert(string message, ActionAlertOptions options) { }
        public void GotoPage(double page, ActionUiContext context) { }
    }

    [Fact]
    public async Task DisposingTheViewerFromTheUiHandlerThrowsInvalidOperation()
    {
        var (viewer, _, actions) = await Opened();
        await using var _v = viewer;
        var ui = new DisposingUI(viewer);
        await actions.SetUiHandlerAsync(ui);
        await actions.ExecuteAsync(UriTree("https://a.test/"), User);
        Assert.IsType<InvalidOperationException>(ui.Thrown);
        await actions.GetPolicyAsync();
    }

    static string Ref(int number) => $$"""{"objectNumber":{{number}},"generation":0}""";

    /// <summary>Compares through the records' text, references by the JSON the engine gives back.</summary>
    static string Describe(object value) => value.ToString()!;

    [Fact]
    public void EveryActionSourceTreeSourceAndTriggerRoundTrips()
    {
        var annotation = new PdfObjectRef(Ref(8));
        var back = Ffi.ToPublic(Ffi.ToFfi(new ActionSource.Outline(annotation)));
        // The engine may respell a reference; compare everything else through its spelling.
        var spelled = ((ActionSource.Outline)back).Item;
        ActionSource[] sources =
        [
            new ActionSource.Widget(spelled, spelled, new PageRef(3)),
            new ActionSource.Link(null, null),
            new ActionSource.Link(spelled, new PageRef(3)),
            new ActionSource.Outline(spelled),
            new ActionSource.Annotation(spelled, new PageRef(3)),
            new ActionSource.Page(new PageRef(3)),
            new ActionSource.Document(),
            new ActionSource.Api(),
        ];
        foreach (var source in sources) Assert.Equal(Describe(source), Describe(Ffi.ToPublic(Ffi.ToFfi(source))));
        ActionTreeSource[] treeSources =
        [
            new ActionTreeSource.Annotation(spelled, new PageRef(3), null),
            new ActionTreeSource.Annotation(spelled, new PageRef(3), new ActionAnnotationTreeEvent.Activate()),
            new ActionTreeSource.Annotation(spelled, new PageRef(3), new ActionAnnotationTreeEvent.Event(PdfAnnotationEventKind.Blur)),
            new ActionTreeSource.Field(spelled, PdfFieldEventKind.Calculate),
            new ActionTreeSource.Page(new PageRef(3), PageEventKind.Invisible),
            new ActionTreeSource.Page(new PageRef(3), null),
            new ActionTreeSource.Document(DocumentTriggerEvent.WillClose),
            new ActionTreeSource.Document(null),
            new ActionTreeSource.Outline(spelled),
        ];
        foreach (var source in treeSources) Assert.Equal(Describe(source), Describe(Ffi.ToPublic(Ffi.ToFfi(source))));
        ActionTrigger[] triggers =
        [
            new ActionTrigger.Activate(spelled, new PageRef(3), null),
            new ActionTrigger.Activate(spelled, new PageRef(3), new ActionSource.Api()),
            new ActionTrigger.Annotation(PdfAnnotationEventKind.MouseUp, spelled, new PageRef(3), new ActionSource.Document()),
            new ActionTrigger.Page(PageEventKind.Visible, new PageRef(3)),
            new ActionTrigger.Document(DocumentTriggerEvent.DidPrint),
        ];
        foreach (var trigger in triggers) Assert.Equal(Describe(trigger), Describe(Ffi.ToPublic(Ffi.ToFfi(trigger))));
        ActionTriggerEvent[] events =
        [
            new ActionTriggerEvent.Activate(), new ActionTriggerEvent.Annotation(PdfAnnotationEventKind.Focus),
            new ActionTriggerEvent.Page(PageEventKind.Close), new ActionTriggerEvent.Document(DocumentTriggerEvent.Open),
        ];
        foreach (var e in events) Assert.Equal(e, Ffi.ToPublic(Ffi.ToFfi(e)));
    }

    [Fact]
    public void AMalformedObjectRefIsInvalidInput()
    {
        var error = Assert.Throws<PdfException>(() => Ffi.ToFfi(new ActionSource.Outline(new PdfObjectRef("{"))));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => PdfActionTree.FromJson("{")).Code);
    }

    [Fact]
    public void ForCurrentTimeZoneSetsTheOffset()
    {
        var config = ActionsConfig.ForCurrentTimeZone();
        Assert.Equal((int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes, config.UtcOffsetMinutes);
    }
}
