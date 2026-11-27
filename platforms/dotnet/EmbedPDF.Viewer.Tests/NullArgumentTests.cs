using System.Reflection;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Every public member that takes a non-nullable reference throws
/// <see cref="ArgumentNullException"/> for null before doing anything else.
/// </summary>
public sealed class NullArgumentTests
{
    static readonly NullabilityInfoContext Nullability = new();

    static StageViewState? viewState;

    static object? Valid(Type type) => type switch
    {
        _ when type == typeof(string) => "x",
        _ when type == typeof(DocumentInfo) => new DocumentInfo("x", null, 0, DocumentStatus.Ready, null),
        _ when type == typeof(Stream) => new MemoryStream(),
        _ when type == typeof(Uri) => new Uri("https://embedpdf.test/x.pdf"),
        _ when type == typeof(IEnumerable<string>) => Array.Empty<string>(),
        _ when type == typeof(IEnumerable<StandardCommandList>) => Array.Empty<StandardCommandList>(),
        _ when type == typeof(CancellationToken) => CancellationToken.None,
        _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) => Array.CreateInstance(type.GetGenericArguments()[0], 0),
        _ when type == typeof(IReadOnlyDictionary<string, string?>) => new Dictionary<string, string?>(),
        _ when type == typeof(SearchQuery) => new SearchQuery("x"),
        _ when type == typeof(AttachedFile) => new AttachedFile("x", new AttachmentHome.Document(new AttachmentRef("x")), "x", null, null, null, null, null, null),
        _ when type == typeof(PageEditPlacement) => new PageEditPlacement.End(),
        _ when type == typeof(OutlinePosition) => new OutlinePosition.End(),
        _ when type == typeof(RenderPageViewDemand) => new RenderPageViewDemand(80),
        _ when type == typeof(ShellSnapshot) => new ShellSnapshot([], []),
        _ when type == typeof(StyleProps) => new StyleProps("a", 1),
        _ when type == typeof(System.Text.Json.Serialization.Metadata.JsonTypeInfo<StyleProps>) => ShellJson.Default.StyleProps,
        _ when type == typeof(PointerSample) => new PointerSample(PointerPhase.Down, new PdfPoint(0, 0), new PointerModifiers()),
        _ when type == typeof(InteractionTool) => new InteractionTool("x", "default", []),
        _ when type == typeof(IInteractionHandler) => new RecordingHandler(),
        _ when type == typeof(Func<bool>) => (Func<bool>)(() => true),
        _ when type == typeof(LocalePack) => new LocalePack("x", "x", new Dictionary<string, string>()),
        _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) =>
            Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments())),
        _ when type == typeof(System.Reflection.Assembly) => typeof(NullArgumentTests).Assembly,
        _ when type == typeof(PdfActionTree) => ActionsTests.UriTree("https://a.test/"),
        _ when type == typeof(ActionContext) => ActionsTests.User,
        _ when type == typeof(ActionTreeSource) => new ActionTreeSource.Document(null),
        _ when type == typeof(ActionTrigger) => new ActionTrigger.Document(DocumentTriggerEvent.Open),
        _ when type == typeof(IActionExecutor) => new RecordingExecutor(),
        _ when type == typeof(Func<Task>) => (Func<Task>)(() => Task.CompletedTask),
        _ when type == typeof(PageDestination) => new PageDestination.Fit(new PageRef(1)),
        _ when type == typeof(StageZoomTarget) => new StageZoomTarget.Level(1),
        _ when type == typeof(StageViewState) => viewState ?? throw new InvalidOperationException("no view state yet"),
        _ when type == typeof(IStageInitialViewProvider) => new SavedView(null),
        _ when type == typeof(SelectionInput) => new SelectionInput.Span(new PageRef(1), 0, 1),
        _ when type == typeof(SelectionTextRange) => new SelectionTextRange(new SelectionTextPosition(new PageRef(1), 0), new SelectionTextPosition(new PageRef(1), 1)),
        _ when type == typeof(SearchHit) => new SearchHit(new PageRef(1), 0, 0, 1, [], null, null),
        _ when type == typeof(LinkItem) => new LinkItem("x", new PageRef(1), default, "{}", false, null, false, null),
        _ when type == typeof(CommandDefinition) => new CommandDefinition("x:y", "x"),
        _ when type == typeof(CommandKeyStroke) => new CommandKeyStroke("x"),
        _ when type == typeof(ToolId) => ToolId.Pointer,
        _ when type == typeof(AnnotationRef) => new AnnotationRef.ObjectNumber(new PageRef(1), 1),
        _ when type == typeof(AnnotationDraft) => new SquareAnnotationDraft(new PdfRect(0, 0, 1, 1)),
        _ when type == typeof(AnnotationPatch) => new SquareAnnotationPatch(),
        _ when type == typeof(AnnotationPosition) => new AnnotationPosition.End(),
        _ when type == typeof(AnnotationLinkTarget) => new AnnotationLinkTarget.Uri("https://a.test/"),
        _ when type == typeof(AnnotationStampSource) => new AnnotationStampSource(new byte[] { 1 }),
        _ when type == typeof(AnnotationStampPlacement) => new AnnotationStampPlacement(new PageRef(1), default),
        _ when type == typeof(AnnotationAttachmentFile) => new AnnotationAttachmentFile(new byte[] { 1 }, "x"),
        _ when type == typeof(AnnotationTextEndAnchor) => new AnnotationTextEndAnchor("{}"),
        _ when type == typeof(FormFieldRef) => new FormFieldRef.Fqn("x"),
        _ when type == typeof(FormValueInput) => new FormValueInput.Value("x"),
        _ when type == typeof(FormCalculationPosition) => new FormCalculationPosition.End(),
        _ when type == typeof(FormCreateFieldInput) => new FormCreateFieldInput(FormFieldFamily.Text, "x", new PageRef(1), default),
        _ when type == typeof(IFormFocusTarget) => new NoFocus(),
        _ when type == typeof(MeasurementPageTarget) => new MeasurementPageTarget.All(),
        _ when type == typeof(PdfMeasurement) => new PdfMeasurement("RL"),
        _ when type == typeof(CreateMeasurementInput) => new CreateMeasurementInput(MeasurementKind.Distance, new PageRef(1), []),
        _ when type == typeof(MeasurementCalibrateInput) => new MeasurementCalibrateInput(new PageRef(1), default, default, 1, "m"),
        _ when type == typeof(AnnotationInfo) => new AnnotationInfo(AnnotationInfoTests.Record()),
        _ when type == typeof(IStampLibraryStore) => new FileStampLibraryStore(Path.Combine(Path.GetTempPath(), "embedpdf-null-args")),
        _ when type == typeof(SignatureSignInput) => new SignatureSignInput(new FormFieldRef.Fqn("x")),
        _ when type == typeof(SignaturePrepareInput) => new SignaturePrepareInput(new FormFieldRef.Fqn("x")),
        _ when type == typeof(SignatureMark) => new SignatureMark.Asset("x"),
        _ when type == typeof(SignaturePlaceTarget) => new SignaturePlaceTarget.Field(new FormFieldRef.Fqn("x")),
        _ when type == typeof(ISignatureSigner) => TestKeys.Signer(),
        _ when type == typeof(SignatureTransportRequest) => new SignatureTransportRequest("not a url", "x", [], new byte[] { 1 }, 1, 1),
        _ when type.IsValueType => Activator.CreateInstance(type),
        _ when type.GetConstructor(Type.EmptyTypes) is { } constructor => constructor.Invoke(null),
        _ when type == typeof(IDocumentFetcher) => new HttpClientDocumentFetcher(),
        _ when type == typeof(Microsoft.Extensions.Logging.ILogger) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
        _ => throw new InvalidOperationException($"no valid value for {type}"),
    };

    /// <summary>Each (method, parameter index) whose parameter is a non-nullable reference.</summary>
    static IEnumerable<(MethodInfo Method, int Index)> Cases(Type type) =>
        from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        where !method.IsSpecialName && method.DeclaringType == type
        let closed = method.IsGenericMethodDefinition ? method.MakeGenericMethod(method.Name == "GetDefaultsAsync" ? typeof(InkAnnotationPatch) : typeof(StyleProps)) : method
        let parameters = closed.GetParameters()
        from index in Enumerable.Range(0, parameters.Length)
        let parameter = parameters[index]
        where !parameter.ParameterType.IsValueType && Nullability.Create(parameter).WriteState == NullabilityState.NotNull
        select (closed, index);

    static async Task<List<string>> Unchecked(object target)
    {
        var missing = new List<string>();
        foreach (var (method, index) in Cases(target.GetType()))
        {
            var arguments = method.GetParameters().Select((p, i) => i == index ? null : Valid(p.ParameterType)).ToArray();
            Exception? error;
            try
            {
                var result = method.Invoke(target, arguments);
                error = result is Task task ? await Record.ExceptionAsync(() => task) : null;
            }
            catch (TargetInvocationException invocation)
            {
                error = invocation.InnerException;
            }
            if (error is not ArgumentNullException) missing.Add($"{method.Name}({method.GetParameters()[index].Name}): {error?.GetType().Name ?? "no error"}");
        }
        return missing;
    }

    [Fact]
    public async Task PdfViewerChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        Assert.Empty(await Unchecked(viewer));
    }

    [Fact]
    public async Task EveryWaveACapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Metadata = true, Search = new(), Render = new(), History = true, Outline = true, Attachment = true,
            PageEdit = true, ViewManager = true, Shell = true,
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var render = await viewer.GetRenderAsync(document);
        object[] targets =
        [
            await viewer.GetMetadataAsync(document), await viewer.GetSearchAsync(document), render, await viewer.GetHistoryAsync(document),
            await viewer.GetOutlineAsync(document), await viewer.GetAttachmentsAsync(document), await viewer.GetPageEditAsync(document),
            await viewer.GetViewManagerAsync(), await viewer.GetShellAsync(document), await render.CreateViewDemandAsync("main"),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
        Assert.Empty(await Unchecked(new PixelBuffer(new byte[4], 1, 1, 4)));
    }

    /// <summary>Each (method, parameter index) whose parameter is a collection of a reference type.</summary>
    static IEnumerable<(MethodInfo Method, int Index, Type Element)> CollectionCases(Type type) =>
        from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        where !method.IsSpecialName && !method.IsGenericMethodDefinition
        let parameters = method.GetParameters()
        from index in Enumerable.Range(0, parameters.Length)
        let parameter = parameters[index].ParameterType
        where parameter.IsGenericType && parameter.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        let element = parameter.GetGenericArguments()[0]
        where !element.IsValueType
        select (method, index, element);

    [Fact]
    public async Task TheViewerAndEveryWaveACapabilityRefuseANullElement()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Attachment = true, ViewManager = true, Shell = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var shell = await viewer.GetShellAsync(document);
        object[] targets = [viewer, await viewer.GetAttachmentsAsync(document), await viewer.GetViewManagerAsync(), shell];
        var missing = new List<string>();
        var checkedCount = 0;
        foreach (var target in targets)
        {
            foreach (var (method, index, element) in CollectionCases(target.GetType()))
            {
                checkedCount++;
                var withNull = Array.CreateInstance(element, 1);
                var arguments = method.GetParameters().Select((p, i) => i == index ? withNull : Valid(p.ParameterType)).ToArray();
                Exception? error;
                try
                {
                    // Up front: the call itself throws, before any task exists.
                    _ = method.Invoke(target, arguments);
                    error = null;
                }
                catch (TargetInvocationException invocation)
                {
                    error = invocation.InnerException;
                }
                if (error is not ArgumentException) missing.Add($"{target.GetType().Name}.{method.Name}: {error?.GetType().Name ?? "no error"}");
            }
        }
        Assert.Empty(missing);
        Assert.Equal(4, checkedCount);
        Assert.Throws<ArgumentException>(() => { _ = shell.ApplySnapshotAsync(new ShellSnapshot([null!], [])); });
        Assert.Throws<ArgumentException>(() => { _ = shell.ApplySnapshotAsync(new ShellSnapshot([], [null!])); });
    }

    [Fact]
    public async Task EveryWaveBCapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Feedback = new(), I18n = new(), Actions = new() { OpenSequence = OpenSequence.Off } });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        object[] targets =
        [
            await viewer.GetInteractionAsync(document), await viewer.GetFeedbackAsync(), await viewer.GetI18nAsync(),
            await viewer.GetActionsAsync(document),
            new EmbeddedResourceLocaleLoader(typeof(NullArgumentTests).Assembly),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
        Assert.Throws<ArgumentNullException>(() => new EmbeddedResourceLocaleLoader(null!));
        Assert.Throws<ArgumentNullException>(() => LocalePack.FromJson(null!));
        Assert.Throws<ArgumentNullException>(() => PdfActionTree.FromJson(null!));
    }

    [Fact]
    public async Task EveryWaveCCapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Stage = new(), Selection = true, Link = true, Commands = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var stage = await viewer.GetStageAsync(document);
        viewState = await stage.GetViewStateAsync();
        object[] targets =
        [
            stage, await viewer.GetSelectionAsync(document), await viewer.GetLinkAsync(document), await viewer.GetCommandsAsync(),
            await viewer.GetPageNavigatorAsync(document), await viewer.GetPageZoomAsync(document), await viewer.GetToolSwitcherAsync(document),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
    }

    [Fact]
    public async Task EveryWaveCCapabilityRefusesANullElementUpFront()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Stage = new(), Commands = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var stage = await viewer.GetStageAsync(document);
        using var commands = await viewer.GetCommandsAsync();
        Action[] calls =
        [
            () => _ = stage.SetResponsiveRulesAsync([null!]),
            () => _ = commands.RegisterAsync(new CommandDefinition[] { null! }),
            () => _ = commands.SetDisabledCategoriesAsync([null!]),
            () => _ = commands.SetDisabledCommandsAsync([null!]),
            () => _ = new PdfViewer(new PdfViewerOptions { Stage = new() { Responsive = [null!] } }),
            () => _ = commands.UpdateSettingsAsync(new CommandsSettingsPatch { DisabledCommands = [null!] }),
            () => _ = commands.UpdateSettingsAsync(new CommandsSettingsPatch { DisabledCategories = [null!] }),
        ];
        foreach (var call in calls) Assert.Throws<ArgumentException>(call);
        // Declared async, so a null element faults the task (the null-argument rule).
        await Assert.ThrowsAsync<ArgumentException>(() => commands.RegisterAsync(new CommandDefinition("x:y", "x") { Shortcuts = [null!] }));
    }

    sealed class NoFocus : IFormFocusTarget
    {
        public bool Focus(bool select) => true;
    }

    [Fact]
    public async Task EveryWaveDCapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var annotations = await viewer.GetAnnotationAsync(document);
        object[] targets =
        [
            annotations, annotations.GetSelection(), annotations.GetTools(), annotations.GetDrafts(), annotations.GetThreads(),
            await viewer.GetFormAsync(document),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
    }

    [Fact]
    public async Task EveryWaveDCapabilityRefusesANullElementUpFront()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var annotations = await viewer.GetAnnotationAsync(document);
        using var selection = annotations.GetSelection();
        using var drafts = annotations.GetDrafts();
        using var form = await viewer.GetFormAsync(document);
        Action[] calls =
        [
            () => _ = annotations.ReorderAsync([null!], new AnnotationPosition.End()),
            () => _ = annotations.ExportAsync([null!]),
            () => _ = selection.SelectAsync([null!]),
            () => _ = drafts.PreviewMarkupAsync(AnnotationSubtype.Highlight, [null!]),
        ];
        foreach (var call in calls) Assert.Throws<ArgumentException>(call);
        // Declared async, so a null element faults the task (the null-argument rule).
        await Assert.ThrowsAsync<ArgumentException>(() => form.SetValuesAsync([null!]));
        await Assert.ThrowsAsync<ArgumentException>(() => form.ResetAsync([null!]));
        await Assert.ThrowsAsync<ArgumentException>(() => form.ReorderCalculationsAsync([null!], new FormCalculationPosition.End()));
    }

    [Fact]
    public async Task EveryWaveECapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new(), Redaction = new(), Measurement = new(), Stamp = new(), Signature = new(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        object[] targets =
        [
            await viewer.GetRedactionAsync(document), await viewer.GetMeasurementAsync(document), await viewer.GetStampsAsync(),
            await viewer.GetSignaturesAsync(document), new FileStampLibraryStore(Path.Combine(Path.GetTempPath(), "embedpdf-null-args")),
            new HttpClientSignatureTransport(), new X509CertificateSigner(TestKeys.Certificate()),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
        Assert.Throws<ArgumentNullException>(() => TrustAnchors.FromPem(null!));
        Assert.Throws<ArgumentNullException>(() => TrustAnchors.FromCertificates(null!));
        Assert.Throws<ArgumentNullException>(() => TrustAnchors.FromEmbeddedResources(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => TrustAnchors.FromEmbeddedResources(typeof(NullArgumentTests).Assembly, null!));
        Assert.Throws<ArgumentNullException>(() => new TrustAnchors(null!));
        Assert.Throws<ArgumentNullException>(() => X509CertificateSigner.FromPkcs12File(null!));
        Assert.Throws<ArgumentNullException>(() => X509CertificateSigner.FromStore(null!));
    }

    [Fact]
    public async Task EveryWaveECapabilityRefusesANullElementUpFront()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Redaction = new(), Stamp = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var redaction = await viewer.GetRedactionAsync(document);
        using var stamps = await viewer.GetStampsAsync();
        var store = new FileStampLibraryStore(Path.Combine(Path.GetTempPath(), "embedpdf-null-args"));
        Action[] calls =
        [
            () => _ = redaction.UnmarkAsync([null!]),
            () => _ = redaction.ApplyAsync([null!]),
            () => _ = redaction.EstimateCollateralAsync([null!]),
            () => _ = stamps.CreateAssetFromAnnotationsAsync([null!], document, new PageRef(1)),
            () => _ = stamps.GetLibrariesAsync([null!]),
            () => _ = stamps.PersistAsync(store, [null!]),
            () => _ = TrustAnchors.FromPem([null!]),
            () => _ = new X509CertificateSigner(TestKeys.Certificate(), [null!]),
        ];
        foreach (var call in calls) Assert.Throws<ArgumentException>(call);
    }

    [Fact]
    public async Task TheBuilderAndTheFetcherCheckEveryReferenceArgument()
    {
        Assert.Empty(await Unchecked(new PdfViewerOptionsBuilder()));
        Assert.Empty(await Unchecked(new HttpClientDocumentFetcher()));
    }

    [Fact]
    public async Task EveryWaveBCapabilityRefusesANullElementUpFront()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, I18n = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var interaction = await viewer.GetInteractionAsync(document);
        using var i18n = await viewer.GetI18nAsync();
        var pack = new LocalePack("x", "x", new Dictionary<string, string> { ["k"] = null! });
        Action[] calls =
        [
            () => _ = interaction.SetToolCursorAsync(ToolId.Pointer, new Dictionary<string, string> { ["default"] = null! }),
            () => _ = i18n.TranslateAsync("k", new Dictionary<string, I18nParam> { ["name"] = null! }),
            () => _ = i18n.AddTranslationsAsync("x", new Dictionary<string, string> { ["k"] = null! }),
            () => _ = i18n.UpdateSettingsAsync(new I18nSettingsPatch { Locales = [null!] }),
            () => _ = i18n.UpdateSettingsAsync(new I18nSettingsPatch { Locales = [pack] }),
            () => _ = new PdfViewer(new PdfViewerOptions { I18n = new() { Locales = [null!] } }),
            () => _ = new PdfViewer(new PdfViewerOptions { I18n = new() { LazyLocales = [null!] } }),
        ];
        foreach (var call in calls) Assert.Throws<ArgumentException>(call);
        // Declared async, so a null element faults the task (the null-argument rule).
        await Assert.ThrowsAsync<ArgumentException>(() => interaction.RegisterToolAsync(new InteractionTool("x", "default", [null!])));
        await Assert.ThrowsAsync<ArgumentException>(() => i18n.RegisterLocaleAsync(pack));
    }
}
