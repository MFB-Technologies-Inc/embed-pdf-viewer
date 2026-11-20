using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Shared values of the draft tests, as AnnotationDraftTests.swift and AnnotationKindDraftTests.swift declare them.</summary>
static class Drafts
{
    public static readonly IReadOnlyList<PdfPoint> Stroke = [new(100, 100), new(140, 120), new(180, 100)];
    public static readonly PdfQuad Quad = new(new(72, 300), new(200, 300), new(72, 312), new(200, 312));
    public static readonly PdfRect NoteRect = new(40, 40, 24, 24);
    public static readonly PdfRect Box = new(100, 120, 80, 40);

    public static AnnotationColor C(string hex) => AnnotationColor.Parse(hex);

    public static readonly InkAnnotationDraft FullInk = new([Stroke])
    {
        Color = C("#1f6feb"), Opacity = 0.75, StrokeWidth = 3, BorderStyle = AnnotationBorderStyle.Dashed,
        DashArray = JsonNullable.Of<IReadOnlyList<double>>([4, 2]), Intent = AnnotationInkIntent.InkHighlight, Rotation = 30,
        Contents = "Ink text", BlendMode = AnnotationBlendMode.Screen,
    };

    public static readonly HighlightAnnotationDraft FullHighlight = new([Quad])
    {
        Color = C("#ffcd45"), Opacity = 0.5, Contents = "Highlight text", BlendMode = AnnotationBlendMode.Multiply,
    };

    public static readonly NoteAnnotationDraft FullNote = new(NoteRect)
    {
        Color = C("#e5484d"), Opacity = 0.5, Icon = AnnotationNoteIcon.Key, Open = true, State = "accepted", StateModel = "review",
        Contents = "Typed note", BlendMode = AnnotationBlendMode.Darken,
    };

