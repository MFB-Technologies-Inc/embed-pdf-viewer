using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The typed patches read back: a patch reads from the field bag it writes.
/// Swift compares decoded values; C# records hold lists by reference, so these
/// compare the JSON a patch writes before and after the round trip.
/// </summary>
public sealed partial class AnnotationPatchDecodingTests
{
    static readonly AnnotationRichText Comment = new(
    [
        new AnnotationRichTextParagraph([new AnnotationRichTextRun("Styled "), new AnnotationRichTextRun("comment") { Style = new JsonObject { ["weight"] = 700 } }])
        {
            Properties = new JsonObject { ["align"] = "center" },
        },
    ])
    {
        Body = new JsonObject { ["family"] = "helvetica", ["size"] = 12 },
    };

    static AnnotationCommonFields FullCommon => new()
    {
        Nm = "patch-1", Subject = JsonNullable<string>.Null,
        Reply = new AnnotationReply(new AnnotationRef.ObjectNumber(new PageRef(2), 9)) { Type = AnnotationReplyType.Group },
        GroupId = "reviewers", Invisible = false, Hidden = true, Print = false, NoZoom = true, NoRotate = true, NoView = false, ReadOnly = true,
        Locked = true, ToggleNoView = false, LockedContents = true,
    };

    /// <summary><paramref name="patch"/> with <see cref="FullCommon"/> reads back as itself, but for Common.Nm, which no patch writes.</summary>
    static void ExpectRoundTrip<T>(T patch) where T : AnnotationPatch, IAnnotationPatch<T>
    {
        var written = patch with { Common = FullCommon };
        var json = written.ToJson();
        var read = T.FromJson(json);
        Assert.Null(read.Common.Nm);
        Assert.Equal(FullCommon with { Nm = null }, read.Common);
        Assert.Equal(json, read.ToJson());
        Assert.Equal(written.Contents, read.Contents);
    }

    static JsonNullable<IReadOnlyList<double>> Dash(params double[] values) => JsonNullable.Of<IReadOnlyList<double>>(values);

    [Fact]
    public void InkHighlightAndNotePatchesRoundTrip()
    {
        ExpectRoundTrip(new InkAnnotationPatch
        {
            InkList = [Drafts.Stroke, [new(1.5, 2)]], Color = Drafts.C("#1f6feb"), Opacity = 0.75, StrokeWidth = 3, BorderStyle = AnnotationBorderStyle.Dashed,
            DashArray = JsonNullable<IReadOnlyList<double>>.Null, Intent = AnnotationInkIntent.InkHighlight, Rotation = 30, Contents = "Ink text",
            BlendMode = AnnotationBlendMode.Screen, RichText = Comment,
        });
        ExpectRoundTrip(new HighlightAnnotationPatch
        {
            QuadPoints = [Drafts.Quad], Color = Drafts.C("#ffcd45"), Opacity = 0.5, Contents = JsonNullable<string>.Null, BlendMode = AnnotationBlendMode.Multiply,
            RichText = Comment,
        });
        ExpectRoundTrip(new NoteAnnotationPatch
        {
            Rect = Drafts.NoteRect, Color = Drafts.C("#e5484d"), Opacity = 0.5, Icon = AnnotationNoteIcon.NewParagraph, Open = true, State = "accepted",
            StateModel = JsonNullable<string>.Null, Contents = "Typed note", BlendMode = AnnotationBlendMode.Darken, RichText = JsonNullable<AnnotationRichText>.Null,
        });
    }

