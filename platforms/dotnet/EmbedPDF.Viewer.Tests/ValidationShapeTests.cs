using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// An argument that is present but invalid (a negative index or page number,
/// an undefined enum value, a negative size) is reported by the returned task,
/// never thrown while the call is made, so <c>var task = call(); await task;</c>
/// and <c>await call()</c> fail alike. Null arguments are
/// <see cref="NullArgumentTests"/>' business.
/// </summary>
public sealed class ValidationShapeTests
{
    static readonly PageRef Bad = new(-1);
    static readonly OutlineItemRef BadItem = new(-1);

    static async Task ExpectFaulted(string name, Func<Task> call)
    {
        Task task;
        try
        {
            task = call();
        }
        catch (Exception error)
        {
            Assert.Fail($"{name} threw {error.GetType().Name} synchronously: {error.Message}");
            return;
        }
        var failure = await Assert.ThrowsAsync<PdfException>(() => task);
        Assert.Equal(PdfErrorCode.InvalidInput, failure.Code);
    }

    [Fact]
    public async Task InvalidArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Metadata = true, Search = new(), Render = new(), History = true, Outline = true, Attachment = true,
            PageEdit = true, ViewManager = true, Shell = true,
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using var edit = await viewer.GetPageEditAsync(document);
        using var render = await viewer.GetRenderAsync(document);
        using var outline = await viewer.GetOutlineAsync(document);
        using var attachments = await viewer.GetAttachmentsAsync(document);
        using var manager = await viewer.GetViewManagerAsync();
        using var search = await viewer.GetSearchAsync(document);
        using var metadata = await viewer.GetMetadataAsync(document);
        var view = await render.CreateViewDemandAsync("main");
        var file = new AttachedFile("x", new AttachmentHome.Document(new AttachmentRef("x")), "x", null, null, -1, null, null, null);
        var calls = new (string, Func<Task>)[]
        {
            ("PageEdit.SelectAsync", () => edit.SelectAsync([Bad])),
            ("PageEdit.RotateAsync", () => edit.RotateAsync([page], (PageEditTurn)9)),
            ("PageEdit.SetRotationAsync", () => edit.SetRotationAsync((PageEditRotation)9, [page])),
            ("PageEdit.MovePagesAsync", () => edit.MovePagesAsync([Bad], new PageEditPlacement.End())),
            ("PageEdit.DeletePagesAsync", () => edit.DeletePagesAsync([Bad])),
            ("PageEdit.InsertBlankAsync", () => edit.InsertBlankAsync(new PageEditBlankOptions { Count = -1 })),
            ("PageEdit.InsertAsync(bytes)", () => edit.InsertAsync(Fixture.Data("colors"), new PageEditBytesOptions { PageIndexes = [-1] })),
            ("PageEdit.InsertAsync(document)", () => edit.InsertAsync([Bad], document)),
            ("PageEdit.DuplicateAsync", () => edit.DuplicateAsync([Bad])),
            ("PageEdit.ExtractAsync", () => edit.ExtractAsync([Bad])),
            ("Render.RenderPageAsync", () => render.RenderPageAsync(Bad)),
            ("Render.RenderThumbnailAsync", () => render.RenderThumbnailAsync(page, (ContentInclusion)9)),
            ("Render.RenderPagesAsync", () => render.RenderPagesAsync([page], concurrency: -1)),
            ("Render.GetRenderEpochAsync", () => render.GetRenderEpochAsync(Bad)),
            ("Render.InvalidateAsync", () => render.InvalidateAsync([page], (RenderInvalidateScope)9)),
            ("Render.RenderSourceAsync", () => render.RenderSourceAsync(Bad, 1)),
            ("Render.GetSourceKeyAsync", () => render.GetSourceKeyAsync(Bad, 1)),
            ("Render.ConformViewportAsync", () => render.ConformViewportAsync(Bad, 1)),
            ("Render.GetSnapshotBoxAsync", () => render.GetSnapshotBoxAsync(Bad)),
            ("Render.UpdateSettingsAsync", () => render.UpdateSettingsAsync(new RenderSettingsPatch { Format = (RenderEncodingFormat)9 })),
            ("RenderViewDemand.SetDemandAsync", () => view.SetDemandAsync(Bad, new RenderPageViewDemand(80))),
            ("RenderViewDemand.GetPlanAsync", () => view.GetPlanAsync(Bad)),
            ("RenderViewDemand.MarkPaintedAsync", () => view.MarkPaintedAsync(Bad, "k")),
            ("RenderViewDemand.MarkUnpaintedAsync", () => view.MarkUnpaintedAsync(Bad, "k")),
            ("RenderViewDemand.ReleaseAsync", () => view.ReleaseAsync(Bad)),
            ("Outline.GetItemAsync", () => outline.GetItemAsync(BadItem)),
            ("Outline.GetChildrenAsync", () => outline.GetChildrenAsync(BadItem)),
            ("Outline.SelectAsync", () => outline.SelectAsync([BadItem])),
            ("Outline.IsExpandedAsync", () => outline.IsExpandedAsync(BadItem)),
            ("Outline.ActivateAsync", () => outline.ActivateAsync(BadItem)),
            ("Outline.CreateAsync", () => outline.CreateAsync(new OutlineItemInput { Parent = BadItem })),
            ("Outline.UpdateAsync", () => outline.UpdateAsync(BadItem, new OutlineItemPatch())),
            ("Outline.MoveAsync", () => outline.MoveAsync([BadItem], null)),
            ("Outline.DeleteAsync", () => outline.DeleteAsync([BadItem])),
            ("Outline.ExpandAsync", () => outline.ExpandAsync(BadItem)),
            ("Outline.CollapseAsync", () => outline.CollapseAsync(BadItem)),
            ("Outline.ToggleAsync", () => outline.ToggleAsync(BadItem)),
            ("Outline.RevealAsync", () => outline.RevealAsync(BadItem)),
            ("Attachment.SelectAsync", () => attachments.SelectAsync([file])),
            ("Attachment.IsPendingAsync", () => attachments.IsPendingAsync(file)),
            ("Attachment.CanRemoveAsync", () => attachments.CanRemoveAsync(file)),
            ("Attachment.RevealAsync", () => attachments.RevealAsync(file)),
            ("Attachment.RemoveAsync", () => attachments.RemoveAsync(file)),
            ("Attachment.DownloadAsync", () => attachments.DownloadAsync(file)),
            ("ViewManager.MovePaneAsync", () => manager.MovePaneAsync("pane-1", -1)),
            ("ViewManager.AddDocumentAsync", () => manager.AddDocumentAsync(document, "pane-1", -1)),
            ("ViewManager.MoveDocumentAsync(within)", () => manager.MoveDocumentAsync(document, "pane-1", -1)),
            ("ViewManager.MoveDocumentAsync(between)", () => manager.MoveDocumentAsync(document, "pane-1", "pane-2", -1)),
            ("Search.SearchAsync", () => search.SearchAsync(new SearchQuery("x"), Bad)),
            ("Search.GoToHitAsync", () => search.GoToHitAsync(-1)),
            ("Search.GetHitsAsync", () => search.GetHitsAsync(Bad)),
            ("Search.GetHitCountAsync", () => search.GetHitCountAsync(Bad)),
            ("Search.NextHitAsync", () => search.NextHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.PreviousHitAsync", () => search.PreviousHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.RevealActiveHitAsync", () => search.RevealActiveHitAsync(new SearchRevealOptions { Behavior = (SearchScrollBehavior)9 })),
            ("Search.FindAllAsync", () => search.FindAllAsync(new SearchQuery("x"), (ContentInclusion)9)),
            ("Search.UpdateSettingsAsync", () => search.UpdateSettingsAsync(new SearchSettingsPatch { Highlight = new SearchHighlightPatch { BlendMode = (SearchBlendMode)99 } })),
            ("Metadata.UpdateAsync", () => metadata.UpdateAsync(new MetadataPatch { Clear = [(MetadataField)9] })),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
        await view.DisposeAsync();
    }