    public static readonly UnderlineAnnotationDraft FullUnderline = new([Quad]) { Color = C("#0000ff"), Opacity = 0.5, Contents = "Underline text", BlendMode = AnnotationBlendMode.Multiply };
    public static readonly SquigglyAnnotationDraft FullSquiggly = new([Quad]) { Color = C("#00ff00"), Opacity = 0.75, Contents = "Squiggly text", BlendMode = AnnotationBlendMode.Darken };
    public static readonly StrikeoutAnnotationDraft FullStrikeout = new([Quad])
    {
        Color = C("#ff0000"), Opacity = 0.25, Intent = AnnotationStrikeoutIntent.StrikeoutTextEdit, Contents = "Strikeout text", BlendMode = AnnotationBlendMode.Screen,
    };
    public static readonly RedactionAnnotationDraft FullRedaction = new()
    {
        QuadPoints = [Quad], Color = C("#112233"), Opacity = 0.5, InteriorColor = C("#000000"), OverlayText = "REDACTED", Repeat = true,
        FontFamily = "courier", FontSize = 9, FontColor = C("#ffffff"), TextAlign = AnnotationTextAlignment.Center, Contents = "Redaction text",
        BlendMode = AnnotationBlendMode.Normal,
    };
    public static readonly SquareAnnotationDraft FullSquare = new(Box)
    {
        Color = C("#ff0000"), Opacity = 0.5, StrokeWidth = 3, BorderStyle = AnnotationBorderStyle.Dashed, DashArray = JsonNullable.Of<IReadOnlyList<double>>([4, 2]),
        InteriorColor = C("#00ff00"), Rotation = 15, CloudyIntensity = 2, Contents = "Square text", BlendMode = AnnotationBlendMode.Multiply,
    };
    public static readonly CircleAnnotationDraft FullCircle = new(Box)
    {
        Color = C("#0000ff"), Opacity = 0.75, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Solid, InteriorColor = C("#ffff00"), Rotation = 30,
        Contents = "Circle text", BlendMode = AnnotationBlendMode.Overlay,
    };
    public static readonly LineAnnotationDraft FullLine = new(new(10, 10), new(100, 50))
    {
        Color = C("#ff00ff"), Opacity = 0.5, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Dashed, DashArray = JsonNullable.Of<IReadOnlyList<double>>([3, 1]),
        InteriorColor = C("#00ff00"), LineEndings = new(AnnotationLineEnding.OpenArrow, AnnotationLineEnding.ReversedClosedArrow),
        Intent = AnnotationLineIntent.LineArrow, CaptionEnabled = true, CaptionPosition = AnnotationCaptionPosition.Top,
        CaptionOffset = new AnnotationCaptionOffset(1, 2), Leader = new AnnotationLineLeader(5) { Extension = 1, Offset = 2 },
        Contents = "Line text", BlendMode = AnnotationBlendMode.HardLight,
    };
    public static readonly PolygonAnnotationDraft FullPolygon = new([new(10, 10), new(100, 10), new(50, 80)])
    {
        Color = C("#0000ff"), Opacity = 0.5, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Dashed, DashArray = JsonNullable.Of<IReadOnlyList<double>>([2, 2]),
        InteriorColor = C("#ffff00"), Intent = AnnotationPolygonIntent.PolygonCloud, CloudyIntensity = 2, CaptionEnabled = true,
        CaptionCenter = new AnnotationCaptionCenter.At(new(50, 30)), Contents = "Polygon text", BlendMode = AnnotationBlendMode.Lighten,
    };
    public static readonly PolylineAnnotationDraft FullPolyline = new([new(10, 10), new(60, 80), new(100, 10)])
    {
        Color = C("#00ffff"), Opacity = 0.75, StrokeWidth = 3, BorderStyle = AnnotationBorderStyle.Solid, InteriorColor = C("#ff0000"),
        LineEndings = new(AnnotationLineEnding.Circle, AnnotationLineEnding.Diamond), CaptionEnabled = true,
        CaptionCenter = new AnnotationCaptionCenter.At(new(60, 40)), Contents = "Polyline text", BlendMode = AnnotationBlendMode.Difference,
    };
    public static readonly FreeTextAnnotationDraft FullFreeText = new(Box)
    {
        Contents = "Free text", FontFamily = "times-roman", FontSize = 14, FontColor = C("#112233"), TextAlign = AnnotationTextAlignment.Center,
        VerticalAlign = AnnotationVerticalAlignment.Middle, Color = C("#445566"), InteriorColor = C("#ffffcc"), Opacity = 0.75, StrokeWidth = 2,
        BorderStyle = AnnotationBorderStyle.Dashed, DashArray = JsonNullable.Of<IReadOnlyList<double>>([2, 1]), CloudyIntensity = 1,
        Intent = AnnotationFreeTextIntent.FreeTextCallout,
        CalloutLine = new AnnotationCalloutLine.Kneed(new(20, 20), new(60, 60), new(100, 130)), LineEnding = AnnotationLineEnding.OpenArrow,
        BlendMode = AnnotationBlendMode.Multiply,
    };
    public static readonly CaretAnnotationDraft FullCaret = new(new(72, 300, 12, 12))
    {
        Color = C("#00ff00"), Opacity = 0.5, Intent = AnnotationCaretIntent.Replace, Contents = "Inserted", BlendMode = AnnotationBlendMode.Exclusion,
    };
    public static readonly LinkAnnotationDraft FullLink = new(Box, new AnnotationLinkTarget.Uri("https://example.test/typed")) { Contents = "Link text" };

    public static IReadOnlyList<AnnotationDraft> FullKindDrafts =>
        [FullUnderline, FullSquiggly, FullStrikeout, FullRedaction, FullSquare, FullCircle, FullLine, FullPolygon, FullPolyline, FullFreeText, FullCaret, FullLink];