    [Fact]
    public void TextMarkupAndRedactionPatchesRoundTrip()
    {
        ExpectRoundTrip(new UnderlineAnnotationPatch
        {
            QuadPoints = [Drafts.Quad], Color = Drafts.C("#0000ff"), Opacity = 0.5, Contents = JsonNullable<string>.Null, BlendMode = AnnotationBlendMode.Multiply, RichText = Comment,
        });
        ExpectRoundTrip(new SquigglyAnnotationPatch
        {
            QuadPoints = [Drafts.Quad], Color = Drafts.C("#00ff00"), Opacity = 0.75, Contents = "Squiggly text", BlendMode = AnnotationBlendMode.Darken,
            RichText = JsonNullable<AnnotationRichText>.Null,
        });
        ExpectRoundTrip(new StrikeoutAnnotationPatch
        {
            QuadPoints = [Drafts.Quad], Color = Drafts.C("#ff0000"), Opacity = 0.25, Intent = AnnotationStrikeoutIntent.StrikeoutTextEdit,
            Contents = JsonNullable<string>.Null, BlendMode = AnnotationBlendMode.Screen, RichText = Comment,
        });
        ExpectRoundTrip(new RedactionAnnotationPatch
        {
            QuadPoints = [Drafts.Quad], Rect = Drafts.Box, Color = Drafts.C("#112233"), Opacity = 0.5, InteriorColor = JsonNullable<AnnotationColor>.Null,
            OverlayText = "REDACTED", Repeat = true, FontFamily = "courier", FontSize = 9, FontColor = Drafts.C("#ffffff"), TextAlign = AnnotationTextAlignment.Center,
            Contents = "Redaction text", BlendMode = AnnotationBlendMode.Normal, RichText = Comment,
        });
    }

    [Fact]
    public void ShapePatchesRoundTrip()
    {
        ExpectRoundTrip(new SquareAnnotationPatch
        {
            Box = Drafts.Box, Color = JsonNullable<AnnotationColor>.Null, Opacity = 0.5, StrokeWidth = 3, BorderStyle = AnnotationBorderStyle.Dashed,
            DashArray = Dash(4, 2), InteriorColor = Drafts.C("#00ff00"), Rotation = 15, CloudyIntensity = 2, Contents = "Square text",
            BlendMode = AnnotationBlendMode.Multiply, RichText = Comment,
        });
        ExpectRoundTrip(new CircleAnnotationPatch
        {
            Box = Drafts.Box, Color = Drafts.C("#0000ff"), Opacity = 0.75, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Solid, DashArray = Dash(1),
            InteriorColor = Drafts.C("#ffff00"), Rotation = JsonNullable<double>.Null, CloudyIntensity = JsonNullable<double>.Null, Contents = "Circle text",
            BlendMode = AnnotationBlendMode.Overlay, RichText = Comment,
        });
        var scale = Drafts.Scale with { Origin = new MeasureOrigin(3, 4) };
        ExpectRoundTrip(new LineAnnotationPatch
        {
            Start = new(10, 10), End = new(100, 50), Color = Drafts.C("#ff00ff"), Opacity = 0.5, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Dashed,
            DashArray = Dash(3, 1), InteriorColor = JsonNullable<AnnotationColor>.Null,
            LineEndings = new(AnnotationLineEnding.OpenArrow, AnnotationLineEnding.ReversedClosedArrow), Rotation = 45,
            Intent = AnnotationLineIntent.LineDimension, CaptionEnabled = true, CaptionPosition = AnnotationCaptionPosition.Top,
            CaptionOffset = new AnnotationCaptionOffset(1, 2), Leader = new AnnotationLineLeader(5) { Extension = 1, Offset = 2 }, Contents = "Line text",
            BlendMode = AnnotationBlendMode.HardLight, RichText = Comment, Measure = scale,
        });
        ExpectRoundTrip(new PolygonAnnotationPatch
        {
            Vertices = [new(10, 10), new(100, 10), new(50, 80)], Color = Drafts.C("#0000ff"), Opacity = 0.5, StrokeWidth = 2,
            BorderStyle = AnnotationBorderStyle.Dashed, DashArray = Dash(2, 2), InteriorColor = Drafts.C("#ffff00"), Rotation = 10,
            Intent = AnnotationPolygonIntent.PolygonCloud, CloudyIntensity = 2, CaptionEnabled = true, CaptionCenter = new AnnotationCaptionCenter.At(new(50, 30)),
            Contents = "Polygon text", BlendMode = AnnotationBlendMode.Lighten, RichText = Comment, Measure = JsonNullable<PdfMeasurement>.Null,
        });
        ExpectRoundTrip(new PolylineAnnotationPatch
        {
            Vertices = [new(10, 10), new(60, 80), new(100, 10)], Color = Drafts.C("#00ffff"), Opacity = 0.75, StrokeWidth = 3,
            BorderStyle = AnnotationBorderStyle.Solid, DashArray = Dash(5), InteriorColor = Drafts.C("#ff0000"),
            LineEndings = new(AnnotationLineEnding.Circle, AnnotationLineEnding.Diamond), Rotation = 20, Intent = AnnotationPolylineIntent.PolylineDimension,
            CaptionEnabled = JsonNullable<bool>.Null, CaptionCenter = new AnnotationCaptionCenter.Automatic(), Contents = "Polyline text",
            BlendMode = AnnotationBlendMode.Difference, RichText = Comment, Measure = scale,
        });
    }

