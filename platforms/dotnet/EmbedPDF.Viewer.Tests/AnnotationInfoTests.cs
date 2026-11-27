using Xunit;
using GA = uniffi.epdf_plugin_annotation;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF.Viewer.Tests;

public sealed partial class AnnotationInfoTests
{
    static readonly PageRef Page = new(1);
    static readonly AnnotationRef Ref = new AnnotationRef.ObjectNumber(Page, 12);
    static readonly AnnotationRef Parent = new AnnotationRef.ObjectNumber(Page, 7);

    const string InkJson = """{"subtype":"ink","ref":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":1},"objectNumber":12},"rect":{"x":-1,"y":-1,"width":22,"height":7.000000000000001},"color":"#e5484d","opacity":0.5,"strokeWidth":2,"borderStyle":"dashed","dashArray":[3,1],"inkList":[[{"x":0,"y":0},{"x":10,"y":5}],[{"x":20,"y":0}]],"intent":"ink-highlight","rotation":30,"customMember":{"kept":true}}""";

    internal static GA.AnnotationRecord Record(
        string subtype = "ink", string? createdAt = "2025-03-04T05:06:07Z", string? modifiedAt = "2025-03-04T10:36:07+05:30", string fieldsJson = InkJson) =>
        new(Ffi.ToFfi(Ref), Ffi.ToFfi(Page), subtype, new GF.Rect(-1, -1, 22, 7.000000000000001), "Ink", "Reviewer", createdAt, modifiedAt, "group-1",
            Ffi.ToFfi(Parent), true, false, fieldsJson);

    static DateTimeOffset? Instant(string? text) => new AnnotationInfo(Record(createdAt: text)).CreatedAt;

    static readonly DateTimeOffset Expected = DateTimeOffset.FromUnixTimeSeconds(1_741_064_767);

    [Fact]
    public void EveryMemberMapsFromTheGeneratedRecord()
    {
        var info = new AnnotationInfo(Record());
        Assert.Equal(Ref, info.AnnotationRef);
        Assert.Equal(Page, info.Page);
        Assert.Equal(AnnotationSubtype.Ink, info.Subtype);
        Assert.Equal(new PdfRect(-1, -1, 22, 7.000000000000001), info.Rect);
        Assert.Equal("Ink", info.Contents);
        Assert.Equal("Reviewer", info.Author);
        Assert.Equal(Expected, info.CreatedAt);
        Assert.Equal("2025-03-04T05:06:07Z", info.RawCreatedAt);
        Assert.Equal(Expected, info.ModifiedAt);
        Assert.Equal("2025-03-04T10:36:07+05:30", info.RawModifiedAt);
        Assert.Equal("group-1", info.GroupId);
        Assert.Equal(Parent, info.InReplyTo);
        Assert.True(info.CanUpdate);
        Assert.False(info.CanDelete);
        Assert.Equal(InkJson, info.FieldsJson);
        Assert.Null(info.Highlight);
        Assert.Null(info.Note);
        var ink = Assert.IsType<AnnotationInkDetails>(info.Ink);
        Assert.Equal(AnnotationColor.Parse("#e5484d"), ink.Color);
        Assert.Equal(0.5, ink.Opacity);
        Assert.Equal(2, ink.StrokeWidth);
        Assert.Equal(AnnotationBorderStyleName.Dashed, ink.BorderStyle);
        Assert.Equal([3.0, 1.0], ink.DashArray!);
        Assert.Equal([new PdfPoint(0, 0), new PdfPoint(10, 5)], ink.InkList[0]);
        Assert.Equal([new PdfPoint(20, 0)], ink.InkList[1]);
        Assert.Equal(AnnotationIntent.InkHighlight, ink.Intent);
        Assert.Equal(30, ink.Rotation);
    }

    [Fact]
    public void InkWithoutAColorIsStillInk()
    {
        const string json = """{"subtype":"ink","color":null,"opacity":1,"strokeWidth":1,"borderStyle":"solid","dashArray":null,"inkList":[[{"x":1,"y":2}]],"intent":null,"rotation":null}""";
        var record = Record(fieldsJson: json);
        var info = new AnnotationInfo(record);
        var ink = Assert.IsType<AnnotationInkDetails>(info.Ink);
        Assert.Null(ink.Color);
        Assert.Null(ink.DashArray);
        Assert.Null(ink.Intent);
        Assert.Null(ink.Rotation);
        Assert.Equal([new PdfPoint(1, 2)], ink.InkList.Single());
        Assert.Equal(record, info.ToRecord());
    }

