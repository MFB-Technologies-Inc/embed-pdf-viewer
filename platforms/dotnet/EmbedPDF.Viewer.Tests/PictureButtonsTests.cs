using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The 2026-11-27b catch-up's form surface (CU-C7): a field every signer
/// fills in (<see cref="FormField.ForAnyone"/>, #1163), and a push button's
/// picture and its widgets' layout (#1165), through the native engine.
/// </summary>
public sealed class PictureButtonsTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, FormCapability Form, PageRef Page)> Opened()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new FormConfig() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var form = await viewer.GetFormAsync(document);
        await form.RefreshAsync();
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, form, page);
    }

    static FormCreateFieldInput Field(FormFieldFamily family, string name, PageRef page, double y) =>
        new(family, name, page, new PdfRect(40, y, 90, 30));

    [Fact]
    public async Task ForAnyoneIsCreatedPatchedAndSaved()
    {
        var (viewer, document, form, page) = await Opened();
        await using var _v = viewer;
        var initials = await form.CreateAsync(Field(FormFieldFamily.Text, "initials_1", page, 60) with { ForAnyone = true });
        Assert.True(initials.ForAnyone);
        // A field in a group is not also for anyone; a signature field never is.
        var grouped = await Assert.ThrowsAsync<PdfException>(() =>
            form.CreateAsync(Field(FormFieldFamily.Text, "grouped", page, 100) with { ForAnyone = true, GroupId = "dana" }));
        Assert.Equal(PdfErrorCode.InvalidInput, grouped.Code);
        var signature = await Assert.ThrowsAsync<PdfException>(() =>
            form.CreateAsync(Field(FormFieldFamily.Signature, "sign_here", page, 140) with { ForAnyone = true }));
        Assert.Equal(PdfErrorCode.InvalidInput, signature.Code);
        var date = await form.CreateAsync(Field(FormFieldFamily.Text, "date_1", page, 180));
        Assert.False(date.ForAnyone);
        Assert.True((await form.UpdateAsync(date.FieldRef, new FormFieldPatch { ForAnyone = true })).ForAnyone);
        Assert.False((await form.UpdateAsync(initials.FieldRef, new FormFieldPatch { ForAnyone = false })).ForAnyone);
        var saved = await viewer.SaveAsync(document);
        var reopened = await viewer.OpenAsync(saved);
        using var again = await viewer.GetFormAsync(reopened);
        await again.RefreshAsync();
        Assert.True((await again.GetFieldAsync(date.FieldRef))?.ForAnyone);
        Assert.False((await again.GetFieldAsync(initials.FieldRef))?.ForAnyone);
    }

    [Fact]
    public async Task APushButtonsWidgetsKeepTheirLayoutAndAPictureGoesInAndOut()
    {
        var (viewer, document, form, page) = await Opened();
        await using var _v = viewer;
        var button = await form.CreateAsync(Field(FormFieldFamily.Pushbutton, "stamp", page, 60) with { Layout = FormWidgetLayout.Image });
        Assert.Equal(new FormFieldDetail.Pushbutton(false), button.Detail);
        var first = button.Widgets[0].AnnotationRef!;
        Assert.Equal(FormWidgetLayout.Image, (await form.GetWidgetAsync(first))?.Layout);
        // A layout goes on a push button only.
        var error = await Assert.ThrowsAsync<PdfException>(() =>
            form.CreateAsync(Field(FormFieldFamily.Text, "caption", page, 100) with { Layout = FormWidgetLayout.Image }));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        var added = await form.AddWidgetAsync(button.FieldRef, new FormWidgetPlacement
        {
            Page = page,
            Bounds = new PdfRect(140, 60, 90, 30),
            Layout = FormWidgetLayout.CaptionBelow,
        });
        var second = (await form.GetWidgetsAsync(button.FieldRef)).Single(w => w.AnnotationRef != first);
        Assert.Equal(FormWidgetLayout.CaptionBelow, second.Layout);
        Assert.Equal(2, added.Field.Widgets.Count);
        var filled = await form.SetButtonImageAsync(button.FieldRef, Fixture.Data("tiny.png"));
        Assert.Equal(new FormFieldDetail.Pushbutton(true), filled.Field.Detail);
        Assert.Equal(
            new FormWidgetLayout?[] { FormWidgetLayout.Image, FormWidgetLayout.CaptionBelow }.Order(),
            filled.Widgets.Select(w => w.Layout).Order());
        // The form hears the engine's update.
        Assert.Equal(new FormFieldDetail.Pushbutton(true), (await form.GetFieldAsync(button.FieldRef))?.Detail);
        var laidOut = await form.UpdateWidgetAsync(second.AnnotationRef, """{"layout":"caption-over"}""");
        Assert.Equal(FormWidgetLayout.CaptionOver, laidOut.Layout);
        var saved = await viewer.SaveAsync(document);
        var emptied = await form.SetButtonImageAsync(button.FieldRef, null);
        Assert.Equal(new FormFieldDetail.Pushbutton(false), emptied.Field.Detail);
        var reopened = await viewer.OpenAsync(saved);
        using var again = await viewer.GetFormAsync(reopened);
        await again.RefreshAsync();
        Assert.Equal(new FormFieldDetail.Pushbutton(true), (await again.GetFieldAsync(button.FieldRef))?.Detail);
        Assert.Equal(FormWidgetLayout.CaptionOver, (await again.GetWidgetAsync(second.AnnotationRef))?.Layout);
    }

    [Fact]
    public async Task APictureForAFieldOfAnotherKindIsRefused()
    {
        var (viewer, _, form, page) = await Opened();
        await using var _v = viewer;
        var text = await form.CreateAsync(Field(FormFieldFamily.Text, "name", page, 60));
        await Assert.ThrowsAsync<PdfException>(() => form.SetButtonImageAsync(text.FieldRef, Fixture.Data("tiny.png")));
        var missing = await Assert.ThrowsAsync<PdfException>(() => form.SetButtonImageAsync(new FormFieldRef.Fqn("nowhere"), null));
        Assert.Equal(PdfErrorCode.NotFound, missing.Code);
    }
}
