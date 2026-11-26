using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>EditingWorkflowsTests.swift</c>: workflows that cross plugins.</summary>
public sealed class EditingWorkflowsTests
{
    static Task<PdfViewer> EditingViewer() =>
        PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new FormConfig() });

    static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();

    static async Task ExpectCode(PdfErrorCode code, Func<Task> call) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<PdfException>(call)).Code);

    [Fact]
    public async Task CommandControlsRespectCategoriesAndReportPermanentRefusals()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Commands = true });
        using var commands = await viewer.GetCommandsAsync();
        var runs = 0;
        using var _r = await commands.RegisterAsync(new CommandDefinition("edit:note", "Note")
        {
            Categories = ["edit.annotations"],
            Handler = new CommandHandler(run: _ => { Interlocked.Increment(ref runs); return Task.CompletedTask; }),
        });
        await commands.DisableCommandAsync("edit:note");
        await commands.DisableCommandAsync("edit:note");
        Assert.Equal(["edit:note"], await commands.GetDisabledCommandsAsync());
        Assert.Equal(CommandRefusalReason.TurnedOff, (await commands.GetRefusalAsync("edit:note"))?.Reason);
        Assert.Equal(new CommandExecuteResult.Rejected(CommandRejectReason.Hidden), await commands.ExecuteAsync("edit:note"));
        Assert.Equal(0, runs);
        await commands.DisableCategoryAsync("edit");
        await commands.EnableCommandAsync("edit:note");
        Assert.True(await commands.IsCommandDisabledAsync("edit:note"));
        await commands.EnableCategoryAsync("edit");
        Assert.Equal(new CommandExecuteResult.Executed(), await commands.ExecuteAsync("edit:note"));
        Assert.Equal(1, Volatile.Read(ref runs));
        await commands.SetDisabledCommandsAsync(["", "missing"]);
        Assert.True(await commands.IsCommandDisabledAsync(""));
        Assert.Null(await commands.GetRefusalAsync("missing"));
        await commands.SetDisabledCommandsAsync([]);
        await viewer.DisposeAsync();
        await ExpectCode(PdfErrorCode.InstanceClosed, () => commands.GetDisabledCommandsAsync());
    }

    [Fact]
    public async Task NamedActionProbeReflectsPolicyAndClosedDocuments()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Actions = new ActionsConfig { OpenSequence = OpenSequence.Off } });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var actions = await viewer.GetActionsAsync(document);
        Assert.True(await actions.CanExecuteNamedAsync("NextPage"));
        await actions.UpdatePolicyAsync(new ActionPolicyPatch { Named = new PartialActionPolicyRow { User = ActionPolicyDecision.Block } });
        Assert.False(await actions.CanExecuteNamedAsync("NextPage"));
        await viewer.CloseAsync(document);
        await ExpectCode(PdfErrorCode.InstanceClosed, () => actions.CanExecuteNamedAsync("NextPage"));
    }

    [Fact]
    public async Task AnnotationBundlesPreserveOwnedResourcesAndTypedMappings()
    {
        await using var viewer = await EditingViewer();
        var first = await viewer.OpenAsync(Fixture.Data("report"));
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var source = await viewer.GetAnnotationAsync(first);
        using var target = await viewer.GetAnnotationAsync(second);
        using var drafts = source.GetDrafts();
        using var selection = source.GetSelection();
        using var threads = source.GetThreads();
        var page = (await viewer.GetPagesAsync(first))[0].Page;
        byte[] bytes = [0, 255, 128, 0, 239];
        var file = await drafts.CreateAttachmentAsync(new AnnotationAttachmentFile(bytes, "résumé.bin"), page, new PdfPoint(40, 40));
        var square = await source.CreateAsync("""{"subtype":"square","box":{"x":80,"y":40,"width":40,"height":30}}""", page);
        var bundle = await source.ExportAsync([file, square]);
        Assert.DoesNotContain("$host", bundle);
        var resources = Object(bundle)["resources"]!.AsObject();
        Assert.Contains(resources, r => r.Value?["$bytes"]?.GetValue<string>() is { } b && Convert.FromBase64String(b).SequenceEqual(bytes));
        var copied = await target.ImportAsync(bundle, """{"pages":"by-position","attribution":"stamp"}""");
        Assert.Equal(2, copied.Annotations.Count);
        Assert.Contains(copied.RefMap, m => m.From == file);
        foreach (var annotation in copied.Annotations)
            Assert.NotNull(await target.GetAsync(annotation.AnnotationRef));
        var copiedRefs = copied.Annotations.Select(a => a.AnnotationRef).ToList();
        var again = Object(await target.ExportAsync(copiedRefs));
        Assert.True(JsonNode.DeepEquals(resources, again["resources"]));
        await selection.SelectAsync([square]);
        var result = await selection.UpdateLinkAsync(new PdfLinkTarget.Uri("https://example.test/selected"));
        Assert.Empty(result.Failed);
        Assert.NotNull(await threads.GetLinkAsync(square));
        _ = await selection.UpdateLinkAsync(null);
        Assert.Null(await threads.GetLinkAsync(square));
        var saved = await viewer.OpenAsync(await viewer.SaveAsync(second));
        using var reopened = await viewer.GetAnnotationAsync(saved);
        var persisted = Object(await reopened.ExportAsync(copiedRefs));
        Assert.True(JsonNode.DeepEquals(resources, persisted["resources"]));
        await viewer.CloseAsync(first);
        await ExpectCode(PdfErrorCode.InstanceClosed, () => source.ExportAsync());
        Assert.NotNull(await target.GetAsync(copied.Annotations[0].AnnotationRef));
    }

    [Fact]
    public async Task IndependentWidgetEditsAndCalculationOrderPersist()
    {
        await using var viewer = await EditingViewer();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var form = await viewer.GetFormAsync(document);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var first = await form.CreateAsync(new FormCreateFieldInput(FormFieldFamily.Text, "first", page, new PdfRect(20, 30, 100, 24)));
        var second = await form.CreateAsync(new FormCreateFieldInput(FormFieldFamily.Text, "second", page, new PdfRect(20, 70, 100, 24)));
        var bundle = Object(await form.ExportAsync([first.FieldRef, second.FieldRef]));
        var fields = bundle["fields"]!.AsArray();
        var refs = new JsonArray();
        for (var index = 0; index < fields.Count; index++)
        {
            var data = fields[index]!["data"]!.AsObject();
            refs.Add(data["ref"]!.DeepClone());
            data["name"] = $"copy_{index}";
            data["actions"] = JsonNode.Parse("""
                {"calculate":{"root":{"type":"javascript","subtype":"JavaScript","script":"event.value = 1","next":[]},"incomplete":false,"warnings":[]}}
                """);
        }
        bundle["calculationOrder"] = refs;
        var imported = await form.ImportAsync(bundle.ToJsonString());
        Assert.Equal(2, imported.Fields.Count);
        var a = imported.Fields[0];
        var b = imported.Fields[1];
        var widget = a.Widgets[0].AnnotationRef!;
        using var interaction = await viewer.GetInteractionAsync(document);
        using var annotations = await viewer.GetAnnotationAsync(document);
        using var selection = annotations.GetSelection();
        await interaction.ActivateToolAsync(ToolId.FormEdit);
        await selection.SelectAsync([widget]);
        Assert.Equal(a.FieldRef, (await form.GetSelectedFieldAsync())?.FieldRef);
        await selection.ClearAsync();
        var updated = await form.UpdateWidgetAsync(widget, """{"rect":{"x":60,"y":90,"width":160,"height":28}}""");
        Assert.Equal(60, updated.Bounds.X);
        Assert.Equal([widget], (await form.GetWidgetsAsync(a.FieldRef)).Select(w => w.AnnotationRef));
        var extra = await form.AddWidgetAsync(a.FieldRef, new FormWidgetPlacement { Page = page, Bounds = new PdfRect(20, 150, 100, 24) });
        var added = extra.Field.Widgets[^1].AnnotationRef!;
        Assert.Equal(1, (await form.DeleteWidgetAsync(added))?.Widgets.Count);
        Assert.Null(await form.GetWidgetAsync(added));
        var order = await form.ReorderCalculationsAsync([b.FieldRef], new FormCalculationPosition.Before(a.FieldRef));
        Assert.Equal([b.FieldRef, a.FieldRef], order);
        var saved = await viewer.OpenAsync(await viewer.SaveAsync(document));
        using var reopened = await viewer.GetFormAsync(saved);
        await reopened.RefreshAsync();
        Assert.Equal(order, (await reopened.GetSnapshotAsync())?.CalculationOrder);
        Assert.Equal(60, (await reopened.GetWidgetAsync(widget))?.Bounds.X);
        await viewer.CloseAsync(document);
        await ExpectCode(PdfErrorCode.InstanceClosed, () => form.GetSelectedFieldAsync());
        await ExpectCode(PdfErrorCode.InstanceClosed, () => form.DeleteWidgetAsync(widget));
    }

    [Fact]
    public async Task DirectFormPayloadsAuthorOptionsScriptsAndPersistNullEdits()
    {
        await using var viewer = await EditingViewer();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var form = await viewer.GetFormAsync(document);
        await form.RefreshAsync();
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        const string action = """{"type":"javascript","script":"event.value = 7","next":[]}""";
        var field = await form.CreateAsync($$$"""
            {"kind":"combobox","name":"direct_native","edit":true,
             "alternateName":"Preserved","mappingName":"Clear","defaultValue":"a",
             "options":[{"label":"Alpha","value":"a"},{"label":"Beta","value":"b"}],
             "actions":{"calculate":{{{action}}}},
             "widgets":[{"page":{"kind":"objectNumber","objectNumber":{{{page.PageObjectNumber}}}},
                         "rect":{"x":20,"y":30,"width":120,"height":24}}]}
            """);
        var combo = Assert.IsType<FormFieldDetail.Combobox>(field.Detail);
        Assert.True(combo.Edit);
        Assert.Equal(["a", "b"], combo.Options.Select(o => o.Value));
        var updated = await form.UpdateAsync(field.FieldRef, $$$"""{"mappingName":null,"edit":false,"actions":{"validate":{{{action}}}}}""");
        Assert.Equal("Preserved", updated.AlternateName);
        Assert.Null(updated.MappingName);
        var panel = await form.GetFieldSettingsAsync(field.FieldRef);
        Assert.NotNull(panel);
        var choice = panel.Properties.First(p => p.Key == "defaultValue");
        Assert.Equal(FormSettingControl.Choice, choice.Control);
        Assert.False(choice.Free);
        Assert.Equal(["a", "b"], choice.Options!);
        Assert.False(Object(panel.ValuesJson)["edit"]!.GetValue<bool>());
        var added = await form.AddWidgetAsync(field.FieldRef, $$$"""
            {"rect":{"x":20,"y":70,"width":120,"height":24},"fontSize":19,"actions":{"focus":{{{action}}}}}
            """);
        Assert.Equal(19, Object(added.WidgetJson)["fontSize"]!.GetValue<double>());
        var count = (await form.GetFieldsAsync()).Count;
        foreach (var malformed in new[] { "{", "[]", "null", "42" })
        {
            await ExpectCode(PdfErrorCode.InvalidInput, () => form.CreateAsync(malformed));
            await ExpectCode(PdfErrorCode.InvalidInput, () => form.UpdateAsync(field.FieldRef, malformed));
            await ExpectCode(PdfErrorCode.InvalidInput, () => form.AddWidgetAsync(field.FieldRef, malformed));
        }
        Assert.Equal(count, (await form.GetFieldsAsync()).Count);
        var saved = await viewer.OpenAsync(await viewer.SaveAsync(document));
        using var reopened = await viewer.GetFormAsync(saved);
        await reopened.RefreshAsync();
        var found = await reopened.GetFieldAsync(field.FieldRef);
        Assert.NotNull(found);
        Assert.Equal("Preserved", found.AlternateName);
        Assert.Null(found.MappingName);
        var script = Object(found.FieldsJson)["actions"]?["validate"]?["root"]?["script"]?.GetValue<string>();
        Assert.Equal("event.value = 7", script);
        Assert.Equal(2, (await reopened.GetWidgetsAsync(found.FieldRef)).Count);
        await viewer.CloseAsync(document);
        await ExpectCode(PdfErrorCode.InstanceClosed, () => form.GetFieldSettingsAsync(field.FieldRef));
    }

    [Fact]
    public async Task DirectFormPayloadsRespectScriptPermission()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Scope = ["doc.open", "doc.forms.read", "doc.forms.modify"], Interaction = true, Form = new FormConfig(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var form = await viewer.GetFormAsync(document);
        const string actions = """{"calculate":{"type":"javascript","script":"event.value = 1"}}""";
        await ExpectCode(PdfErrorCode.PermissionDenied,
            () => form.CreateAsync($$$"""{"kind":"text","name":"denied","widgets":[],"actions":{{{actions}}}}"""));
        var missing = new FormFieldRef.Fqn("missing");
        await ExpectCode(PdfErrorCode.PermissionDenied, () => form.UpdateAsync(missing, $$$"""{"actions":{{{actions}}}}"""));
        await ExpectCode(PdfErrorCode.PermissionDenied, () => form.AddWidgetAsync(missing, $$$"""{"actions":{{{actions}}}}"""));
    }

    [Fact]
    public async Task OptionalEditingPluginsReportUnsupported()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions());
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        await ExpectCode(PdfErrorCode.Unsupported, () => viewer.GetAnnotationAsync(document));
        await ExpectCode(PdfErrorCode.Unsupported, () => viewer.GetFormAsync(document));
        await ExpectCode(PdfErrorCode.Unsupported, () => viewer.GetActionsAsync(document));
        await ExpectCode(PdfErrorCode.Unsupported, () => viewer.GetCommandsAsync());
    }
}
