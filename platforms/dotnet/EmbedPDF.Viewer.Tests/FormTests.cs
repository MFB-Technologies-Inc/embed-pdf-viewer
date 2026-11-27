using EmbedPDF.Scripting.Jint;
using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class FormTests
{
    static readonly CancellationToken Cancelled = new(true);

    static async Task<(PdfViewer Viewer, DocumentInfo Document, FormCapability Form)> Opened(string fixture = "form_fields")
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Form = new FormConfig { Validation = FormValidation.None } });
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var form = await viewer.GetFormAsync(document);
        await form.RefreshAsync();
        return (viewer, document, form);
    }

    static async Task<FormField> WritableText(FormCapability form) =>
        (await form.GetFieldsAsync()).First(f => f.Detail is FormFieldDetail.Text && !f.ReadOnly);

    sealed class CursorTarget : IFormFocusTarget
    {
        readonly List<bool> selections = [];
        public bool[] Selections { get { lock (selections) return [.. selections]; } }
        public bool Focus(bool select) { lock (selections) selections.Add(select); return true; }
    }

    sealed class ThrowingTarget : IFormFocusTarget
    {
        public int Calls;
        public bool Focus(bool select)
        {
            Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("no cursor");
        }
    }

    sealed class DisposingTarget(PdfViewer viewer) : IFormFocusTarget
    {
        public Exception? Thrown;
        public bool Focus(bool select)
        {
            try { viewer.Dispose(); }
            catch (Exception error) { Thrown = error; }
            return true;
        }
    }

    [Fact]
    public async Task AMountedControlReceivesFocusAndUnregisters()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        var widget = field.Widgets.First(w => w.AnnotationRef is not null).AnnotationRef!;
        var target = new CursorTarget();
        var registration = await form.AttachFocusTargetAsync(widget, target);
        var events = form.ObserveEvents();
        Assert.True(await form.FocusAsync(field.FieldRef, new FormFocusOptions { Select = true }));
        Assert.Equal([true], target.Selections);
        await form.NotifyWidgetEventAsync(field.FieldRef, widget, FormWidgetEvent.Focus);
        Assert.Equal(field.FieldRef, (await form.GetFocusedFieldAsync())?.FieldRef);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is not FormEvent.FocusChanged changed) continue;
            Assert.Equal(field.FieldRef, changed.Field?.FieldRef);
            Assert.Equal(widget, changed.Widget);
            break;
        }
        registration.Dispose();
        Assert.False(await form.FocusAsync(field.FieldRef));
    }

    [Fact]
    public async Task AThrowingFocusTargetLeavesFocusAnswering()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        var widget = field.Widgets.First(w => w.AnnotationRef is not null).AnnotationRef!;
        var target = new ThrowingTarget();
        using var registration = await form.AttachFocusTargetAsync(widget, target);
        Assert.True(await form.FocusAsync(field.FieldRef));
        Assert.Equal(1, target.Calls);
        Assert.NotEmpty(await form.GetFieldsAsync());
    }

    [Fact]
    public async Task DisposingFromAFocusTargetThrowsIntoIt()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        var widget = field.Widgets.First(w => w.AnnotationRef is not null).AnnotationRef!;
        var target = new DisposingTarget(viewer);
        using var registration = await form.AttachFocusTargetAsync(widget, target);
        Assert.True(await form.FocusAsync(field.FieldRef));
        Assert.IsType<InvalidOperationException>(target.Thrown);
        Assert.NotEmpty(await form.GetFieldsAsync());
    }

    [Fact]
    public async Task FieldsListWithTypedDetail()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        Assert.Equal(ResourceStatus.Ready, await form.GetStatusAsync());
        Assert.NotEmpty(await form.GetFieldsAsync());
        var snapshot = await form.GetSnapshotAsync();
        Assert.Equal(FormKind.Acroform, snapshot?.FormKind);
    }

    [Fact]
    public async Task ATextFieldFillsAndIsObserved()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        var events = form.ObserveEvents();
        var result = await form.SetValueAsync(field.FieldRef, new FormValueInput.Value("From C#"));
        Assert.Equal(FormSetValueStatus.Applied, result.Status);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is not FormEvent.ValueChanged changed) continue;
            Assert.Equal(field.FieldRef, changed.Field.FieldRef);
            break;
        }
        Assert.Equal(new FormValueInput.Value("From C#"), await form.GetValueAsync(field.FieldRef));
    }

    [Fact]
    public async Task ACheckboxIsCheckedByItsExportValue()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var box = (await form.GetFieldsAsync()).First(f => f.Detail is FormFieldDetail.Checkbox && !f.ReadOnly);
        var exportValue = box.Widgets[0].Toggle!.ExportValue;
        // form_fields.pdf's box starts checked; clear it so checking is a change.
        Assert.Equal(FormSetValueStatus.Applied, (await form.SetValueAsync(box.FieldRef, new FormValueInput.Value(null))).Status);
        Assert.Equal(FormSetValueStatus.Applied, (await form.SetValueAsync(box.FieldRef, new FormValueInput.Value(exportValue))).Status);
        var read = Assert.IsType<FormFieldDetail.Checkbox>((await form.GetFieldAsync(box.FieldRef))!.Detail);
        Assert.True(read.Checked);
    }

    [Fact]
    public async Task ExportedValuesRoundTrip()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        _ = await form.SetValueAsync(field.FieldRef, new FormValueInput.Value("Round trip"));
        var all = JsonNode.Parse(await form.ExportValuesAsync())!.AsObject();
        Assert.Equal("Round trip", (string?)all[field.Name]);
        var values = new JsonObject { [field.Name] = "Round trip" }.ToJsonString();
        var second = await viewer.OpenAsync(Fixture.Data("form_fields"));
        using var fresh = await viewer.GetFormAsync(second);
        await fresh.RefreshAsync();
        var imported = await fresh.ImportValuesAsync(values);
        Assert.Contains(field.FieldRef, imported.Applied);
        Assert.Empty(imported.Failed);
        var read = Assert.IsType<FormFieldDetail.Text>((await fresh.GetFieldAsync(field.FieldRef))!.Detail);
        Assert.Equal("Round trip", read.Value);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, form) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => form.GetFieldsAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in form.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await form.RefreshAsync();
    }

    [Fact]
    public async Task EveryCancellableMemberCancelsBeforeItStarts()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        var widget = field.Widgets[0].AnnotationRef!;
        var page = field.Widgets[0].Page!.Value;
        var before = (await form.GetFieldsAsync()).Count;
        var bundle = await form.ExportAsync();
        var values = new JsonObject { [field.Name] = "x" }.ToJsonString();
        Func<Task>[] calls =
        [
            () => form.RefreshAsync(Cancelled),
            () => form.ExportAsync(null, Cancelled),
            () => form.SetValueAsync(field.FieldRef, new FormValueInput.Value("x"), Cancelled),
            () => form.SetValueAsync(field.FieldRef, new FormValueInput.Value(null), Cancelled),
            () => form.SetValueAsync(field.FieldRef, new FormValueInput.Checked(false), Cancelled),
            () => form.SetValueAsync(field.FieldRef, new FormValueInput.SelectedValues(["a"]), Cancelled),
            () => form.SetValuesAsync([new FormValueEntry(field.FieldRef, new FormValueInput.Value("x"))], Cancelled),
            () => form.ImportValuesAsync(values, Cancelled),
            () => form.ResetAsync([field.FieldRef], Cancelled),
            () => form.ResetAsync(null, Cancelled),
            () => form.CreateAsync(new FormCreateFieldInput(FormFieldFamily.Text, "fromCSharp", page, new PdfRect(10, 10, 80, 20)), Cancelled),
            () => form.UpdateAsync(field.FieldRef, new FormFieldPatch { ReadOnly = true }, Cancelled),
            () => form.DeleteAsync(field.FieldRef, Cancelled),
            () => form.RemoveWidgetAsync(widget, field.FieldRef, Cancelled),
            () => form.AddWidgetAsync(field.FieldRef, new FormWidgetPlacement { Page = page, Bounds = new PdfRect(10, 10, 80, 20) }, Cancelled),
            () => form.ImportAsync(bundle, "{}", Cancelled),
            () => form.RepairAsync(false, Cancelled),
            () => form.ActivateAsync(widget, Cancelled),
            () => form.EnsureLoadedAsync(page, Cancelled),
        ];
        foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
        Assert.NotEqual(new FormValueInput.Value("x"), await form.GetValueAsync(field.FieldRef));
        Assert.Equal(before, (await form.GetFieldsAsync()).Count);
        var kept = await form.GetFieldAsync(field.FieldRef);
        Assert.NotNull(kept);
        Assert.Equal(field.Widgets.Count, kept.Widgets.Count);
    }

    sealed class RecordingSubmit : IActionSubmitHandler
    {
        readonly List<string?> urls = [];
        public string?[] Seen { get { lock (urls) return [.. urls]; } }
        public void Submit(ActionSubmitRequest request) { lock (urls) urls.Add(request.Url); }
    }

    [Fact]
    public async Task ASubmitReachesTheHandler()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Form = new(), Actions = new() { OpenSequence = OpenSequence.Off },
        });
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("form_fields"));
        using var actions = await viewer.GetActionsAsync(document);
        var submit = new RecordingSubmit();
        using var registration = await actions.SetSubmitHandlerAsync(submit);
        _ = await actions.ExecuteAsync(ActionsTests.SubmitTree(), ActionsTests.User);
        Assert.Equal(new string?[] { "https://home.test/submit" }, submit.Seen);
    }

    [Fact]
    public async Task ADocumentScriptRunsWithScriptingOn()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new(),
            Actions = new() { OpenSequence = OpenSequence.Off, JavaScript = new ActionsJavaScriptConfig(true) { Engine = new JintScriptEngineFactory() } },
        });
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("document_actions"));
        using var actions = await viewer.GetActionsAsync(document);
        var result = await actions.PrepareCloseAsync();
        // ActionsTests sees Inert with scripting off; with an engine and forms
        // and annotations reachable, the same script runs.
        Assert.Equal([ActionNodeStatus.Executed], result.Steps[0].Result.Nodes.Select(n => n.Status));
    }

    [Fact]
    public async Task FormSettingsKeepValidationWhileClearingFocusAndFieldBorders()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Form = new FormConfig { Validation = FormValidation.None } });
        await using var _v = viewer;
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var form = await viewer.GetFormAsync(document);
        var registered = await form.GetSettingsAsync();
        await form.UpdateSettingsAsync(new FormSettingsPatch
        {
            Focus = new() { Color = "red" }, Fields = new() { Border = "blue", Background = "white" },
            Fillable = new FormFillable.Kinds([FormFieldFamily.Text, FormFieldFamily.Checkbox]),
        });
        await form.UpdateSettingsAsync(new FormSettingsPatch { Focus = new() { ClearColor = true }, Fields = new() { ClearBorder = true } });
        var changed = await form.GetSettingsAsync();
        Assert.Equal(FormValidation.None, changed.Validation);
        Assert.Null(changed.Focus.Color);
        Assert.Null(changed.Fields.Border);
        Assert.Equal("white", changed.Fields.Background);
        Assert.Equal([FormFieldFamily.Text, FormFieldFamily.Checkbox], Assert.IsType<FormFillable.Kinds>(changed.Fillable).Families);
        await form.ResetSettingsAsync();
        var reset = await form.GetSettingsAsync();
        Assert.Equal(registered with { Fillable = reset.Fillable }, reset);
        Assert.Equal(registered.Fillable.GetType(), reset.Fillable.GetType());
    }

    [Fact]
    public async Task TheFieldEditorCreatesUpdatesAndDeletesAField()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var page = (await WritableText(form)).Widgets[0].Page!.Value;
        var created = await form.CreateAsync(new FormCreateFieldInput(FormFieldFamily.Text, "fromCSharp", page, new PdfRect(10, 10, 80, 20)));
        Assert.Equal("fromCSharp", created.Name);
        var updated = await form.UpdateAsync(created.FieldRef, new FormFieldPatch { ReadOnly = true, MaxLength = 4 });
        Assert.True(updated.ReadOnly);
        Assert.Equal(4, Assert.IsType<FormFieldDetail.Text>(updated.Detail).MaxLength);
        var cleared = await form.UpdateAsync(created.FieldRef, new FormFieldPatch { Clear = [FormFieldPatchKey.MaxLength] });
        Assert.Null(Assert.IsType<FormFieldDetail.Text>(cleared.Detail).MaxLength);
        await form.DeleteAsync(created.FieldRef);
        Assert.Null(await form.GetFieldAsync(created.FieldRef));
    }

    [Fact]
    public async Task AFieldPatchMaxLengthBelowZeroIsInvalidInput()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var field = await WritableText(form);
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => form.UpdateAsync(field.FieldRef, new FormFieldPatch { MaxLength = -1 }))).Code);
    }

    [Fact]
    public async Task FormFieldDuplicationMakesANamedCopy()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var original = await WritableText(form);
        var before = (await form.GetFieldsAsync()).Count;
        var result = await form.DuplicateAsync([original.FieldRef], new FormDuplicateOptions { Offset = new PdfPoint(0, 30) });
        var copy = Assert.Single(result.Fields);
        Assert.NotEqual(original.FieldRef, copy.FieldRef);
        Assert.StartsWith(original.Name, copy.Name);
        Assert.NotEqual(original.Name, copy.Name);
        Assert.Equal(original.Widgets.Count, result.Widgets.Count);
        Assert.Equal(before + 1, (await form.GetFieldsAsync()).Count);
        Assert.Equal("[]", result.DroppedJson);
    }

    [Fact]
    public async Task FormWidgetDuplicationMakesANewField()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var original = await WritableText(form);
        var widget = original.Widgets.First(w => w.AnnotationRef is not null).AnnotationRef!;
        var result = await form.DuplicateWidgetsAsync([widget]);
        var copy = Assert.Single(result.Widgets);
        Assert.NotEqual(widget, copy);
        var field = Assert.Single(result.Fields);
        Assert.NotEqual(original.FieldRef, field.FieldRef);
        Assert.Contains(field.Widgets, w => w.AnnotationRef == copy);
    }

    [Fact]
    public async Task DuplicatingAFieldTheFormDoesNotHaveIsNotFound()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => form.DuplicateAsync([new FormFieldRef.Fqn("no such field")]));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
    }

    [Fact]
    public async Task ADuplicateOffsetThatIsNotFiniteIsInvalidInput()
    {
        var (viewer, _, form) = await Opened();
        await using var _v = viewer;
        var original = await WritableText(form);
        var before = (await form.GetFieldsAsync()).Count;
        var options = new FormDuplicateOptions { Offset = new PdfPoint(double.NegativeInfinity, 0) };
        var error = await Assert.ThrowsAsync<PdfException>(() => form.DuplicateAsync([original.FieldRef], options));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        var widget = original.Widgets.First(w => w.AnnotationRef is not null).AnnotationRef!;
        error = await Assert.ThrowsAsync<PdfException>(() => form.DuplicateWidgetsAsync([widget], options));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal(before, (await form.GetFieldsAsync()).Count);
    }
}