    [Fact]
    public async Task InvalidWaveBArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Feedback = new(), I18n = new(), Actions = new() { OpenSequence = OpenSequence.Off },
        });
        var document = await viewer.OpenAsync(Fixture.Data("document_actions"));
        using var interaction = await viewer.GetInteractionAsync(document);
        using var feedback = await viewer.GetFeedbackAsync();
        using var i18n = await viewer.GetI18nAsync();
        using var actions = await viewer.GetActionsAsync(document);
        var sample = new PointerSample((PointerPhase)9, default, default);
        var badPack = new LocalePack("x", "x", new Dictionary<string, string>()) { Direction = (TextDirection)9 };
        var badContext = new ActionContext((ActionOrigin)9, new ActionSource.Api(), new ActionTriggerEvent.Activate());
        var badRef = new PdfObjectRef("{");
        var tree = ActionsTests.UriTree("https://a.test/");
        var calls = new (string, Func<Task>)[]
        {
            ("Interaction.WouldClaimTouchAsync", () => interaction.WouldClaimTouchAsync(sample)),
            ("Interaction.WouldClaimTouchAsync(page)", () => interaction.WouldClaimTouchAsync(new PointerSample(PointerPhase.Down, default, default) { Page = new PointerPageHit(Bad, default) })),
            ("Interaction.RegisterToolAsync", () => interaction.RegisterToolAsync(new InteractionTool("x", "default", []) { Touch = (InteractionToolTouch)9 })),
            ("Feedback.ImpactAsync", () => feedback.ImpactAsync((FeedbackImpactWeight)9)),
            ("Feedback.NotifyAsync", () => feedback.NotifyAsync((FeedbackNotifyKind)9)),
            ("I18n.RegisterLocaleAsync", () => i18n.RegisterLocaleAsync(badPack)),
            ("I18n.UpdateSettingsAsync", () => i18n.UpdateSettingsAsync(new I18nSettingsPatch { Locales = [badPack] })),
            ("Actions.ExecuteAsync", () => actions.ExecuteAsync(tree, badContext)),
            ("Actions.CanExecuteAsync", () => actions.CanExecuteAsync(tree, badContext)),
            ("Actions.ExecuteNamedAsync", () => actions.ExecuteNamedAsync("NextPage", new PartialActionContext { Origin = (ActionOrigin)9 })),
            ("Actions.CanExecuteNamedAsync", () => actions.CanExecuteNamedAsync("NextPage", new PartialActionContext { Origin = (ActionOrigin)9 })),
            ("Actions.GetActionTreeAsync", () => actions.GetActionTreeAsync(new ActionTreeSource.Page(Bad, null))),
            ("Actions.GetActionTreeAsync(ref)", () => actions.GetActionTreeAsync(new ActionTreeSource.Outline(badRef))),
            ("Actions.DispatchAsync", () => actions.DispatchAsync(new ActionTrigger.Page((PageEventKind)9, new PageRef(1)))),
            ("Actions.CanDispatchAsync", () => actions.CanDispatchAsync(new ActionTrigger.Activate(badRef, new PageRef(1), null))),
            ("Actions.UpdatePolicyAsync", () => actions.UpdatePolicyAsync(new ActionPolicyPatch { Goto = new PartialActionPolicyRow { Hover = (ActionPolicyDecision)9 } })),
            ("Actions.UpdateSettingsAsync", () => actions.UpdateSettingsAsync(new ActionsSettingsPatch { OpenSequence = (OpenSequence)9 })),
            ("Actions.RunDocumentVerbAsync", () => actions.RunDocumentVerbAsync((DocumentVerb)9, () => Task.CompletedTask)),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
        // DispatchPointer returns no task, so it refuses at once.
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => interaction.DispatchPointer(sample)).Code);
    }

    [Fact]
    public async Task InvalidWaveCArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Stage = new(), Selection = true, Link = true, Commands = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var stage = await viewer.GetStageAsync(document);
        using var selection = await viewer.GetSelectionAsync(document);
        using var link = await viewer.GetLinkAsync(document);
        using var commands = await viewer.GetCommandsAsync();
        using var navigator = await viewer.GetPageNavigatorAsync(document);
        var badOptions = new StageGoToOptions { Behavior = (StageScrollBehavior)9 };
        var calls = new (string, Func<Task>)[]
        {
            ("Stage.GoToPageIndexAsync", () => stage.GoToPageIndexAsync(-1)),
            ("Stage.RevealIndexAsync", () => stage.RevealIndexAsync(-1)),
            ("Stage.GoToPageAsync", () => stage.GoToPageAsync(Bad)),
            ("Stage.NextPageAsync", () => stage.NextPageAsync(badOptions)),
            ("Stage.GoToDestinationAsync", () => stage.GoToDestinationAsync(new PageDestination.Fit(Bad))),
            ("Stage.GoToDestinationAsync(behavior)", () => stage.GoToDestinationAsync(new PageDestination.Fit(new PageRef(1)), (StageScrollBehavior)9)),
            ("Stage.ScrollToAsync", () => stage.ScrollToAsync(new StageScrollToOptions { Behavior = (StageScrollBehavior)9 })),
            ("Stage.UpdateSettingsAsync", () => stage.UpdateSettingsAsync(new StageSettingsPatch { Columns = new StageGridColumns.Count(-1) })),
            ("Stage.SetResponsiveRulesAsync", () => stage.SetResponsiveRulesAsync([new StageResponsiveRule(new StageBoxQuery { Orientation = (StageOrientation)9 })])),
            ("Stage.ZoomToAsync", () => stage.ZoomToAsync(new StageZoomTarget.Spec(new ZoomSpec.Mode((ZoomMode)9)))),
            ("Stage.RevealAsync", () => stage.RevealAsync(new PageRef(1), new StageRevealOptions { Behavior = (StageScrollBehavior)9 })),
            ("Stage.IsPageVisibleAsync", () => stage.IsPageVisibleAsync(Bad)),
            ("Navigator.GoToPageIndexAsync", () => navigator.GoToPageIndexAsync(-1)),
            ("Selection.SelectPageAsync", () => selection.SelectPageAsync(Bad)),
            ("Selection.SelectAsync", () => selection.SelectAsync(new SelectionInput.Span(Bad, 0, 1))),
            ("Selection.ReadTextAsync(range)", () => selection.ReadTextAsync(new SelectionTextRange(new SelectionTextPosition(Bad, 0), new SelectionTextPosition(Bad, 1)))),
            ("Link.GetLinksAsync", () => link.GetLinksAsync(Bad)),
            ("Link.ActivateAtAsync", () => link.ActivateAtAsync(Bad, default)),
            ("Commands.SetAppStateAsync", () => commands.SetAppStateAsync("\"\uD800\"")),
            ("Commands.ExecuteAsync", () => commands.ExecuteAsync("x:y", argsJson: "\"\uDC00\"")),
            ("Commands.UpdateSettingsAsync", () => commands.UpdateSettingsAsync(new CommandsSettingsPatch { NotAllowed = (NotAllowedMode)9 })),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
    }

    /// <summary>
    /// JSON bound for the engine with an unpaired surrogate would cross as
    /// U+FFFD and change what it says, so every wave D member that takes JSON
    /// text refuses it, as the commands and link members do.
    /// </summary>
    [Fact]
    public async Task WaveDJsonWithAnUnpairedSurrogateIsInvalidInput()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using var annotations = await viewer.GetAnnotationAsync(document);
        await annotations.RefreshAsync();
        using var selection = annotations.GetSelection();
        using var tools = annotations.GetTools();
        using var drafts = annotations.GetDrafts();
        using var threads = annotations.GetThreads();
        using var form = await viewer.GetFormAsync(document);
        var square = await annotations.CreateAsync(new SquareAnnotationDraft(new PdfRect(10, 10, 20, 20)), page);
        var field = new FormFieldRef.Fqn("x");
        const string bad = "{\"contents\":\"\uD800\"}";
        var anchor = new AnnotationTextEndAnchor("\"\uDC00\"");
        var calls = new (string, Func<Task>)[]
        {
            ("Annotation.UpdateSettingsAsync", () => annotations.UpdateSettingsAsync(bad)),
            ("Annotation.ImportAsync(bundle)", () => annotations.ImportAsync(bad)),
            ("Annotation.ImportAsync(options)", () => annotations.ImportAsync("{}", bad)),
            ("Annotation.CreateAsync(json)", () => annotations.CreateAsync(bad, page)),
            ("Annotation.UpdateAsync(json)", () => annotations.UpdateAsync(square, bad)),
            ("Selection.UpdateAsync(json)", () => selection.UpdateAsync(bad)),
            ("Tools.UpdateDefaultsAsync(json)", () => tools.UpdateDefaultsAsync(bad, ToolId.Ink)),
            ("Drafts.CreateMarkupAsync", () => drafts.CreateMarkupAsync(AnnotationSubtype.Highlight, bad, page)),
            ("Drafts.CreateReplaceTextAsync(quads)", () => drafts.CreateReplaceTextAsync(bad, new AnnotationTextEndAnchor("{}"), page)),
            ("Drafts.CreateReplaceTextAsync(anchor)", () => drafts.CreateReplaceTextAsync("[]", anchor, page)),
            ("Drafts.CreateCaretAsync", () => drafts.CreateCaretAsync(anchor, page)),
            ("Drafts.PreviewMarkupAsync", () => drafts.PreviewMarkupAsync(AnnotationSubtype.Highlight, [new AnnotationPageQuads(page, bad)])),
            ("Threads.SetLinkAsync(json)", () => threads.SetLinkAsync(square, bad)),
            ("Annotation.CreateAsync(draft)", () => annotations.CreateAsync(new SquareAnnotationDraft(new PdfRect(0, 0, 1, 1)) { Contents = "\uD800" }, page)),
            ("Form.ImportValuesAsync", () => form.ImportValuesAsync(bad)),
            ("Form.CreateAsync(json)", () => form.CreateAsync(bad)),
            ("Form.UpdateAsync(json)", () => form.UpdateAsync(field, bad)),
            ("Form.AddWidgetAsync(json)", () => form.AddWidgetAsync(field, bad)),
            ("Form.UpdateWidgetAsync", () => form.UpdateWidgetAsync(square, bad)),
            ("Form.ImportAsync(bundle)", () => form.ImportAsync(bad)),
            ("Form.ImportAsync(options)", () => form.ImportAsync("{}", bad)),
        };
        foreach (var (name, call) in calls)
        {
            await ExpectFaulted(name, call);
            var error = await Assert.ThrowsAsync<PdfException>(call);
            Assert.True(error.Code == PdfErrorCode.InvalidInput && error.Name == "EncodingError", $"{name}: {error.Name} {error.Code}");
        }
        Assert.Single(await annotations.ListAsync());
    }

    [Fact]
    public async Task InvalidWaveDArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var annotations = await viewer.GetAnnotationAsync(document);
        using var selection = annotations.GetSelection();
        using var drafts = annotations.GetDrafts();
        using var threads = annotations.GetThreads();
        using var form = await viewer.GetFormAsync(document);
        var note = new AnnotationRef.ObjectNumber(new PageRef(1), 1);
        var field = new FormFieldRef.Fqn("x");
        var calls = new (string, Func<Task>)[]
        {
            ("Annotation.ListAsync", () => annotations.ListAsync(new AnnotationFilter { Pages = [Bad] })),
            ("Annotation.HitTestAsync", () => annotations.HitTestAsync(Bad, default)),
            ("Annotation.GetAsync", () => annotations.GetAsync(new AnnotationRef.ObjectNumber(Bad, 1))),
            ("Annotation.CreateAsync(NaN)", () => annotations.CreateAsync(new InkAnnotationDraft([[new(double.NaN, 0)]]), new PageRef(1))),
            ("Annotation.CreateAsync(enum)", () => annotations.CreateAsync(new SquareAnnotationDraft(default) { BlendMode = (AnnotationBlendMode)99 }, new PageRef(1))),
            ("Annotation.UpdateAsync(enum)", () => annotations.UpdateAsync(note, new SquareAnnotationPatch { BorderStyle = (AnnotationBorderStyle)9 })),
            ("Selection.ToggleAsync", () => selection.ToggleAsync((AnnotationTextFormat)9)),
            ("Selection.SelectInRectAsync", () => selection.SelectInRectAsync(Bad, default)),
            ("Drafts.SetTextSelectionAsync", () => drafts.SetTextSelectionAsync(note, -1, 2)),
            ("Drafts.PlaceAtAsync", () => drafts.PlaceAtAsync(Bad, default)),
            ("Threads.SetStatusAsync", () => threads.SetStatusAsync(note, (AnnotationThreadStatus)9)),
            ("Form.NotifyWidgetEventAsync", () => form.NotifyWidgetEventAsync(field, note, (FormWidgetEvent)9)),
            ("Form.GetFillItemsAsync", () => form.GetFillItemsAsync(Bad)),
            ("Form.GetFieldsAsync", () => form.GetFieldsAsync(new FormFilter { Family = (FormFieldFamily)99 })),
            ("Form.UpdateAsync", () => form.UpdateAsync(field, new FormFieldPatch { MaxLength = -1 })),
            ("Form.UpdateSettingsAsync", () => form.UpdateSettingsAsync(new FormSettingsPatch { Validation = (FormValidation)9 })),
            ("Form.GetFieldAsync", () => form.GetFieldAsync(new FormFieldRef.ObjectNumber(-1))),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
    }

    [Fact]
    public async Task InvalidWaveEArgumentsFaultTheReturnedTask()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new(), Redaction = new(), Measurement = new(), Stamp = new(), Signature = new(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var redaction = await viewer.GetRedactionAsync(document);
        using var measurement = await viewer.GetMeasurementAsync(document);
        using var stamps = await viewer.GetStampsAsync();
        using var signatures = await viewer.GetSignaturesAsync(document);
        var page = new MeasurementPageTarget.Page(new PageRef(1));
        var calls = new (string, Func<Task>)[]
        {
            ("Redaction.MarkAreaAsync", () => redaction.MarkAreaAsync(default, Bad)),
            ("Redaction.GetPendingAsync", () => redaction.GetPendingAsync(Bad)),
            ("Redaction.ApplyPagesAsync", () => redaction.ApplyPagesAsync([Bad])),
            ("Measurement.GetPageScaleAsync", () => measurement.GetPageScaleAsync(Bad)),
            ("Measurement.SetScaleAsync(NaN)", () => measurement.SetScaleAsync(page, new PdfMeasurement("RL") { Cyx = double.NaN })),
            ("Measurement.SetPrecisionAsync(NaN)", () => measurement.SetPrecisionAsync(page, double.NaN)),
            ("Measurement.CreateMeasurementAsync", () => measurement.CreateMeasurementAsync(new CreateMeasurementInput((MeasurementKind)9, new PageRef(1), []))),
            ("Stamps.GetAssetsAsync", () => stamps.GetAssetsAsync(new StampAssetFilter { Kind = (StampAssetKind)9 })),
            ("Stamps.RenderAssetPreviewAsync", () => stamps.RenderAssetPreviewAsync("x", double.NaN)),
            ("Stamps.UpdateSettingsAsync", () => stamps.UpdateSettingsAsync(new StampConfig { PreviewWidth = double.PositiveInfinity })),
            ("Signatures.AnalyzeChangesAsync(-1)", () => signatures.AnalyzeChangesAsync(-1)),
            ("Signatures.AnalyzeChangesAsync(enum)", () => signatures.AnalyzeChangesAsync(0, (SignatureModificationsBasis)9)),
            ("Signatures.ValidateAsync", () => signatures.ValidateAsync(new SignatureValidateRequest { Until = (SignatureModificationsBasis)9 })),
            ("Signatures.SignAsync", () => signatures.SignAsync(new SignatureSignInput(new FormFieldRef.Fqn("x")) { Certify = 256 })),
            ("Signatures.CanSignAsync", () => signatures.CanSignAsync(new FormFieldRef.ObjectNumber(-1))),
        };
        foreach (var (name, call) in calls) await ExpectFaulted(name, call);
    }
}