    public static readonly PdfMeasurement Scale = new("rectilinear")
    {
        Ratio = "1 in = 1 ft",
        X = [new PdfNumberFormat("ft") { Conversion = 0.125, Fraction = MeasureFraction.Decimal, Precision = 100, Fixed = true, Thousands = ",", Decimal = ".", PrefixSpacing = "", SuffixSpacing = " ", LabelPosition = MeasureLabelPosition.Suffix }],
        Y = [new PdfNumberFormat("ft") { Conversion = 0.25, Fraction = MeasureFraction.Round, Precision = 1 }],
        Distance = [new PdfNumberFormat("ft") { Conversion = 1, Fraction = MeasureFraction.Fraction, Precision = 16 }, new PdfNumberFormat("in") { Conversion = 12, Fraction = MeasureFraction.Truncate, Precision = 1, LabelPosition = MeasureLabelPosition.Prefix }],
        Area = [new PdfNumberFormat("sq ft") { Conversion = 1, Fraction = MeasureFraction.Decimal, Precision = 10 }],
        Angle = [new PdfNumberFormat("deg") { Conversion = 1, Fraction = MeasureFraction.Decimal, Precision = 1 }],
        Slope = [new PdfNumberFormat("%") { Conversion = 100, Fraction = MeasureFraction.Decimal, Precision = 1 }],
        Cyx = 2,
    };

    public static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();
}

/// <summary>Exact encodings of the typed drafts and patches: the field bag sorts its keys, so a draft is always the same text.</summary>
public sealed class AnnotationDraftJsonTests
{
    [Fact]
    public void AnInkDraftEncodesEveryMember() => Assert.Equal(
        """{"blendMode":"screen","borderStyle":"dashed","color":"#1f6feb","contents":"Ink text","dashArray":[4,2],"inkList":[[{"x":100,"y":100},{"x":140,"y":120},{"x":180,"y":100}]],"intent":"ink-highlight","opacity":0.75,"rotation":30,"strokeWidth":3,"subtype":"ink"}""",
        Drafts.FullInk.ToJson());

    [Fact]
    public void ADraftLeavesUnsetMembersOut()
    {
        Assert.Equal("""{"inkList":[[{"x":1.5,"y":2}]],"subtype":"ink"}""", new InkAnnotationDraft([[new(1.5, 2)]]).ToJson());
        Assert.Equal(
            """{"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}],"subtype":"highlight"}""",
            new HighlightAnnotationDraft([Drafts.Quad]).ToJson());
        Assert.Equal("""{"rect":{"height":24,"width":24,"x":40,"y":40},"subtype":"text"}""", new NoteAnnotationDraft(Drafts.NoteRect).ToJson());
    }

    [Fact]
    public void AnInkDraftWritesNullForNothing() => Assert.Equal(
        """{"color":null,"contents":null,"dashArray":null,"inkList":[[{"x":0,"y":0}]],"intent":null,"rotation":null,"subtype":"ink"}""",
        new InkAnnotationDraft([[new(0, 0)]])
        {
            Color = JsonNullable<AnnotationColor>.Null, DashArray = JsonNullable<IReadOnlyList<double>>.Null, Intent = JsonNullable<AnnotationInkIntent>.Null,
            Rotation = JsonNullable<double>.Null, Contents = JsonNullable<string>.Null,
        }.ToJson());

    [Fact]
    public void AHighlightDraftEncodesEveryMember() => Assert.Equal(
        """{"blendMode":"multiply","color":"#ffcd45","contents":"Highlight text","opacity":0.5,"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}],"subtype":"highlight"}""",
        Drafts.FullHighlight.ToJson());

    [Fact]
    public void ANoteDraftEncodesEveryMember()
    {
        Assert.Equal(
            """{"blendMode":"darken","color":"#e5484d","contents":"Typed note","icon":"key","opacity":0.5,"open":true,"rect":{"height":24,"width":24,"x":40,"y":40},"state":"accepted","stateModel":"review","subtype":"text"}""",
            Drafts.FullNote.ToJson());
        Assert.Equal(
            """{"contents":null,"rect":{"height":24,"width":24,"x":40,"y":40},"state":null,"stateModel":null,"subtype":"text"}""",
            new NoteAnnotationDraft(Drafts.NoteRect) { State = JsonNullable<string>.Null, StateModel = JsonNullable<string>.Null, Contents = JsonNullable<string>.Null }.ToJson());
    }

