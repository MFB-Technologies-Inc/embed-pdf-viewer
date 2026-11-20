using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// A public enum value outside its cases is refused with <c>invalid-input</c>
/// before it reaches the engine, never sent as whichever case shares its number.
/// </summary>
public sealed class EnumRangeTests
{
    public static TheoryData<string, Action> Conversions => new()
    {
        { "trapped", () => Ffi.ToFfi(new MetadataPatch { Trapped = (DocumentMetadataTrapped)9 }) },
        { "metadata field", () => Ffi.ToFfi(new MetadataPatch { Clear = [(MetadataField)9] }) },
        { "turn", () => Ffi.ToFfi((PageEditTurn)9) },
        { "rotation", () => Ffi.ToFfi((PageEditRotation)9) },
        { "outline patch key", () => Ffi.ToFfi(new OutlineItemPatch { Clear = [(OutlinePatchKey)9] }) },
        { "scroll behavior", () => Ffi.ToFfi(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 }) },
        { "scroll behavior", () => Ffi.ToFfi(new SearchSettingsPatch { Reveal = new SearchRevealPatch { Behavior = (SearchScrollBehavior)9 } }) },
        { "blend mode", () => Ffi.ToFfi(new SearchSettingsPatch { Highlight = new SearchHighlightPatch { BlendMode = (SearchBlendMode)99 } }) },
        { "rotation", () => Ffi.ToFfi(new RenderPageOptions { Rotation = (RenderRotation)9 }) },
        { "content inclusion", () => Ffi.ToFfi(new RenderPageOptions { IncludeAnnotations = (ContentInclusion)9 }) },
        { "encoding format", () => Ffi.ToFfi(new RenderSettingsPatch { Format = (RenderEncodingFormat)9 }) },
        { "invalidate scope", () => Ffi.ToFfi((RenderInvalidateScope)9) },
        { "save mode", () => Ffi.ToFfi((SaveMode)9) },
        { "settings field", () => Ffi.ToFfi(new ViewerSettingsPatch { Clear = [(ViewerSettingsField)9] }) },
        { "standard command list", () => ((StandardCommandList)9).ToFfi() },
        { "pointer phase", () => Ffi.ToFfi(new PointerSample((PointerPhase)9, default, default)) },
        { "pointer kind", () => Ffi.ToFfi(new PointerSample(PointerPhase.Down, default, default) { PointerType = (PointerKind)9 }) },
        { "pointer gesture", () => Ffi.ToFfi(new PointerSample(PointerPhase.Down, default, default) { Gesture = (PointerGesture)9 }) },
        { "tool touch", () => Ffi.ToFfi(new InteractionTool("x", "default", []) { Touch = (InteractionToolTouch)9 }) },
        { "impact weight", () => Ffi.ToFfi((FeedbackImpactWeight)9) },
        { "notify kind", () => Ffi.ToFfi((FeedbackNotifyKind)9) },
        { "text direction", () => Ffi.ToFfi(new LocalePack("x", "x", new Dictionary<string, string>()) { Direction = (TextDirection)9 }) },
        { "action origin", () => Ffi.ToFfi(new ActionContext((ActionOrigin)9, new ActionSource.Api(), new ActionTriggerEvent.Activate())) },
        { "action origin", () => Ffi.ToFfi(new PartialActionContext { Origin = (ActionOrigin)9 }) },
        { "policy decision", () => Ffi.ToFfi(new ActionPolicyPatch { Uri = new PartialActionPolicyRow { User = (ActionPolicyDecision)9 } }) },
        { "open sequence", () => Ffi.ToFfi(new ActionsSettingsPatch { OpenSequence = (OpenSequence)9 }) },
        { "page event", () => Ffi.ToFfi(new ActionTrigger.Page((PageEventKind)9, new PageRef(1))) },
        { "page event", () => Ffi.ToFfi(new ActionTreeSource.Page(new PageRef(1), (PageEventKind)9)) },
        { "annotation event", () => Ffi.ToFfi(new ActionTriggerEvent.Annotation((PdfAnnotationEventKind)9)) },
        { "field event", () => Ffi.ToFfi(new ActionTreeSource.Field(new PdfObjectRef("""{"objectNumber":7,"generation":0}"""), (PdfFieldEventKind)9)) },
        { "document event", () => Ffi.ToFfi(new ActionTrigger.Document((DocumentTriggerEvent)9)) },
        { "document event", () => Ffi.ToFfi(new ActionTreeSource.Document((DocumentTriggerEvent)9)) },
        { "document verb", () => Ffi.ToFfi((DocumentVerb)9) },
        { "scroll behavior", () => Ffi.ToFfi(new StageScrollToOptions { Behavior = (StageScrollBehavior)9 }) },
        { "flow", () => Ffi.ToFfi(new StageSettingsPatch { Flow = (StageFlow)9 }) },
        { "layout", () => Ffi.ToFfi(new StageSettingsPatch { Layout = (StageLayout)9 }) },
        { "spread mode", () => Ffi.ToFfi(new StageSettingsPatch { Spread = (StageSpreadMode)9 }) },
        { "sizing mode", () => Ffi.ToFfi(new StageSettingsPatch { Sizing = (StageSizingMode)9 }) },
        { "direction", () => Ffi.ToFfi(new StageSettingsPatch { Direction = (StageDirection)9 }) },
        { "align", () => Ffi.ToFfi(new StageSettingsPatch { FitAlign = new StageAlignment((StageAlign)9, StageAlign.Start) }) },
        { "zoom mode", () => Ffi.ToFfi(new StageSettingsPatch { Zoom = new ZoomSpec.Mode((ZoomMode)9) }) },
        { "orientation", () => Ffi.ToFfi(new StageBoxQuery { Orientation = (StageOrientation)9 }) },
        { "reveal word", () => Ffi.ToFfiOrNull(new StageRevealOptions { Anchor = new StageRevealAnchor { X = new StageRevealAnchorValue.Word((StageRevealWord)9) } }) },
    };

    [Theory]
    [MemberData(nameof(Conversions))]
    public void AnUndefinedValueIsInvalidInput(string argument, Action convert)
    {
        var error = Assert.Throws<PdfException>(convert);
        Assert.Equal((PdfErrorCode.InvalidInput, "RangeError"), (error.Code, error.Name));
        Assert.StartsWith(argument + " 9", error.Detail);
    }

    [Fact]
    public async Task TheViewerRefusesAnUndefinedValue()
    {
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new PdfViewer(new PdfViewerOptions { StandardCommands = [(StandardCommandList)9] })).Code);
        await using var viewer = await PdfViewer.CreateAsync();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var save = viewer.SaveAsync(document, (SaveMode)9);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => save)).Code);
        var update = viewer.UpdateSettingsAsync(new ViewerSettingsPatch { Clear = [(ViewerSettingsField)9] });
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => update)).Code);
    }

    [Fact]
    public async Task ACapabilityRefusesAnUndefinedValue()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Render = new() });
        var document = await viewer.OpenAsync(Fixture.Data("colors"));
        using var render = await viewer.GetRenderAsync(document);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => render.InvalidateAsync([page], (RenderInvalidateScope)9))).Code);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => render.RenderThumbnailAsync(page, (ContentInclusion)9))).Code);
    }
}
