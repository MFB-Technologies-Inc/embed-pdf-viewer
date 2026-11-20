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