    [Fact]
    public void ANullableColourTakesAColour()
    {
        Assert.Contains("\"color\":\"#abcdef\"", new InkAnnotationDraft([[new(0, 0)]]) { Color = AnnotationColor.Parse("#ABCDEF") }.ToJson(), StringComparison.Ordinal);
        Assert.Equal("""{"color":null}""", new InkAnnotationPatch { Color = JsonNullable<AnnotationColor>.Null }.ToJson());
    }

    [Fact]
    public void AnEmptyPatchIsAnEmptyObject()
    {
        AnnotationPatch[] patches =
        [
            new InkAnnotationPatch(), new HighlightAnnotationPatch(), new NoteAnnotationPatch(), new UnderlineAnnotationPatch(), new SquigglyAnnotationPatch(),
            new StrikeoutAnnotationPatch(), new RedactionAnnotationPatch(), new SquareAnnotationPatch(), new CircleAnnotationPatch(), new LineAnnotationPatch(),
            new PolygonAnnotationPatch(), new PolylineAnnotationPatch(), new FreeTextAnnotationPatch(), new CaretAnnotationPatch(), new LinkAnnotationPatch(),
            new StampAnnotationPatch(), new FileAttachmentAnnotationPatch(),
        ];
        foreach (var patch in patches) Assert.Equal("{}", patch.ToJson());
    }

    [Fact]
    public void AnInkPatchEncodesEveryMemberAndNoSubtype() => Assert.Equal(
        """{"blendMode":"normal","borderStyle":"solid","color":"#00ff00","contents":"Changed","dashArray":[1],"inkList":[[{"x":1,"y":2}],[{"x":3,"y":4}]],"intent":"ink-highlight","opacity":0.25,"rotation":90,"strokeWidth":5}""",
        new InkAnnotationPatch
        {
            InkList = [[new(1, 2)], [new(3, 4)]], Color = Drafts.C("#00ff00"), Opacity = 0.25, StrokeWidth = 5, BorderStyle = AnnotationBorderStyle.Solid,
            DashArray = JsonNullable.Of<IReadOnlyList<double>>([1]), Intent = AnnotationInkIntent.InkHighlight, Rotation = 90, Contents = "Changed",
            BlendMode = AnnotationBlendMode.Normal,
        }.ToJson());

    [Fact]
    public void APatchTellsClearingFromLeavingAlone()
    {
        Assert.Equal(
            """{"color":null,"contents":null,"dashArray":null,"intent":null,"rotation":null}""",
            new InkAnnotationPatch
            {
                Color = JsonNullable<AnnotationColor>.Null, DashArray = JsonNullable<IReadOnlyList<double>>.Null, Intent = JsonNullable<AnnotationInkIntent>.Null,
                Rotation = JsonNullable<double>.Null, Contents = JsonNullable<string>.Null,
            }.ToJson());
        Assert.Equal(
            """{"contents":null,"state":null,"stateModel":null}""",
            new NoteAnnotationPatch { State = JsonNullable<string>.Null, StateModel = JsonNullable<string>.Null, Contents = JsonNullable<string>.Null }.ToJson());
        Assert.Equal("""{"contents":null}""", new HighlightAnnotationPatch { Contents = JsonNullable<string>.Null }.ToJson());
    }

    [Fact]
    public void AHighlightPatchEncodesEveryMember() => Assert.Equal(
        """{"blendMode":"normal","color":"#00ff00","contents":"Changed","opacity":1,"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}]}""",
        new HighlightAnnotationPatch { QuadPoints = [Drafts.Quad], Color = Drafts.C("#00ff00"), Opacity = 1, Contents = "Changed", BlendMode = AnnotationBlendMode.Normal }.ToJson());

