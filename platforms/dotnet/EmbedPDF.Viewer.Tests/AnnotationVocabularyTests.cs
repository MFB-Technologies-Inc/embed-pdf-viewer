using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class AnnotationColorTests
{
    [Fact]
    public void HexReadsEitherCaseAndWritesLowercase()
    {
        var color = AnnotationColor.Parse("#1F6fEb");
        Assert.Equal(new AnnotationColor(0x1f, 0x6f, 0xeb), color);
        Assert.Equal("#1f6feb", color.Hex);
        Assert.Equal("#000aff", new AnnotationColor(0, 10, 255).Hex);
        Assert.Equal("#e5484d", AnnotationColor.Parse("#E5484D").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("1f6feb")]
    [InlineData("#1f6fe")]
    [InlineData("#1f6feb0")]
    [InlineData("#1f6fez")]
    [InlineData("#ffffff ")]
    [InlineData(" #ffffff")]
    [InlineData("#ｆｆｆｆｆｆ")]
    public void AnythingButHashAndSixHexDigitsIsRefused(string text)
    {
        Assert.False(AnnotationColor.TryParse(text, out _));
        Assert.Throws<FormatException>(() => AnnotationColor.Parse(text));
    }

    [Fact]
    public void ANullableColourHoldsAColourOrNull()
    {
        JsonNullable<AnnotationColor> color = AnnotationColor.Parse("#abcdef");
        Assert.False(color.IsNull);
        Assert.Equal("#abcdef", color.Value.Hex);
        Assert.True(JsonNullable<AnnotationColor>.Null.IsNull);
        Assert.Throws<InvalidOperationException>(() => JsonNullable<AnnotationColor>.Null.Value);
        Assert.NotEqual(color, JsonNullable<AnnotationColor>.Null);
    }
}

public sealed class AnnotationVocabularyTests
{
    static string[] Names<T>() where T : struct, Enum => [.. Enum.GetValues<T>().Select(AnnotationVocabulary.WireName)];

    [Fact]
    public void TheVocabularyIsTheEnginesSpelling()
    {
        Assert.Equal(["comment", "key", "note", "help", "new-paragraph", "paragraph", "insert"], Names<AnnotationNoteIcon>());
        Assert.Equal(["solid", "dashed"], Names<AnnotationBorderStyle>());
        Assert.Equal(["ink-highlight"], Names<AnnotationInkIntent>());
        Assert.Equal(
            ["normal", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge", "color-burn", "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color", "luminosity"],
            Names<AnnotationBlendMode>());
        Assert.Equal(["none", "square", "circle", "diamond", "open-arrow", "closed-arrow", "butt", "r-open-arrow", "r-closed-arrow", "slash"], Names<AnnotationLineEnding>());
        Assert.Equal(["graph", "push-pin", "paperclip", "tag"], Names<AnnotationFileAttachmentIcon>());
        Assert.Equal(["NextPage", "PrevPage", "FirstPage", "LastPage"], Names<AnnotationLinkVerb>());
        Assert.Equal(["left", "center", "right", "justify"], Names<AnnotationTextAlignment>());
        Assert.Equal(["top", "middle", "bottom"], Names<AnnotationVerticalAlignment>());
        Assert.Equal(["contain", "cover", "fill"], Names<AnnotationStampFit>());
        Assert.Equal(["inline", "top"], Names<AnnotationCaptionPosition>());
        Assert.Equal(["strikeout-text-edit"], Names<AnnotationStrikeoutIntent>());
        Assert.Equal(["line-arrow", "line-dimension"], Names<AnnotationLineIntent>());
        Assert.Equal(["polygon-cloud", "polygon-dimension"], Names<AnnotationPolygonIntent>());
        Assert.Equal(["polyline-dimension"], Names<AnnotationPolylineIntent>());
        Assert.Equal(["free-text", "free-text-callout"], Names<AnnotationFreeTextIntent>());
        Assert.Equal(["replace"], Names<AnnotationCaretIntent>());
        Assert.Equal(["reply", "group"], Names<AnnotationReplyType>());
        Assert.Equal(["decimal", "fraction", "round", "truncate"], Names<MeasureFraction>());
        Assert.Equal(["suffix", "prefix"], Names<MeasureLabelPosition>());
    }

    [Fact]
    public void AnUndefinedValueHasNoSpelling() =>
        Assert.Throws<FieldBagException>(() => AnnotationVocabulary.WireName((AnnotationBlendMode)99));

    [Fact]
    public void ThePositionsAreTheEnginesJson()
    {
        var page = new PageRef(4);
        Assert.Equal("\"start\"", new AnnotationPosition.Start().Json);
        Assert.Equal("\"end\"", new AnnotationPosition.End().Json);
        Assert.Equal(
            """{"before":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":4},"objectNumber":9}}""",
            new AnnotationPosition.Before(new AnnotationRef.ObjectNumber(page, 9)).Json);
        Assert.Equal(
            """{"after":{"kind":"baseIndex","page":{"kind":"objectNumber","objectNumber":4},"baseIndex":2}}""",
            new AnnotationPosition.After(new AnnotationRef.Index(page, 2)).Json);
    }

    [Fact]
    public void ALinkTargetIsTaggedByKindAndReadsBack()
    {
        var page = new PageRef(7);
        AnnotationLinkTarget[] targets =
        [
            new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 10, null, 2)),
            new AnnotationLinkTarget.GoTo(new PageDestination.FitR(page, 1, 2, 3, 4)),
            new AnnotationLinkTarget.GoTo(new PageDestination.FitBH(page, null)),
            new AnnotationLinkTarget.Uri("https://example.test/typed"),
            new AnnotationLinkTarget.Named(AnnotationLinkVerb.PreviousPage),
        ];
        Assert.Equal(
            """{"destination":{"kind":"xyz","page":{"kind":"objectNumber","objectNumber":7},"x":10,"y":null,"zoom":2},"kind":"goto"}""",
            targets[0].ToJson());
        Assert.Equal("""{"kind":"named","name":"PrevPage"}""", targets[4].ToJson());
        foreach (var target in targets) Assert.Equal(target, AnnotationLinkTarget.FromEngineJson(target.ToJson()));
        var error = Assert.Throws<PdfException>(() => AnnotationLinkTarget.FromEngineJson("""{"kind":"launch","path":"x"}"""));
        Assert.Equal(PdfErrorCode.OperationFailed, error.Code);
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new AnnotationLinkTarget.Named((AnnotationLinkVerb)9).ToJson()).Code);
    }

    [Fact]
    public void PlainTextIsOneParagraphPerLine()
    {
        var text = AnnotationRichText.FromPlainText("One\r\nTwo\nThree");
        Assert.Equal(["One", "Two", "Three"], text.Paragraphs.Select(p => p.Runs.Single().Text));
        Assert.Equal("One\nTwo\nThree", text.PlainText);
    }

    [Fact]
    public void AMeasureEncodesEveryMember()
    {
        var scale = new PdfMeasurement("rectilinear")
        {
            Ratio = "1 in = 1 ft",
            X = [new PdfNumberFormat("ft") { Conversion = 0.125, Fraction = MeasureFraction.Decimal, Precision = 100, Fixed = true, Thousands = ",", Decimal = ".", PrefixSpacing = "", SuffixSpacing = " ", LabelPosition = MeasureLabelPosition.Suffix }],
            Y = [new PdfNumberFormat("ft") { Conversion = 0.25, Fraction = MeasureFraction.Round, Precision = 1 }],
            Distance = [new PdfNumberFormat("ft") { Conversion = 1, Fraction = MeasureFraction.Fraction, Precision = 16 }, new PdfNumberFormat("in") { Conversion = 12, Fraction = MeasureFraction.Truncate, Precision = 1, LabelPosition = MeasureLabelPosition.Prefix }],
            Area = [new PdfNumberFormat("sq ft") { Conversion = 1, Fraction = MeasureFraction.Decimal, Precision = 10 }],
            Angle = [new PdfNumberFormat("deg") { Conversion = 1, Fraction = MeasureFraction.Decimal, Precision = 1 }],
            Slope = [new PdfNumberFormat("%") { Conversion = 100, Fraction = MeasureFraction.Decimal, Precision = 1 }],
            Origin = new MeasureOrigin(3, 4),
            Cyx = 2,
        };
        const string json = """{"angle":[{"conversion":1,"fraction":"decimal","precision":1,"unit":"deg"}],"area":[{"conversion":1,"fraction":"decimal","precision":10,"unit":"sq ft"}],"cyx":2,"distance":[{"conversion":1,"fraction":"fraction","precision":16,"unit":"ft"},{"conversion":12,"fraction":"truncate","labelPosition":"prefix","precision":1,"unit":"in"}],"origin":{"x":3,"y":4},"ratio":"1 in = 1 ft","slope":[{"conversion":100,"fraction":"decimal","precision":1,"unit":"%"}],"subtype":"rectilinear","x":[{"conversion":0.125,"decimal":".","fixed":true,"fraction":"decimal","labelPosition":"suffix","precision":100,"prefixSpacing":"","suffixSpacing":" ","thousands":",","unit":"ft"}],"y":[{"conversion":0.25,"fraction":"round","precision":1,"unit":"ft"}]}""";
        Assert.Equal(json, FieldBag.Sorted(scale.ToNode("measure")));
        Assert.Equal(json, FieldBag.Sorted(PdfMeasurement.Read(System.Text.Json.Nodes.JsonNode.Parse(json)!, "measure").ToNode("measure")));
    }
}