    [Fact]
    public void BoxPatchesRoundTrip()
    {
        ExpectRoundTrip(new FreeTextAnnotationPatch
        {
            Box = Drafts.Box, Contents = "Styled comment", FontFamily = "times-roman", FontSize = 14, FontColor = Drafts.C("#112233"),
            TextAlign = AnnotationTextAlignment.Justify, VerticalAlign = AnnotationVerticalAlignment.Middle, Color = Drafts.C("#445566"),
            InteriorColor = Drafts.C("#ffffcc"), Opacity = 0.75, StrokeWidth = 2, BorderStyle = AnnotationBorderStyle.Dashed, DashArray = Dash(2, 1),
            CloudyIntensity = 1, Rotation = JsonNullable<double>.Null, Intent = AnnotationFreeTextIntent.FreeTextCallout,
            CalloutLine = new AnnotationCalloutLine.Kneed(new(20, 20), new(60, 60), new(100, 130)), LineEnding = AnnotationLineEnding.OpenArrow,
            BlendMode = AnnotationBlendMode.Multiply, RichText = Comment,
        });
        ExpectRoundTrip(new CaretAnnotationPatch
        {
            Box = Drafts.Box, Color = Drafts.C("#ff0000"), Opacity = 0.5, Rotation = 90, Intent = AnnotationCaretIntent.Replace,
            Contents = JsonNullable<string>.Null, BlendMode = AnnotationBlendMode.Multiply, RichText = Comment,
        });
        ExpectRoundTrip(new LinkAnnotationPatch
        {
            Rect = Drafts.Box, Target = new AnnotationLinkTarget.GoTo(new PageDestination.Xyz(new PageRef(4), 10, null, 2)), Contents = JsonNullable<string>.Null,
        });
        ExpectRoundTrip(new StampAnnotationPatch
        {
            Box = Drafts.Box, Opacity = 0.5, Rotation = 90, Name = "Approved", Fit = JsonNullable<AnnotationStampFit>.Null, Contents = "Stamp text", RichText = Comment,
        });
        ExpectRoundTrip(new FileAttachmentAnnotationPatch
        {
            Rect = Drafts.Box, Icon = AnnotationFileAttachmentIcon.PushPin, Color = Drafts.C("#facc15"), Opacity = 0.5, Contents = JsonNullable<string>.Null,
            BlendMode = AnnotationBlendMode.Multiply, RichText = Comment,
        });
    }

    [Fact]
    public void NullAndAbsentDecodeDifferently()
    {
        Assert.Equal(JsonNullable<AnnotationColor>.Null, InkAnnotationPatch.FromJson("""{"color":null}""").Color);
        var absent = InkAnnotationPatch.FromJson("{}");
        Assert.Null(absent.Color);
        Assert.Equal(new InkAnnotationPatch(), absent);
    }