    [Fact]
    public void ANotePatchEncodesEveryMember() => Assert.Equal(
        """{"color":"#00ff00","contents":"Changed","icon":"new-paragraph","opacity":0.5,"open":false,"rect":{"height":4,"width":3,"x":1,"y":2},"state":"rejected","stateModel":"review"}""",
        new NoteAnnotationPatch
        {
            Rect = new PdfRect(1, 2, 3, 4), Color = Drafts.C("#00ff00"), Opacity = 0.5, Icon = AnnotationNoteIcon.NewParagraph, Open = false, State = "rejected",
            StateModel = "review", Contents = "Changed",
        }.ToJson());

    [Fact]
    public void AStringIsWrittenAsGiven() =>
        Assert.Equal("""{"contents":"a/b \"c\" é"}""", new HighlightAnnotationPatch { Contents = "a/b \"c\" é" }.ToJson());

    [Fact]
    public void ANumberJsonCantHoldIsInvalidInput()
    {
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new InkAnnotationPatch { Opacity = double.NaN }.ToJson()).Code);
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new InkAnnotationDraft([[new(double.PositiveInfinity, 0)]]).ToJson()).Code);
        // An enum value outside its cases never reaches the engine either.
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new InkAnnotationDraft([[new(0, 0)]]) { BlendMode = (AnnotationBlendMode)99 }.ToJson()).Code);
    }

    [Fact]
    public void AStringWithAnUnpairedSurrogateIsInvalidInput()
    {
        var error = Assert.Throws<PdfException>(() => new HighlightAnnotationPatch { Contents = "a\uD800" }.ToJson());
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal("EncodingError", error.Name);
        Assert.Contains("contents", new HighlightAnnotationPatch { Contents = "\uD83D\uDE00" }.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineDraftEncodesItsStructuredMembers()
    {
        Assert.Equal(
            """{"blendMode":"hard-light","borderStyle":"dashed","captionEnabled":true,"captionOffset":{"along":1,"perpendicular":2},"captionPosition":"top","color":"#ff00ff","contents":"Line text","dashArray":[3,1],"intent":"line-arrow","interiorColor":"#00ff00","leader":{"extension":1,"length":5,"offset":2},"lineEndings":{"end":"r-closed-arrow","start":"open-arrow"},"linePoints":{"end":{"x":100,"y":50},"start":{"x":10,"y":10}},"opacity":0.5,"strokeWidth":2,"subtype":"line"}""",
            Drafts.FullLine.ToJson());
        Assert.Equal(
            """{"leader":null,"linePoints":{"end":{"x":1,"y":1},"start":{"x":0,"y":0}},"subtype":"line"}""",
            new LineAnnotationDraft(new(0, 0), new(1, 1)) { Leader = JsonNullable<AnnotationLineLeader>.Null }.ToJson());
    }

    [Fact]
    public void ALinePatchMovesBothEndsOrNeither()
    {
        Assert.Equal("""{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}""", new LineAnnotationPatch { Start = new(1, 2), End = new(3, 4) }.ToJson());
        Assert.Equal(PdfErrorCode.InvalidInput, Assert.Throws<PdfException>(() => new LineAnnotationPatch { Start = new(1, 2) }.ToJson()).Code);
    }

    [Fact]
    public void ACaptionCenterIsAPointOrNull()
    {
        Assert.Equal("""{"captionCenter":null}""", new PolygonAnnotationPatch { CaptionCenter = new AnnotationCaptionCenter.Automatic() }.ToJson());
        Assert.Equal(
            """{"captionCenter":{"x":2,"y":3},"subtype":"polygon","vertices":[{"x":0,"y":0},{"x":1,"y":0},{"x":0,"y":1}]}""",
            new PolygonAnnotationDraft([new(0, 0), new(1, 0), new(0, 1)]) { CaptionCenter = new AnnotationCaptionCenter.At(new(2, 3)) }.ToJson());
    }

    [Fact]
    public void ACalloutLineIsTwoOrThreePoints()
    {
        Assert.Equal(
            """{"calloutLine":[{"x":1,"y":2},{"x":3,"y":4}]}""",
            new FreeTextAnnotationPatch { CalloutLine = new AnnotationCalloutLine.Straight(new(1, 2), new(3, 4)) }.ToJson());
        Assert.Equal(
            """{"calloutLine":null,"lineEnding":null}""",
            new FreeTextAnnotationPatch { CalloutLine = JsonNullable<AnnotationCalloutLine>.Null, LineEnding = JsonNullable<AnnotationLineEnding>.Null }.ToJson());
    }

    [Fact]
    public void ALinkTargetIsTaggedByKind()
    {
        var page = new PageRef(7);
        Assert.Equal(
            """{"target":{"destination":{"kind":"xyz","page":{"kind":"objectNumber","objectNumber":7},"x":10,"y":null,"zoom":2},"kind":"goto"}}""",
            new LinkAnnotationPatch { Target = new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(page, 10, null, 2)) }.ToJson());
        Assert.Equal("""{"target":{"kind":"named","name":"PrevPage"}}""", new LinkAnnotationPatch { Target = new AnnotationLinkTarget.Named(AnnotationLinkVerb.PreviousPage) }.ToJson());
        Assert.Equal(
            """{"target":{"destination":{"height":4,"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"width":3,"x":1,"y":2},"kind":"goto"}}""",
            new LinkAnnotationPatch { Target = new AnnotationLinkTarget.GoTo(new PageDestination.FitR(page, 1, 2, 3, 4)) }.ToJson());
        Assert.Equal("""{"target":null}""", new LinkAnnotationPatch { Target = JsonNullable<AnnotationLinkTarget>.Null }.ToJson());
    }

    [Fact]
    public void EachDraftNamesItsSubtype() => Assert.Equal(
        ["underline", "squiggly", "strikeout", "redact", "square", "circle", "line", "polygon", "polyline", "free-text", "caret", "link"],
        Drafts.FullKindDrafts.Select(d => (string)Drafts.Object(d.ToJson())["subtype"]!));

    static AnnotationCommonFields FullCommon => new()
    {
        Nm = "note-1", Subject = JsonNullable<string>.Null, GroupId = "reviewers",
        Reply = new AnnotationReply(new AnnotationRef.ObjectNumber(new PageRef(2), 9)) { Type = AnnotationReplyType.Group },
        Invisible = false, Hidden = true, Print = false, NoZoom = true, NoRotate = true, NoView = false, ReadOnly = true, Locked = true,
        ToggleNoView = false, LockedContents = true,
    };

    [Fact]
    public void CommonFieldsEncodeFlatBesideTheKinds()
    {
        Assert.Equal(
            """{"groupId":"reviewers","hidden":true,"icon":"key","invisible":false,"locked":true,"lockedContents":true,"nm":"note-1","noRotate":true,"noView":false,"noZoom":true,"print":false,"readOnly":true,"rect":{"height":24,"width":24,"x":40,"y":40},"reply":{"to":{"kind":"objectNumber","objectNumber":9,"page":{"kind":"objectNumber","objectNumber":2}},"type":"group"},"subject":null,"subtype":"text","toggleNoView":false}""",
            new NoteAnnotationDraft(Drafts.NoteRect) { Icon = AnnotationNoteIcon.Key, Common = FullCommon }.ToJson());
        // A patch writes the same members but nm: a name can't change after create.
        Assert.Equal(
            """{"groupId":"reviewers","hidden":true,"icon":"key","invisible":false,"locked":true,"lockedContents":true,"noRotate":true,"noView":false,"noZoom":true,"print":false,"readOnly":true,"reply":{"to":{"kind":"objectNumber","objectNumber":9,"page":{"kind":"objectNumber","objectNumber":2}},"type":"group"},"subject":null,"toggleNoView":false}""",
            new NoteAnnotationPatch { Icon = AnnotationNoteIcon.Key, Common = FullCommon }.ToJson());
        Assert.Equal(
            """{"groupId":null,"reply":null}""",
            new SquareAnnotationPatch { Common = new() { Reply = JsonNullable<AnnotationReply>.Null, GroupId = JsonNullable<string>.Null } }.ToJson());
    }

    [Fact]
    public void CommonFieldsDecodeWhatTheyEncode()
    {
        var common = new AnnotationCommonFields
        {
            Nm = "note-1", Subject = JsonNullable<string>.Null, Reply = new AnnotationReply(new AnnotationRef.Index(new PageRef(2), 3)), Print = false,
        };
        var decoded = NoteAnnotationPatch.FromJson(new NoteAnnotationDraft(Drafts.NoteRect) { Common = common }.ToJson()).Common;
        Assert.Equal(common, decoded);
        Assert.Equal(new AnnotationCommonFields(), NoteAnnotationPatch.FromJson("{}").Common);
    }

    [Fact]
    public void RichTextEncodesItsOpenMapsFlat()
    {
        var richText = new AnnotationRichText(
        [
            new AnnotationRichTextParagraph([new AnnotationRichTextRun("Hi") { Style = new JsonObject { ["italic"] = true } }])
            {
                Properties = new JsonObject { ["align"] = "center", ["spacing"] = new JsonObject { ["before"] = 2 } },
            },
        ])
        {
            Body = new JsonObject { ["size"] = 12, ["decoration"] = new JsonArray("underline"), ["margins"] = null },
        };
        const string json = """{"body":{"decoration":["underline"],"margins":null,"size":12},"paragraphs":[{"align":"center","runs":[{"style":{"italic":true},"text":"Hi"}],"spacing":{"before":2}}]}""";
        Assert.Equal("{\"richText\":" + json + "}", new FreeTextAnnotationPatch { RichText = richText }.ToJson());
        Assert.Equal("{\"richText\":" + json + "}", new FreeTextAnnotationPatch { RichText = FreeTextAnnotationPatch.FromJson("{\"richText\":" + json + "}").RichText }.ToJson());
        Assert.Equal("""{"richText":null}""", new SquareAnnotationPatch { RichText = JsonNullable<AnnotationRichText>.Null }.ToJson());
        // One value can go into several drafts: writing copies its nodes.
        Assert.Equal(new FreeTextAnnotationPatch { RichText = richText }.ToJson(), new FreeTextAnnotationPatch { RichText = richText }.ToJson());
    }

    [Fact]
    public void AMeasureEncodesEveryMember()
    {
        var scale = Drafts.Scale with { Origin = new MeasureOrigin(3, 4) };
        const string json = """{"angle":[{"conversion":1,"fraction":"decimal","precision":1,"unit":"deg"}],"area":[{"conversion":1,"fraction":"decimal","precision":10,"unit":"sq ft"}],"cyx":2,"distance":[{"conversion":1,"fraction":"fraction","precision":16,"unit":"ft"},{"conversion":12,"fraction":"truncate","labelPosition":"prefix","precision":1,"unit":"in"}],"origin":{"x":3,"y":4},"ratio":"1 in = 1 ft","slope":[{"conversion":100,"fraction":"decimal","precision":1,"unit":"%"}],"subtype":"rectilinear","x":[{"conversion":0.125,"decimal":".","fixed":true,"fraction":"decimal","labelPosition":"suffix","precision":100,"prefixSpacing":"","suffixSpacing":" ","thousands":",","unit":"ft"}],"y":[{"conversion":0.25,"fraction":"round","precision":1,"unit":"ft"}]}""";
        Assert.Equal("{\"measure\":" + json + "}", new PolygonAnnotationPatch { Measure = scale }.ToJson());
        Assert.Equal("""{"measure":null}""", new PolylineAnnotationPatch { Measure = JsonNullable<PdfMeasurement>.Null }.ToJson());
    }
}
