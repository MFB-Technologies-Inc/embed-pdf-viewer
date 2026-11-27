using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The 2026-11-27 catch-up's form surface (CU-C8): a field's purpose and
/// format, the document's groups and the design group, a turned widget's
/// frame, a group's accent on a fill item and on a lent widget, the
/// selection's fields, a widget's anchor and focus, and a focus target that
/// takes no cursor.
/// </summary>
public sealed class FormGroupsTests
{
    static Task<PdfViewer> EditingViewer() =>
        PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new FormConfig() });

    static async Task<(PdfViewer Viewer, DocumentInfo Document, FormCapability Form, PageRef Page)> Opened()
    {
        var viewer = await EditingViewer();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var form = await viewer.GetFormAsync(document);
        await form.RefreshAsync();
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, form, page);
    }

    static FormCreateFieldInput Text(string name, PageRef page, string? groupId = null) =>
        new(FormFieldFamily.Text, name, page, new PdfRect(40, 60, 120, 24)) { GroupId = groupId };

    sealed class Target(bool takesCursor, bool takesFocus) : IFormFocusTarget
    {
        public int Focused;
        public bool TakesCursor() => takesCursor;
        public bool Focus(bool select)
        {
            Interlocked.Increment(ref Focused);
            return takesFocus;
        }
    }

    [Fact]
    public async Task PurposeFormatAndGroupAreWrittenReadAndSaved()
    {
        var (viewer, document, form, page) = await Opened();
        await using var _v = viewer;
        var field = await form.CreateAsync(Text("signed_on", page, "dana") with
        {
            Purpose = "date-signed",
            Format = new FormFieldFormat.Date("yyyy-mm-dd"),
        });
        Assert.Equal("date-signed", field.Purpose);
        Assert.Equal(new FormFieldFormat.Date("yyyy-mm-dd"), field.Format);
        Assert.Equal("dana", field.GroupId);
        var error = await Assert.ThrowsAsync<PdfException>(() =>
            form.UpdateAsync(field.FieldRef, new FormFieldPatch { Format = new FormFieldFormat.Other() }));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        var updated = await form.UpdateAsync(field.FieldRef, new FormFieldPatch
        {
            Format = new FormFieldFormat.Time("HH:MM"),
            Clear = [FormFieldPatchKey.Purpose],
        });
        Assert.Null(updated.Purpose);
        Assert.Equal(new FormFieldFormat.Time("HH:MM"), updated.Format);
        var saved = await viewer.SaveAsync(document);
        var reopened = await viewer.OpenAsync(saved);
        using var again = await viewer.GetFormAsync(reopened);
        await again.RefreshAsync();
        var found = await again.GetFieldAsync(field.FieldRef);
        Assert.Equal(new FormFieldFormat.Time("HH:MM"), found?.Format);
        Assert.Equal("dana", found?.GroupId);
        var moved = await again.MoveToGroupAsync([field.FieldRef], null);
        Assert.Null(Assert.Single(moved).GroupId);
    }

    [Fact]
    public async Task GroupsAreNamedAnnouncedAndShowOnTheirFields()
    {
        var (viewer, _, form, page) = await Opened();
        await using var _v = viewer;
        var field = await form.CreateAsync(Text("dana_name", page, "dana"));
        Assert.Equal([new FormGroup("dana", null, "#ea580c")], await form.GetGroupsAsync());
        var events = form.ObserveEvents();
        await form.SetGroupsAsync([new FormGroupInput("ari") { Label = "Ari Cole" }, new FormGroupInput("dana") { Color = "#0055aa" }]);
        IReadOnlyList<FormGroup> groups = [new("ari", "Ari Cole", "#ea580c"), new("dana", null, "#0055aa")];
        Assert.Equal(groups, await form.GetGroupsAsync());
        Assert.True(await form.CanSetGroupAsync("ari"));
        await form.SetDesignGroupAsync("ari");
        Assert.Equal("ari", await form.GetDesignGroupAsync());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var seen = new List<FormEvent>();
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is FormEvent.GroupsChanged or FormEvent.DesignGroupChanged) seen.Add(e);
            if (seen.Count == 2) break;
        }
        Assert.Equal(groups, Assert.IsType<FormEvent.GroupsChanged>(seen[0]).Groups);
        Assert.Equal("ari", Assert.IsType<FormEvent.DesignGroupChanged>(seen[1]).GroupId);
        var item = (await form.GetFillItemsAsync(page)).Single(i => i.FieldRef == field.FieldRef);
        Assert.Equal("dana", item.GroupId);
        Assert.True(item.CanFill);
        Assert.True(item.Empty);
        Assert.Equal(new RecordAccent("#0055aa", 0.16), item.Accent);
        await form.UpdateSettingsAsync(new FormSettingsPatch { Fields = new FormFieldSettingsPatch { Tint = 0 } });
        Assert.Equal(0, (await form.GetSettingsAsync()).Fields.Tint);
        Assert.Null((await form.GetFillItemsAsync(page)).Single(i => i.FieldRef == field.FieldRef).Accent);
    }

    [Fact]
    public async Task ATurnedWidgetFillsWithItsFrameAndAnchorsWhereItStands()
    {
        var (viewer, _, form, page) = await Opened();
        await using var _v = viewer;
        var field = await form.CreateAsync(Text("turned", page));
        var widget = field.Widgets[0].AnnotationRef!;
        await form.UpdateWidgetAsync(widget, """{"rotation":90}""");
        var item = (await form.GetFillItemsAsync(page)).Single(i => i.FieldRef == field.FieldRef);
        Assert.Equal(90, item.Rotation);
        Assert.Equal((120d, 24d), (item.Frame.Width, item.Frame.Height));
        Assert.Equal((24d, 120d), (item.Bounds.Width, item.Bounds.Height));
        var anchor = await form.GetWidgetAnchorAsync(widget);
        Assert.Equal(item.Bounds, anchor?.Bounds);
        var hit = await form.GetWidgetAtAsync(page, new PdfPoint(item.Bounds.X + 12, item.Bounds.Y + 60));
        Assert.Equal((90d, item.Frame), (hit!.Rotation, hit.Frame));
    }

    [Fact]
    public async Task ATargetThatTakesNoCursorIsPassedOverAndAFocusedOneSaysSo()
    {
        var (viewer, _, form, page) = await Opened();
        await using var _v = viewer;
        var field = await form.CreateAsync(Text("signer_only", page));
        var widget = field.Widgets[0].AnnotationRef!;
        var declining = new Target(takesCursor: false, takesFocus: true);
        using (await form.AttachFocusTargetAsync(widget, declining))
        {
            Assert.False(await form.FocusAsync(field.FieldRef));
            Assert.Equal(0, declining.Focused);
        }
        var taking = new Target(takesCursor: true, takesFocus: true);
        using var registration = await form.AttachFocusTargetAsync(widget, taking);
        Assert.True(await form.FocusAsync(field.FieldRef));
        await form.NotifyWidgetEventAsync(field.FieldRef, widget, FormWidgetEvent.Focus);
        Assert.True(await form.IsWidgetFocusedAsync(widget));
    }

    [Fact]
    public async Task ALentWidgetShowsItsAccentSelectsAsItsFieldAndTurns()
    {
        var (viewer, document, form, page) = await Opened();
        await using var _v = viewer;
        var field = await form.CreateAsync(Text("lent", page, "dana"));
        var widget = field.Widgets[0].AnnotationRef!;
        await form.SetGroupsAsync([new FormGroupInput("dana") { Color = "#0055aa" }]);
        using var interaction = await viewer.GetInteractionAsync(document);
        using var annotations = await viewer.GetAnnotationAsync(document);
        using var selection = annotations.GetSelection();
        await interaction.ActivateToolAsync(ToolId.FormEdit);
        await annotations.RefreshAsync();
        Assert.Equal(new RecordAccent("#0055aa", 0.16), (await annotations.GetAsync(widget))?.Accent);
        await selection.SelectAsync([widget]);
        Assert.Equal([field.FieldRef], (await form.GetSelectedFieldsAsync()).Select(f => f.FieldRef));
        Assert.Contains((await selection.GetPropsAsync()).Properties, p => p.Key == "orientation");
        await selection.RotateByAsync(90);
        Assert.Equal(90, (await form.GetFillItemsAsync(page)).Single(i => i.FieldRef == field.FieldRef).Rotation);
        await selection.ResetRotationAsync();
        Assert.Equal(0, (await form.GetFillItemsAsync(page)).Single(i => i.FieldRef == field.FieldRef).Rotation);
    }

    [Fact]
    public async Task ADragPreviewWithADisplayRotationIsAccepted()
    {
        var (viewer, document, _, page) = await Opened();
        await using var _v = viewer;
        using var annotations = await viewer.GetAnnotationAsync(document);
        using var drafts = annotations.GetDrafts();
        await drafts.PreviewPlacementAsync(new ToolId("free-text"), page, new PdfPoint(100, 100), new PdfPoint(300, 160), displayRotation: 90);
        await drafts.ClearPlacementPreviewAsync();
    }
}