    [Fact]
    public void EveryNullableMemberReadsNull()
    {
        var line = LineAnnotationPatch.FromJson(
            """{"captionOffset":null,"color":null,"contents":null,"dashArray":null,"intent":null,"interiorColor":null,"leader":null,"measure":null,"richText":null,"rotation":null,"subject":null}""");
        Assert.Equal(
            new LineAnnotationPatch
            {
                Color = JsonNullable<AnnotationColor>.Null, DashArray = JsonNullable<IReadOnlyList<double>>.Null, InteriorColor = JsonNullable<AnnotationColor>.Null,
                Rotation = JsonNullable<double>.Null, Intent = JsonNullable<AnnotationLineIntent>.Null, CaptionOffset = JsonNullable<AnnotationCaptionOffset>.Null,
                Leader = JsonNullable<AnnotationLineLeader>.Null, Contents = JsonNullable<string>.Null, RichText = JsonNullable<AnnotationRichText>.Null,
                Measure = JsonNullable<PdfMeasurement>.Null, Common = new() { Subject = JsonNullable<string>.Null },
            },
            line);
        // A caption centre's null is a value, the engine's own placement.
        Assert.Equal(new PolygonAnnotationPatch { CaptionCenter = new AnnotationCaptionCenter.Automatic() }, PolygonAnnotationPatch.FromJson("""{"captionCenter":null}"""));
        // A free text's rich text has no null: null reads as left out.
        Assert.Equal(new FreeTextAnnotationPatch(), FreeTextAnnotationPatch.FromJson("""{"richText":null}"""));
    }

    [Fact]
    public void APatchIgnoresKeysItDoesNotDeclare()
    {
        Assert.Equal(new InkAnnotationPatch { StrokeWidth = 5 }, InkAnnotationPatch.FromJson("""{"icon":5,"strokeWidth":5,"subtype":"ink","target":"nowhere","zzz":[1]}"""));
        Assert.Equal(new UnderlineAnnotationPatch { Opacity = 0.5 }, UnderlineAnnotationPatch.FromJson("""{"intent":7,"opacity":0.5}"""));
        Assert.Equal(new PolygonAnnotationPatch { Opacity = 0.5 }, PolygonAnnotationPatch.FromJson("""{"lineEndings":7,"opacity":0.5}"""));
        Assert.Equal(new PolylineAnnotationPatch { Opacity = 0.5 }, PolylineAnnotationPatch.FromJson("""{"cloudyIntensity":"x","opacity":0.5}"""));
        Assert.Equal(new LinkAnnotationPatch(), LinkAnnotationPatch.FromJson("""{"blendMode":"normal","richText":7}"""));
    }

    [Theory]
    [InlineData("ink", """{"strokeWidth":"wide"}""")]
    [InlineData("ink", """{"borderStyle":"beveled"}""")]
    [InlineData("polygon", """{"intent":"polyline-dimension"}""")]
    [InlineData("free-text", """{"calloutLine":[{"x":1,"y":2}]}""")]
    [InlineData("ink", """{"inkList":[[{"x":1}]]}""")]
    [InlineData("ink", "[]")]
    [InlineData("ink", "{")]
    public void ADeclaredKeyOfTheWrongShapeThrows(string kind, string json)
    {
        Action read = kind switch
        {
            "ink" => () => InkAnnotationPatch.FromJson(json),
            "polygon" => () => PolygonAnnotationPatch.FromJson(json),
            _ => () => FreeTextAnnotationPatch.FromJson(json),
        };
        var error = Assert.Throws<PdfException>(read);
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal("DecodingError", error.Name);
    }

    [Fact]
    public void ADecodedPatchKeepsTheNameItsJsonCarries()
    {
        var note = NoteAnnotationPatch.FromJson("""{"nm":"note-1","open":true}""");
        Assert.Equal("note-1", note.Common.Nm?.Value);
        Assert.Equal("""{"open":true}""", note.ToJson());
    }

    [Fact]
    public void ALinePatchReadsBothPoints() => Assert.Equal(
        new LineAnnotationPatch { Start = new(1, 2), End = new(3, 4) },
        LineAnnotationPatch.FromJson("""{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}"""));
}