    [Fact]
    public void AHighlightsQuadsAreTyped()
    {
        const string json = """{"subtype":"highlight","color":"#ffcd45","opacity":1,"quadPoints":[{"upperLeft":{"x":10,"y":10},"upperRight":{"x":110,"y":10},"lowerLeft":{"x":10,"y":22},"lowerRight":{"x":110,"y":22}}]}""";
        var info = new AnnotationInfo(Record(subtype: "highlight", fieldsJson: json));
        Assert.Equal(AnnotationColor.Parse("#ffcd45"), info.Highlight?.Color);
        Assert.Equal(1, info.Highlight?.Opacity);
        Assert.Equal([new PdfQuad(new(10, 10), new(110, 10), new(10, 22), new(110, 22))], info.Highlight!.QuadPoints);
        Assert.Null(info.Ink);
    }

    [Fact]
    public void ATextAnnotationIsANote()
    {
        const string json = """{"subtype":"text","color":"#e5484d","opacity":1,"icon":"key","open":true,"state":"accepted","stateModel":"review"}""";
        var info = new AnnotationInfo(Record(subtype: "text", fieldsJson: json));
        Assert.Equal(AnnotationSubtype.Text, info.Subtype);
        Assert.Equal(new AnnotationNoteDetails(AnnotationColor.Parse("#e5484d"), 1, "key", true, "accepted", "review", null), info.Note);
    }

    [Fact]
    public void MembersOfAnotherShapeAreLeftInFieldsJson()
    {
        const string json = """{"subtype":"ink","color":"#e5484d","opacity":1,"strokeWidth":2,"borderStyle":"solid","inkList":"none"}""";
        var info = new AnnotationInfo(Record(fieldsJson: json));
        Assert.Equal(AnnotationSubtype.Ink, info.Subtype);
        Assert.Null(info.Details);
        Assert.Equal(json, info.FieldsJson);
    }

    [Fact]
    public void AnIntentOrBorderStyleWithoutAConstantKeepsItsText()
    {
        const string json = """{"subtype":"ink","color":null,"opacity":1,"strokeWidth":1,"borderStyle":"engraved","dashArray":null,"inkList":[[{"x":1,"y":2}]],"intent":"ink-signature","rotation":null}""";
        var ink = new AnnotationInfo(Record(fieldsJson: json)).Ink!;
        Assert.Equal(new AnnotationIntent("ink-signature"), ink.Intent);
        Assert.Equal("ink-signature", ink.Intent?.Value);
        Assert.Equal(new AnnotationBorderStyleName("engraved"), ink.BorderStyle);
        Assert.Equal("engraved", ink.BorderStyle.ToString());
    }

    [Fact]
    public void TheConstantsAreTheWriteSideSpellings()
    {
        AnnotationIntent[] intents =
        [
            AnnotationIntent.InkHighlight, AnnotationIntent.StrikeoutTextEdit, AnnotationIntent.LineArrow, AnnotationIntent.LineDimension,
            AnnotationIntent.PolygonCloud, AnnotationIntent.PolygonDimension, AnnotationIntent.PolylineDimension, AnnotationIntent.FreeText,
            AnnotationIntent.FreeTextCallout, AnnotationIntent.Replace,
        ];
        static IEnumerable<string> Names<T>() where T : struct, Enum => Enum.GetValues<T>().Select(AnnotationVocabulary.WireName);
        Assert.Equal(
            [.. Names<AnnotationInkIntent>(), .. Names<AnnotationStrikeoutIntent>(), .. Names<AnnotationLineIntent>(), .. Names<AnnotationPolygonIntent>(),
             .. Names<AnnotationPolylineIntent>(), .. Names<AnnotationFreeTextIntent>(), .. Names<AnnotationCaretIntent>()],
            intents.Select(i => i.Value));
        Assert.Equal("free-text-typewriter", AnnotationIntent.FreeTextTypewriter.Value);
        AnnotationBorderStyleName[] styles = [AnnotationBorderStyleName.Solid, AnnotationBorderStyleName.Dashed, AnnotationBorderStyleName.Beveled, AnnotationBorderStyleName.Inset];
        Assert.Equal(["solid", "dashed", "beveled", "inset"], styles.Select(s => s.Value));
        Assert.Equal(Names<AnnotationBorderStyle>(), styles.Take(2).Select(s => s.Value));
    }

    [Fact]
    public void TheTextSpellingsCompare()
    {
        var ink = new AnnotationInfo(Record()).Ink!;
        Assert.True(ink.BorderStyle == "dashed");
        Assert.True(ink.Intent == "ink-highlight");
        Assert.Equal("dashed", $"{ink.BorderStyle}");
    }

    [Fact]
    public void AnUnknownSubtypeKeepsItsName()
    {
        const string json = """{"subtype":"sparkle","glow":3}""";
        var info = new AnnotationInfo(Record(subtype: "sparkle", fieldsJson: json));
        Assert.Equal("sparkle", info.Subtype.Value);
        Assert.Null(info.Details);
        Assert.Equal(Record(subtype: "sparkle", fieldsJson: json), info.ToRecord());
    }

