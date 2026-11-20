using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The annotation and link cases of TypedOverloadTests.swift: each typed
/// overload leaves the engine where its JSON twin does. The measurement
/// readout cases port with wave E, which brings the measurement capability;
/// the shell surface cases are in <see cref="ShellTests"/>.
/// </summary>
public sealed class TypedOverloadTests
{
    static readonly PdfRect Box = new(100, 120, 80, 40);
    const string UriJson = """{"kind":"uri","uri":"https://example.com"}""";

    static async Task<(PdfViewer Viewer, DocumentInfo Document, PageRef Page, AnnotationCapability Annotations)> Opened(string fixture = "report")
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Link = true, Annotation = "" });
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var annotations = await viewer.GetAnnotationAsync(document);
        await annotations.RefreshAsync();
        return (viewer, document, page, annotations);
    }

    [Fact]
    public async Task ASelectionTakesATypedPatch()
    {
        var (viewer, _, page, annotations) = await Opened("annotation_kinds");
        await using var _v = viewer;
        // The fixture has one highlight; the second is made here.
        var fixture = (await annotations.ListAsync()).First(info => info.Subtype == AnnotationSubtype.Highlight);
        var created = await annotations.CreateAsync(Drafts.FullHighlight, page);
        AnnotationRef[] highlights = [fixture.AnnotationRef, created];
        using var selection = annotations.GetSelection();
        await selection.SelectAsync(highlights);

        var typed = await selection.UpdateAsync(new HighlightAnnotationPatch { Opacity = 0.4 });
        Assert.Equal(highlights.ToHashSet(), typed.Applied.ToHashSet());
        Assert.Empty(typed.Failed);
        foreach (var highlight in highlights) Assert.Equal(0.4, (await annotations.GetAsync(highlight))?.Highlight?.Opacity);

        var json = await selection.UpdateAsync("""{"opacity":0.4}""");
        Assert.Equal(typed.Applied, json.Applied);
        Assert.Equal(typed.Failed, json.Failed);
        foreach (var highlight in highlights) Assert.Equal(0.4, (await annotations.GetAsync(highlight))?.Highlight?.Opacity);
    }

    [Fact]
    public async Task AnAttachedLinkIsSetAndReadTyped()
    {
        var (viewer, _, page, annotations) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(new SquareAnnotationDraft(Box), page);
        using var threads = annotations.GetThreads();
        Assert.Null(await threads.GetLinkTargetAsync(square));

        await threads.SetLinkAsync(square, new AnnotationLinkTarget.Uri("https://example.com"));
        Assert.Equal<AnnotationLinkTarget?>(new AnnotationLinkTarget.Uri("https://example.com"), await threads.GetLinkTargetAsync(square));
        var typed = await threads.GetLinkAsync(square);

        await threads.ClearLinkAsync(square);
        await threads.SetLinkAsync(square, UriJson);
        Assert.Equal(typed, await threads.GetLinkAsync(square));
    }

    [Fact]
    public async Task AnAttachedGotoKeepsItsCoordinates()
    {
        var (viewer, _, page, annotations) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(new SquareAnnotationDraft(Box), page);
        using var threads = annotations.GetThreads();
        var target = new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 30, 40, 2));
        await threads.SetLinkAsync(square, target);
        Assert.Equal<AnnotationLinkTarget?>(target, await threads.GetLinkTargetAsync(square));
    }

    [Fact]
    public async Task ADraftedGotoKeepsItsCoordinates()
    {
        var (viewer, _, page, annotations) = await Opened();
        await using var _v = viewer;
        var target = new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 30, 40, null));
        var annotationRef = await annotations.CreateAsync(new LinkAnnotationDraft(Box, target), page);
        Assert.Equal<AnnotationLinkTarget?>(target, (await annotations.GetAsync(annotationRef))?.Link?.Target);
        var area = new AnnotationLinkTarget.GoTo(new PageDestination.FitR(page, 10, 20, 100, 50));
        await annotations.UpdateAsync(annotationRef, new LinkAnnotationPatch { Target = area });
        Assert.Equal<AnnotationLinkTarget?>(area, (await annotations.GetAsync(annotationRef))?.Link?.Target);
    }

    /// <summary>
    /// <see cref="AnnotationThreadsCapability.GetLinkTargetAsync"/> reads with
    /// this, so a target the type can't hold is an error and never a different
    /// target. Swift throws a decoding error; C# throws operation-failed.
    /// </summary>
    [Theory]
    [InlineData("""{"kind":"launch","path":"notes.txt"}""")]
    [InlineData("""{"kind":"named","name":"Print"}""")]
    [InlineData("""{"kind":"goto","destination":{"kind":"spiral","page":{"kind":"objectNumber","objectNumber":7}}}""")]
    [InlineData("""{"kind":"goto","destination":{"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"x":1,"y":2}}""")]
    public void ATargetALinkTargetCannotHoldDoesNotDecode(string json) =>
        Assert.Equal(PdfErrorCode.OperationFailed, Assert.Throws<PdfException>(() => AnnotationLinkTarget.FromEngineJson(json)).Code);

    [Fact]
    public void ALinkTargetDecodesWhatItEncodes()
    {
        var page = new PageRef(7);
        AnnotationLinkTarget[] targets =
        [
            new AnnotationLinkTarget.Uri("https://example.com"), new AnnotationLinkTarget.Named(AnnotationLinkVerb.LastPage),
            new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 30, null, 2)), new AnnotationLinkTarget.GoTo(new PageDestination.Fit(page)),
            new AnnotationLinkTarget.GoTo(new PageDestination.FitH(page, 40)), new AnnotationLinkTarget.GoTo(new PageDestination.FitV(page, null)),
            new AnnotationLinkTarget.GoTo(new PageDestination.FitR(page, 1, 2, 3, 4)), new AnnotationLinkTarget.GoTo(new PageDestination.FitB(page)),
            new AnnotationLinkTarget.GoTo(new PageDestination.FitBH(page, null)), new AnnotationLinkTarget.GoTo(new PageDestination.FitBV(page, 5)),
        ];
        foreach (var target in targets) Assert.Equal(target, AnnotationLinkTarget.FromEngineJson(target.ToJson()));
    }

    [Fact]
    public async Task ALinkTargetResolvesActivatesAndLabelsTyped()
    {
        var (viewer, document, page, _) = await Opened();
        await using var _v = viewer;
        using var link = await viewer.GetLinkAsync(document);
        var uri = new AnnotationLinkTarget.Uri("https://example.com");

        var resolution = await link.ResolveAsync(uri);
        Assert.Equal(new LinkResolution.Uri("https://example.com"), resolution);
        Assert.Equal(resolution, await link.ResolveAsync(UriJson));

        var activation = await link.ActivateAsync(uri);
        Assert.Equal(new LinkActivation.Uri("https://example.com"), activation);
        Assert.Equal(activation, await link.ActivateAsync(UriJson));

        var label = await link.GetLabelAsync(uri);
        Assert.Equal(label, await link.GetLabelAsync(UriJson));
        Assert.Contains("https://example.com", label, StringComparison.Ordinal);

        var place = await link.ResolveAsync(new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 30, 40, null)));
        var destination = Drafts.Object(Assert.IsType<LinkResolution.Destination>(place).DestinationJson);
        Assert.Equal("xyz", (string?)destination["kind"]);
        Assert.Equal(30, (double?)destination["x"]);
        Assert.Equal(40, (double?)destination["y"]);
    }
}
