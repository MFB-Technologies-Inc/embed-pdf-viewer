using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Duplication, movability and remeasuring, against the engine.</summary>
public sealed class AnnotationDuplicateTests
{
    const string Square = """{"subtype": "square", "box": {"x": 72, "y": 72, "width": 100, "height": 50}, "contents": "Original"}""";

    static async Task<(PdfViewer Viewer, DocumentInfo Document, AnnotationCapability Annotations, PageRef Page)> Opened(bool history = false)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", History = history });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var annotations = await viewer.GetAnnotationAsync(document);
        await annotations.RefreshAsync();
        return (viewer, document, annotations, (await viewer.GetPagesAsync(document))[0].Page);
    }

    [Fact]
    public async Task DuplicateCopiesFieldsDrawingFileAndLinkWithNewRefsAsOneUndoStep()
    {
        var (viewer, document, annotations, page) = await Opened(history: true);
        await using var _v = viewer;
        using var drafts = annotations.GetDrafts();
        using var threads = annotations.GetThreads();
        var square = await annotations.CreateAsync(Square, page);
        await threads.SetLinkAsync(square, new AnnotationLinkTarget.Uri("https://example.com/a"));
        var ink = await annotations.CreateAsync(new InkAnnotationDraft([[new(100, 300), new(150, 320), new(200, 300)]]), page);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var file = await drafts.CreateAttachmentAsync(new AnnotationAttachmentFile(bytes, "data.bin"), page, new PdfPoint(300, 300));
        using var history = await viewer.GetHistoryAsync(document);
        var originals = new[] { square, ink, file };

        var result = await annotations.DuplicateAsync(originals, new AnnotationDuplicateOptions { Offset = new PdfPoint(20, 10) });

        Assert.Equal(3, result.Annotations.Count);
        Assert.Equal("[]", result.DroppedJson);
        var copies = result.Annotations.Select(a => a.AnnotationRef).ToArray();
        Assert.Empty(copies.Intersect(originals));
        Assert.Equal(3, copies.Distinct().Count());
        foreach (var (original, copy) in originals.Zip(result.Annotations))
            Assert.Equal((await annotations.GetAsync(original))!.Subtype, copy.Subtype);
        var squareCopy = result.Annotations[0];
        var squareRect = (await annotations.GetAsync(square))!.Rect;
        Assert.Equal("Original", squareCopy.Contents);
        Assert.Equal(squareRect.X + 20, squareCopy.Rect.X, 3);
        Assert.Equal(new AnnotationLinkTarget.Uri("https://example.com/a"), await threads.GetLinkTargetAsync(squareCopy.AnnotationRef));
        Assert.Equal(bytes, (await drafts.ReadAttachmentAsync(copies[2])).ToArray());

        await history.UndoAsync();
        foreach (var copy in copies) Assert.Null(await annotations.GetAsync(copy));
        foreach (var original in originals) Assert.NotNull(await annotations.GetAsync(original));
    }

    [Fact]
    public async Task DuplicatingNothingCopiesNothing()
    {
        var (viewer, _, annotations, _) = await Opened();
        await using var _v = viewer;
        var result = await annotations.DuplicateAsync([]);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public async Task DuplicatingAMissingAnnotationIsNotFound()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var before = (await annotations.ListAsync()).Count;
        var error = await Assert.ThrowsAsync<PdfException>(() => annotations.DuplicateAsync([new AnnotationRef.ObjectNumber(page, 99999)]));
        Assert.Equal(PdfErrorCode.NotFound, error.Code);
        Assert.Equal(before, (await annotations.ListAsync()).Count);
    }

    [Fact]
    public async Task ALockedAnnotationCannotBeDuplicatedByHand()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var locked = await annotations.CreateAsync(Square, page);
        await annotations.UpdateAsync(locked, """{"locked": true}""");
        Assert.False(await annotations.CanDuplicateAsync([locked]));
        using var selection = annotations.GetSelection();
        await selection.SelectAsync([locked]);
        Assert.False(await selection.CanDuplicateAsync());
    }

    [Fact]
    public async Task CanDuplicateTruthTable()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(Square, page);
        Assert.False(await annotations.CanDuplicateAsync([]));
        Assert.True(await annotations.CanDuplicateAsync([square]));
        Assert.False(await annotations.CanDuplicateAsync([square, new AnnotationRef.ObjectNumber(page, 99999)]));
        Assert.False(await annotations.CanDuplicateAsync([new AnnotationRef.ObjectNumber(page, 99999)]));
    }

    [Fact]
    public async Task ASessionThatMayNotCreateCannotDuplicate()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Scope = ["doc.open"], Interaction = true, Annotation = "" });
        var document = await viewer.OpenAsync(Fixture.Data("annotation_kinds"));
        using var annotations = await viewer.GetAnnotationAsync(document);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var any = new AnnotationRef.ObjectNumber(page, 1);
        Assert.False(await annotations.CanDuplicateAsync([any]));
        var error = await Assert.ThrowsAsync<PdfException>(() => annotations.DuplicateAsync([any]));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        using var selection = annotations.GetSelection();
        Assert.False(await selection.CanDuplicateAsync());
    }

    [Fact]
    public async Task SelectionDuplicateCopiesTheSelectionAndSelectsTheCopies()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(Square, page);
        using var selection = annotations.GetSelection();
        await selection.SelectAsync([square]);
        Assert.True(await selection.CanDuplicateAsync());
        var result = await selection.DuplicateAsync(new PdfPoint(0, 40));
        var copy = Assert.Single(result.Annotations);
        Assert.NotEqual(square, copy.AnnotationRef);
        Assert.Equal((await annotations.GetAsync(square))!.Rect.Y + 40, copy.Rect.Y, 3);
        Assert.Equal([copy.AnnotationRef], await selection.GetRefsAsync());
    }

    [Fact]
    public async Task SelectionDuplicateWithNothingSelectedIsEmpty()
    {
        var (viewer, _, annotations, _) = await Opened();
        await using var _v = viewer;
        using var selection = annotations.GetSelection();
        Assert.False(await selection.CanDuplicateAsync());
        Assert.Empty((await selection.DuplicateAsync()).Annotations);
    }

    [Fact]
    public async Task AHighlightIsNotMovableAndASquareIs()
    {
        var (viewer, document, annotations, page) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(Square, page);
        var quad = new PdfQuad(new(72, 100), new(172, 100), new(72, 120), new(172, 120));
        var highlight = await annotations.CreateAsync(new HighlightAnnotationDraft([quad]), page);
        Assert.True(await annotations.IsMovableAsync(square));
        Assert.False(await annotations.IsMovableAsync(highlight));
        Assert.False(await annotations.IsMovableAsync(new AnnotationRef.ObjectNumber(page, 99999)));
    }

    static PdfMeasurement FeetPerInch(double feet) => new("rectilinear")
    {
        Ratio = $"1 in = {feet} ft",
        X = [new PdfNumberFormat("ft") { Conversion = feet / 72, Precision = 100 }],
        Distance = [new PdfNumberFormat("ft") { Conversion = 1, Precision = 100 }],
        Area = [new PdfNumberFormat("sq ft") { Conversion = 1, Precision = 100 }],
    };

    [Fact]
    public async Task RemeasuringAPageRelabelsItsMeasurements()
    {
        var (viewer, document, measurement, page) = await MeasurementTests.Opened();
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        var json = await measurement.CreateMeasurementAsync(new CreateMeasurementInput(MeasurementKind.Distance, page, [new(10, 10), new(82, 10)]));
        var line = new AnnotationRef.ObjectNumber(page, System.Text.Json.Nodes.JsonNode.Parse(json)!["objectNumber"]!.GetValue<int>());
        using var annotations = await viewer.GetAnnotationAsync(document);
        var before = Assert.IsType<MeasurementReading.Value>(await measurement.ReadoutAsync(line)).Readout;

        await annotations.WhenRemeasurableAsync();
        var report = await annotations.StageRemeasureAsync(page, FeetPerInch(2));

        Assert.Null(report.Error);
        Assert.Equal(page, report.Page);
        Assert.Contains(line, report.Updated);
        var after = Assert.IsType<MeasurementReading.Value>(await measurement.ReadoutAsync(line)).Readout;
        Assert.NotEqual(before.Label, after.Label);
        Assert.Contains("ft", after.Label);
        Assert.Equal(2, after.Value, 3);
    }

    [Fact]
    public async Task ARemeasureScaleJsonCannotHoldIsInvalidInput()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() => annotations.StageRemeasureAsync(page, new PdfMeasurement("rectilinear") { Cyx = double.NaN }));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
    }

    [Fact]
    public async Task AnOffsetThatIsNotFiniteIsInvalidInput()
    {
        var (viewer, _, annotations, page) = await Opened();
        await using var _v = viewer;
        var square = await annotations.CreateAsync(Square, page);
        var before = (await annotations.ListAsync()).Count;
        var error = await Assert.ThrowsAsync<PdfException>(
            () => annotations.DuplicateAsync([square], new AnnotationDuplicateOptions { Offset = new PdfPoint(double.NaN, 0) }));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        using var selection = annotations.GetSelection();
        await selection.SelectAsync([square]);
        error = await Assert.ThrowsAsync<PdfException>(() => selection.DuplicateAsync(new PdfPoint(0, double.PositiveInfinity)));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal(before, (await annotations.ListAsync()).Count);
    }
}