    [Fact]
    public void TheRoundTripLosesNothing()
    {
        GA.AnnotationRecord[] records =
        [
            Record(),
            Record(createdAt: null, modifiedAt: null),
            Record(createdAt: "not a date", modifiedAt: "2025-02-29T00:00:00Z"),
            Record(subtype: "sparkle", fieldsJson: "{}"),
            new(Ffi.ToFfi(Ref), Ffi.ToFfi(Page), "", new GF.Rect(0, 0, 0, 0), null, null, null, null, null, null, false, true, ""),
        ];
        foreach (var record in records)
        {
            var info = new AnnotationInfo(record);
            Assert.Equal(record, info.ToRecord());
            Assert.Equal(info.ToRecord(), new AnnotationInfo(info.ToRecord()).ToRecord());
        }
    }

    [Fact]
    public void TheEnginesTimestampFormsRead()
    {
        Assert.Equal(Expected, Instant("2025-03-04T05:06:07Z"));
        Assert.Equal(Expected, Instant("2025-03-04T10:36:07+05:30"));
        Assert.Equal(Expected, Instant("2025-03-04T00:06:07-05:00"));
        Assert.Equal(Expected, Instant("2025-03-04T05:06:07"));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_741_064_767_250), Instant("2025-03-04T05:06:07.25Z"));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_741_064_760), Instant("2025-03-04T05:06Z"));
        Assert.Equal(Expected, Instant("2025-03-04T10:36:07+0530"));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_709_251_140), Instant("2024-02-29T23:59:00Z"));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(-3600), Instant("1970-01-01T00:00:00+01:00"));
    }

    [Theory]
    [InlineData("2025-02-29T00:00:00Z")]
    [InlineData("2025-03-04T24:00:00Z")]
    [InlineData("2025-13-01T00:00:00Z")]
    [InlineData("2025-03-04T05:06:07+24:00")]
    [InlineData("D:20250304050607Z")]
    [InlineData("2025-03-04T05:06:07z")]
    [InlineData("2025-03-04T05:06:07+5:30")]
    [InlineData("2025-03-04T05:06:07Z ")]
    [InlineData("2025-03-04")]
    [InlineData("yesterday")]
    [InlineData("")]
    public void AMalformedTimestampKeepsItsText(string text)
    {
        var info = new AnnotationInfo(Record(createdAt: text, modifiedAt: text));
        Assert.Null(info.CreatedAt);
        Assert.Equal(text, info.RawCreatedAt);
        Assert.Null(info.ModifiedAt);
        Assert.Equal(text, info.RawModifiedAt);
    }

    /// <summary>
    /// Dates the engine can write that fall outside what DateTimeOffset holds
    /// (it writes year 0000) read as no instant and keep their text, rather
    /// than throwing out of the read.
    /// </summary>
    [Theory]
    [InlineData("0000-01-01T00:00+01:00")]
    [InlineData("0001-01-01T00:00+00:01")]
    [InlineData("9999-12-31T23:59-01:00")]
    public void ATimestampOutsideTheRangeKeepsItsText(string text)
    {
        var info = new AnnotationInfo(Record(createdAt: text, modifiedAt: text));
        Assert.Null(info.CreatedAt);
        Assert.Equal(text, info.RawCreatedAt);
        Assert.Null(info.ModifiedAt);
        Assert.Equal(text, info.RawModifiedAt);
        var file = new AnnotationAttachedFile("x", null, null, null, null, text, text);
        Assert.Null(file.CreatedAt);
        Assert.Null(file.ModifiedAt);
    }

    [Fact]
    public async Task AListReadSurvivesAnAnnotationDatedOutsideTheRange()
    {
        var (viewer, document, annotations) = await AnnotationTests.Opened("report");
        await using var _v = viewer;
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        _ = await annotations.CreateAsync(new SquareAnnotationDraft(new PdfRect(10, 10, 20, 20)), page);
        // A bundle carries the dates as the engine wrote them; give one year 0000.
        var bundle = System.Text.RegularExpressions.Regex.Replace(
            await annotations.ExportAsync(), "\"(modifiedAt|createdAt)\":\"[^\"]*\"", "\"$1\":\"0000-01-01T00:00:00+01:00\"");
        Assert.Contains("0000-01-01", bundle, StringComparison.Ordinal);
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var other = await viewer.GetAnnotationAsync(second);
        await other.RefreshAsync();
        _ = await other.ImportAsync(bundle);
        var listed = Assert.Single(await other.ListAsync());
        Assert.Equal("0000-01-01T00:00:00+01:00", listed.RawModifiedAt ?? listed.RawCreatedAt);
        Assert.Null(listed.ModifiedAt);
    }

    [Fact]
    public void AnAbsentTimestampIsNull()
    {
        var info = new AnnotationInfo(Record(createdAt: null, modifiedAt: null));
        Assert.Null(info.CreatedAt);
        Assert.Null(info.RawCreatedAt);
        Assert.Null(info.ModifiedAt);
        Assert.Null(info.RawModifiedAt);
    }
}
