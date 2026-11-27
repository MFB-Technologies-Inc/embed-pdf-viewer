using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Structural comparison for values that hold lists or JSON nodes, which C#
/// records compare by reference. Sequences compare element by element whatever
/// their concrete type, JSON nodes by <see cref="JsonNode.DeepEquals"/>, and
/// anything else by <see cref="object.Equals(object?)"/> or, failing that,
/// member by member.
/// </summary>
static class Same
{
    public static void Equal(object? expected, object? actual, string what = "value")
    {
        var difference = Difference(expected, actual, what);
        Assert.True(difference is null, $"{difference} differs");
    }

    public static bool Is(object? expected, object? actual) => Difference(expected, actual, "value") is null;

    static string? Difference(object? a, object? b, string path)
    {
        if (a is null || b is null) return a is null && b is null ? null : path;
        if (a is JsonNode na) return b is JsonNode nb && JsonNode.DeepEquals(na, nb) ? null : path;
        if (a.Equals(b)) return null;
        if (a is string || b is string) return path;
        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            if (la.Count != lb.Count) return $"{path}.Count ({la.Count} against {lb.Count})";
            for (var i = 0; i < la.Count; i++)
            {
                if (Difference(la[i], lb[i], $"{path}[{i}]") is { } inner) return inner;
            }
            return null;
        }
        var type = a.GetType();
        if (type != b.GetType()) return $"{path} ({type.Name} against {b.GetType().Name})";
        if (type.IsPrimitive || type.IsEnum) return path;
        var compared = false;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            compared = true;
            if (Difference(property.GetValue(a), property.GetValue(b), $"{path}.{property.Name}") is { } inner) return inner;
        }
        return compared ? null : path;
    }

    /// <summary>A point rounded to a thousandth: a rotated annotation's points come back off by about 1e-5.</summary>
    public static PdfPoint Round(PdfPoint point) => new(R(point.X), R(point.Y));

    public static PdfRect Round(PdfRect rect) => new(R(rect.X), R(rect.Y), R(rect.Width), R(rect.Height));

    static double R(double value) => Math.Round(value * 1000) / 1000;

    /// <summary>JSON values equal, numbers within a thousandth.</summary>
    public static bool Json(JsonNode? left, JsonNode? right) => (left, right) switch
    {
        (null, null) => true,
        (JsonObject l, JsonObject r) => l.Count == r.Count && l.All(p => r.TryGetPropertyValue(p.Key, out var v) && Json(p.Value, v)),
        (JsonArray l, JsonArray r) => l.Count == r.Count && l.Zip(r).All(p => Json(p.First, p.Second)),
        (JsonValue l, JsonValue r) when l.GetValueKind() == JsonValueKind.Number && r.GetValueKind() == JsonValueKind.Number =>
            Math.Abs(l.GetValue<double>() - r.GetValue<double>()) < 0.001,
        _ => JsonNode.DeepEquals(left, right),
    };

    /// <summary>Every key <paramref name="json"/> writes is in <paramref name="read"/> with the value written.</summary>
    public static void Written(string json, string read, string what, bool withSubtype = false)
    {
        var readFields = Drafts.Object(read);
        foreach (var (key, value) in Drafts.Object(json))
        {
            if (key == "subtype" && !withSubtype) continue;
            Assert.True(readFields.TryGetPropertyValue(key, out var readValue), $"the engine read back no '{key}' for {what}");
            Assert.True(Json(value, readValue), $"{what} '{key}': wrote {value?.ToJsonString() ?? "null"}, read {readValue?.ToJsonString() ?? "null"}");
        }
    }
}

/// <summary>
/// The engine tests of AnnotationDraftTests.swift, AnnotationKindDraftTests.swift,
/// AnnotationPatchDecodingTests.swift and AnnotationInfoTests.swift: typed drafts,
/// patches and tool defaults go through the engine and read back as
/// <see cref="AnnotationInfo"/>.
/// </summary>
public sealed class AnnotationEngineDraftTests
{
    static AnnotationColor C(string hex) => AnnotationColor.Parse(hex);

    static readonly PdfQuad Moved = new(new(80, 400), new(160, 400), new(80, 414), new(160, 414));
    static readonly PdfRect OtherBox = new(200, 220, 60, 30);

    static async Task<(PdfViewer Viewer, PageRef Page, AnnotationCapability Annotations)> Opened(string fixture = "report")
    {
        var (viewer, document, annotations) = await AnnotationTests.Opened(fixture);
        return (viewer, (await viewer.GetPagesAsync(document))[0].Page, annotations);
    }

    static async Task<AnnotationInfo> Read(AnnotationCapability annotations, AnnotationRef annotationRef) =>
        Assert.IsType<AnnotationInfo>(await annotations.GetAsync(annotationRef));

    static async Task<AnnotationInfo> Created(AnnotationCapability annotations, AnnotationDraft draft, PageRef page, ToolId? tool = null) =>
        await Read(annotations, await annotations.CreateAsync(draft, page, tool is { } t ? new AnnotationCreateOptions { Tool = t } : null));

    static async Task ExpectInvalidInput(Func<Task> call) =>
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(call)).Code);

    static void ExpectInk(
        AnnotationInfo info, string? color, double opacity, double strokeWidth, string borderStyle, double[]? dashArray,
        IReadOnlyList<IReadOnlyList<PdfPoint>> inkList, string? intent, double? rotation)
    {
        var ink = Assert.IsType<AnnotationInkDetails>(info.Ink);
        Assert.Equal(color is null ? (AnnotationColor?)null : C(color), ink.Color);
        Assert.Equal(opacity, ink.Opacity);
        Assert.Equal(strokeWidth, ink.StrokeWidth);
        Assert.Equal(new AnnotationBorderStyleName(borderStyle), ink.BorderStyle);
        Same.Equal(dashArray, ink.DashArray, "dashArray");
        Same.Equal(inkList, ink.InkList.Select(stroke => stroke.Select(Same.Round)), "inkList");
        Assert.Equal(intent is null ? (AnnotationIntent?)null : new AnnotationIntent(intent), ink.Intent);
        Assert.Equal(rotation, ink.Rotation);
    }

    static void ExpectHighlight(AnnotationInfo info, string color, double opacity, IReadOnlyList<PdfQuad> quadPoints)
    {
        var highlight = Assert.IsType<AnnotationHighlightDetails>(info.Highlight);
        Assert.Equal(C(color), highlight.Color);
        Assert.Equal(opacity, highlight.Opacity);
        Same.Equal(quadPoints, highlight.QuadPoints, "quadPoints");
    }

    static void ExpectNote(AnnotationInfo info, string color, double opacity, string icon, bool open, string? state, string? stateModel)
    {
        var note = Assert.IsType<AnnotationNoteDetails>(info.Note);
        Assert.Equal(C(color), note.Color);
        Assert.Equal(opacity, note.Opacity);
        Assert.Equal(icon, note.Icon);
        Assert.Equal(open, note.Open);
        Assert.Equal(state, note.State);
        Assert.Equal(stateModel, note.StateModel);
    }

    // ── AnnotationDraftTests.swift: the engine takes every typed member ──

    [Fact]
    public async Task CreatedDraftsReadBackEveryMember()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;

        var ink = await Created(annotations, Drafts.FullInk, page);
        Assert.Equal(AnnotationSubtype.Ink, ink.Subtype);
        Assert.Equal("Ink text", ink.Contents);
        Assert.Equal(AnnotationBlendMode.Screen, ink.BlendMode);
        ExpectInk(ink, "#1f6feb", 0.75, 3, "dashed", [4, 2], [Drafts.Stroke], "ink-highlight", 30);

        var highlight = await Created(annotations, Drafts.FullHighlight, page);
        Assert.Equal(AnnotationSubtype.Highlight, highlight.Subtype);
        Assert.Equal("Highlight text", highlight.Contents);
        Assert.Equal(AnnotationBlendMode.Multiply, highlight.BlendMode);
        ExpectHighlight(highlight, "#ffcd45", 0.5, [Drafts.Quad]);

        var note = await Created(annotations, Drafts.FullNote, page);
        Assert.Equal(AnnotationSubtype.Text, note.Subtype);
        Assert.Equal(Drafts.NoteRect, note.Rect);
        Assert.Equal("Typed note", note.Contents);
        Assert.Equal(AnnotationBlendMode.Darken, note.BlendMode);
        ExpectNote(note, "#e5484d", 0.5, "key", true, "accepted", "review");
    }

    [Fact]
    public async Task ANullColorDrawsAnInkWithNoStroke()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var ink = await Created(annotations, new InkAnnotationDraft([Drafts.Stroke]) { Color = JsonNullable<AnnotationColor>.Null }, page);
        Assert.Null(Assert.IsType<AnnotationInkDetails>(ink.Ink).Color);
    }

    [Fact]
    public async Task UpdatesChangeEveryMember()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;

        var inkRef = await annotations.CreateAsync(Drafts.FullInk, page);
        IReadOnlyList<IReadOnlyList<PdfPoint>> strokes = [[new(50, 60), new(70, 80)], [new(90, 100)]];
        await annotations.UpdateAsync(inkRef, new InkAnnotationPatch
        {
            InkList = strokes, Color = C("#00ff00"), Opacity = 0.25, StrokeWidth = 5, BorderStyle = AnnotationBorderStyle.Dashed,
            DashArray = JsonNullable.Of<IReadOnlyList<double>>([1, 1]), Rotation = 45, Contents = "Changed ink",
        });
        var ink = await Read(annotations, inkRef);
        Assert.Equal("Changed ink", ink.Contents);
        ExpectInk(ink, "#00ff00", 0.25, 5, "dashed", [1, 1], strokes, "ink-highlight", 45);

        var highlightRef = await annotations.CreateAsync(Drafts.FullHighlight, page);
        await annotations.UpdateAsync(highlightRef, new HighlightAnnotationPatch { QuadPoints = [Moved], Color = C("#00ff00"), Opacity = 1, Contents = "Changed highlight" });
        var highlight = await Read(annotations, highlightRef);
        Assert.Equal("Changed highlight", highlight.Contents);
        ExpectHighlight(highlight, "#00ff00", 1, [Moved]);

        var noteRef = await annotations.CreateAsync(Drafts.FullNote, page);
        var rect = new PdfRect(100, 120, 20, 20);
        await annotations.UpdateAsync(noteRef, new NoteAnnotationPatch
        {
            Rect = rect, Color = C("#00ff00"), Opacity = 1, Icon = AnnotationNoteIcon.NewParagraph, Open = false, State = "completed",
            StateModel = "review", Contents = "Changed note",
        });
        var note = await Read(annotations, noteRef);
        Assert.Equal(rect, note.Rect);
        Assert.Equal("Changed note", note.Contents);
        ExpectNote(note, "#00ff00", 1, "new-paragraph", false, "completed", "review");
    }

    [Fact]
    public async Task APatchLeavesWhatItLeavesOutAndClearsWhatItNulls()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;

        var inkRef = await annotations.CreateAsync(Drafts.FullInk, page);
        await annotations.UpdateAsync(inkRef, new InkAnnotationPatch { Opacity = 0.5 });
        var kept = await Read(annotations, inkRef);
        Assert.Equal(0.5, kept.Ink?.Opacity);
        Assert.Equal(C("#1f6feb"), kept.Ink?.Color);
        Same.Equal(new double[] { 4, 2 }, kept.Ink?.DashArray);
        Assert.Equal("Ink text", kept.Contents);

        await annotations.UpdateAsync(inkRef, new InkAnnotationPatch
        {
            Color = JsonNullable<AnnotationColor>.Null, BorderStyle = AnnotationBorderStyle.Solid, DashArray = JsonNullable<IReadOnlyList<double>>.Null,
            Intent = JsonNullable<AnnotationInkIntent>.Null, Rotation = JsonNullable<double>.Null, Contents = JsonNullable<string>.Null,
        });
        var cleared = await Read(annotations, inkRef);
        var ink = Assert.IsType<AnnotationInkDetails>(cleared.Ink);
        Assert.Null(ink.Color);
        Assert.Null(ink.DashArray);
        Assert.Null(ink.Intent);
        Assert.Null(ink.Rotation);
        Assert.Null(cleared.Contents);

        var noteRef = await annotations.CreateAsync(Drafts.FullNote, page);
        await annotations.UpdateAsync(noteRef, new NoteAnnotationPatch { State = JsonNullable<string>.Null, StateModel = JsonNullable<string>.Null, Contents = JsonNullable<string>.Null });
        var note = await Read(annotations, noteRef);
        Assert.Null(note.Note?.State);
        Assert.Null(note.Note?.StateModel);
        Assert.Null(note.Contents);
        Assert.Equal("key", note.Note?.Icon);

        var highlightRef = await annotations.CreateAsync(Drafts.FullHighlight, page);
        await annotations.UpdateAsync(highlightRef, new HighlightAnnotationPatch { Contents = JsonNullable<string>.Null });
        var highlight = await Read(annotations, highlightRef);
        Assert.Null(highlight.Contents);
        Assert.Equal(C("#ffcd45"), highlight.Highlight?.Color);
    }

    [Fact]
    public async Task APatchOfAnotherSubtypesFieldsIsRefused()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var highlight = await annotations.CreateAsync(Drafts.FullHighlight, page);
        await ExpectInvalidInput(() => annotations.UpdateAsync(highlight, new InkAnnotationPatch { StrokeWidth = 2 }));
    }

    [Fact]
    public async Task DefaultsReachTheToolsNextAnnotation()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();

        await tools.UpdateDefaultsAsync(
            new InkAnnotationPatch
            {
                Color = C("#00ff00"), Opacity = 0.5, StrokeWidth = 4, BorderStyle = AnnotationBorderStyle.Dashed,
                DashArray = JsonNullable.Of<IReadOnlyList<double>>([2, 3]), Rotation = 15, Contents = "Default ink",
            },
            ToolId.Ink);
        var ink = await Created(annotations, new InkAnnotationDraft([Drafts.Stroke]), page, ToolId.Ink);
        Assert.Equal("Default ink", ink.Contents);
        ExpectInk(ink, "#00ff00", 0.5, 4, "dashed", [2, 3], [Drafts.Stroke], null, 15);

        await tools.UpdateDefaultsAsync(new HighlightAnnotationPatch { Color = C("#00ff00"), Opacity = 0.5, Contents = "Default highlight" }, ToolId.Highlight);
        var highlight = await Created(annotations, new HighlightAnnotationDraft([Drafts.Quad]), page, ToolId.Highlight);
        Assert.Equal("Default highlight", highlight.Contents);
        ExpectHighlight(highlight, "#00ff00", 0.5, [Drafts.Quad]);

        await tools.UpdateDefaultsAsync(
            new NoteAnnotationPatch
            {
                Color = C("#00ff00"), Opacity = 0.5, Icon = AnnotationNoteIcon.Help, Open = true, State = "marked", StateModel = "marked", Contents = "Default note",
            },
            ToolId.Note);
        var note = await Created(annotations, new NoteAnnotationDraft(Drafts.NoteRect), page, ToolId.Note);
        Assert.Equal("Default note", note.Contents);
        ExpectNote(note, "#00ff00", 0.5, "help", true, "marked", "marked");
    }

    [Fact]
    public async Task ANullDefaultReachesTheToolsNextAnnotation()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        await tools.UpdateDefaultsAsync(new InkAnnotationPatch { Color = JsonNullable<AnnotationColor>.Null }, ToolId.Ink);
        var defaults = Drafts.Object(await tools.GetDefaultsAsync(ToolId.Ink));
        Assert.True(defaults.TryGetPropertyValue("color", out var color));
        Assert.Null(color);
        var ink = await Created(annotations, new InkAnnotationDraft([Drafts.Stroke]), page, ToolId.Ink);
        Assert.Null(ink.Ink?.Color);
    }

    // ── AnnotationDraftTests.swift: drift guard ──

    /// <summary>
    /// The engine refuses a key its kind doesn't declare, so a typed key the
    /// engine renamed or dropped makes a create throw. This pins that refusal.
    /// </summary>
    [Fact]
    public async Task TheEngineRefusesAKeyItsKindDoesntDeclare()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        string[] drafts =
        [
            """{"subtype":"ink","inkList":[[{"x":1,"y":2}]],"colour":"#ff0000"}""",
            """{"subtype":"highlight","quadPoints":[{"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300},"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312}}],"strokeWidth":2}""",
            """{"subtype":"text","rect":{"x":40,"y":40,"width":24,"height":24},"stateModell":"review"}""",
        ];
        foreach (var json in drafts) await ExpectInvalidInput(() => annotations.CreateAsync(json, page));
        var ink = await annotations.CreateAsync(new InkAnnotationDraft([Drafts.Stroke]), page);
        await ExpectInvalidInput(() => annotations.UpdateAsync(ink, """{"strokeWidht": 2}"""));
    }

    /// <summary>Every key a full typed draft writes comes back in the engine's read, with the value written.</summary>
    [Fact]
    public async Task EveryTypedKeyRoundTripsThroughTheEngine()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        AnnotationDraft[] drafts = [Drafts.FullInk, Drafts.FullHighlight, Drafts.FullNote];
        foreach (var draft in drafts)
        {
            var read = await Created(annotations, draft, page);
            Same.Written(draft.ToJson(), read.FieldsJson, draft.GetType().Name, withSubtype: true);
        }
    }

    // ── AnnotationDraftTests.swift: the JSON-string overloads ──

    [Fact]
    public async Task TheStringOverloadsTakeTheTypedJsonAlike()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        AnnotationDraft[] drafts = [Drafts.FullInk, Drafts.FullHighlight, Drafts.FullNote];
        foreach (var draft in drafts)
        {
            var typed = await Created(annotations, draft, page);
            var untyped = await Read(annotations, await annotations.CreateAsync(draft.ToJson(), page));
            Same.Equal(typed.Details, untyped.Details, draft.GetType().Name);
            Assert.Equal(typed.Rect, untyped.Rect);
            Assert.Equal(typed.Contents, untyped.Contents);
        }

        var patch = new InkAnnotationPatch { Color = C("#00ff00"), StrokeWidth = 6, Rotation = JsonNullable<double>.Null };
        var typedRef = await annotations.CreateAsync(Drafts.FullInk, page);
        var untypedRef = await annotations.CreateAsync(Drafts.FullInk, page);
        await annotations.UpdateAsync(typedRef, patch);
        await annotations.UpdateAsync(untypedRef, patch.ToJson());
        Same.Equal((await Read(annotations, typedRef)).Details, (await Read(annotations, untypedRef)).Details);

        using var tools = annotations.GetTools();
        await tools.UpdateDefaultsAsync(patch.ToJson(), ToolId.Ink);
        var viaString = await tools.GetDefaultsAsync(ToolId.Ink);
        await tools.UpdateDefaultsAsync(new InkAnnotationPatch { StrokeWidth = 1 }, ToolId.Ink);
        await tools.UpdateDefaultsAsync(patch, ToolId.Ink);
        Assert.Equal(viaString, await tools.GetDefaultsAsync(ToolId.Ink));
    }

    // ── AnnotationKindDraftTests.swift: drift guard ──

    [Fact]
    public async Task EveryTypedDraftKeyRoundTripsThroughTheEngine()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        foreach (var draft in Drafts.FullKindDrafts)
        {
            var read = await Created(annotations, draft, page);
            Same.Written(draft.ToJson(), read.FieldsJson, draft.GetType().Name);
            Assert.True(read.Details is not null, $"{draft.GetType().Name} read back untyped");
        }
    }

    /// <summary>Every key a full patch writes comes back in the engine's read, and the typed read changes with it.</summary>
    [Fact]
    public async Task EveryTypedPatchKeyRoundTripsThroughTheEngine()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        (AnnotationDraft Draft, AnnotationPatch Patch)[] cases =
        [
            (Drafts.FullUnderline, new UnderlineAnnotationPatch { QuadPoints = [Moved], Color = C("#123456"), Opacity = 1, Contents = "Changed", BlendMode = AnnotationBlendMode.Normal }),
            (Drafts.FullSquiggly, new SquigglyAnnotationPatch { QuadPoints = [Moved], Color = C("#123456"), Opacity = 1, Contents = "Changed", BlendMode = AnnotationBlendMode.Normal }),
            (Drafts.FullStrikeout, new StrikeoutAnnotationPatch
            {
                QuadPoints = [Moved], Color = C("#123456"), Opacity = 1, Intent = JsonNullable<AnnotationStrikeoutIntent>.Null, Contents = "Changed",
                BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullRedaction, new RedactionAnnotationPatch
            {
                QuadPoints = [Moved], Color = C("#654321"), Opacity = 1, InteriorColor = JsonNullable<AnnotationColor>.Null, OverlayText = JsonNullable<string>.Null,
                Repeat = false, FontFamily = "helvetica", FontSize = 0, FontColor = C("#000000"), TextAlign = AnnotationTextAlignment.Right, Contents = "Changed",
                BlendMode = AnnotationBlendMode.Multiply,
            }),
            (Drafts.FullSquare, new SquareAnnotationPatch
            {
                Box = OtherBox, Color = JsonNullable<AnnotationColor>.Null, Opacity = 1, StrokeWidth = 0, BorderStyle = AnnotationBorderStyle.Solid,
                DashArray = JsonNullable<IReadOnlyList<double>>.Null, InteriorColor = JsonNullable<AnnotationColor>.Null, Rotation = JsonNullable<double>.Null,
                CloudyIntensity = JsonNullable<double>.Null, Contents = "Changed", BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullCircle, new CircleAnnotationPatch
            {
                Box = OtherBox, Color = C("#000000"), Opacity = 0.25, StrokeWidth = 4, BorderStyle = AnnotationBorderStyle.Dashed,
                DashArray = JsonNullable.Of<IReadOnlyList<double>>([1, 1]), InteriorColor = C("#ffffff"), Rotation = 45, CloudyIntensity = 1, Contents = "Changed",
                BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullLine, new LineAnnotationPatch
            {
                Start = new(20, 30), End = new(120, 90), Color = JsonNullable<AnnotationColor>.Null, Opacity = 1, StrokeWidth = 4, BorderStyle = AnnotationBorderStyle.Solid,
                DashArray = JsonNullable<IReadOnlyList<double>>.Null, InteriorColor = JsonNullable<AnnotationColor>.Null,
                LineEndings = new(AnnotationLineEnding.None, AnnotationLineEnding.Slash), Intent = JsonNullable<AnnotationLineIntent>.Null, CaptionEnabled = false,
                CaptionPosition = AnnotationCaptionPosition.Inline, CaptionOffset = JsonNullable<AnnotationCaptionOffset>.Null, Leader = JsonNullable<AnnotationLineLeader>.Null,
                Contents = "Changed", BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullPolygon, new PolygonAnnotationPatch
            {
                Vertices = [new(0, 0), new(40, 0), new(40, 40), new(0, 40)], Color = C("#000000"), Opacity = 1, StrokeWidth = 1, BorderStyle = AnnotationBorderStyle.Solid,
                DashArray = JsonNullable<IReadOnlyList<double>>.Null, InteriorColor = JsonNullable<AnnotationColor>.Null, Intent = JsonNullable<AnnotationPolygonIntent>.Null,
                CloudyIntensity = JsonNullable<double>.Null, CaptionEnabled = false, Contents = "Changed", BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullPolyline, new PolylineAnnotationPatch
            {
                Vertices = [new(0, 0), new(40, 40)], Color = JsonNullable<AnnotationColor>.Null, Opacity = 1, StrokeWidth = 1, BorderStyle = AnnotationBorderStyle.Dashed,
                DashArray = JsonNullable.Of<IReadOnlyList<double>>([5, 5]), InteriorColor = JsonNullable<AnnotationColor>.Null,
                LineEndings = new(AnnotationLineEnding.Butt, AnnotationLineEnding.ClosedArrow), CaptionEnabled = false, Contents = "Changed",
                BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullFreeText, new FreeTextAnnotationPatch
            {
                Box = OtherBox, Contents = "Changed", FontFamily = "helvetica", FontSize = 10, FontColor = C("#000000"), TextAlign = AnnotationTextAlignment.Justify,
                VerticalAlign = AnnotationVerticalAlignment.Bottom, Color = C("#000000"), InteriorColor = JsonNullable<AnnotationColor>.Null, Opacity = 1, StrokeWidth = 0,
                BorderStyle = AnnotationBorderStyle.Solid, DashArray = JsonNullable<IReadOnlyList<double>>.Null, CloudyIntensity = JsonNullable<double>.Null, Rotation = 90,
                Intent = AnnotationFreeTextIntent.FreeText, CalloutLine = JsonNullable<AnnotationCalloutLine>.Null, LineEnding = JsonNullable<AnnotationLineEnding>.Null,
                BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullCaret, new CaretAnnotationPatch
            {
                Box = new PdfRect(90, 310, 10, 10), Color = C("#000000"), Opacity = 1, Rotation = 45, Intent = JsonNullable<AnnotationCaretIntent>.Null, Contents = "Changed",
                BlendMode = AnnotationBlendMode.Normal,
            }),
            (Drafts.FullLink, new LinkAnnotationPatch { Rect = OtherBox, Target = new AnnotationLinkTarget.Named(AnnotationLinkVerb.NextPage), Contents = "Changed" }),
        ];
        foreach (var (draft, patch) in cases)
        {
            var annotationRef = await annotations.CreateAsync(draft, page);
            await annotations.UpdateAsync(annotationRef, patch);
            var read = await Read(annotations, annotationRef);
            Same.Written(patch.ToJson(), read.FieldsJson, patch.GetType().Name);
            Assert.True(read.Details is not null, $"{patch.GetType().Name} read back untyped");
        }
    }

    static async Task<AnnotationRef> PlaceStamp(AnnotationCapability annotations, PageRef page)
    {
        using var drafts = annotations.GetDrafts();
        return await drafts.PlaceStampAsync(
            new AnnotationStampSource(Fixture.Data("tiny.png")) { MimeType = "image/png" }, new AnnotationStampPlacement(page, new PdfPoint(200, 200)), 80);
    }

    static async Task<AnnotationInfo> FixtureAttachment(AnnotationCapability annotations) =>
        (await annotations.ListAsync()).First(info => info.Subtype == AnnotationSubtype.FileAttachment);

    /// <summary>Stamps and file attachments have no draft: a placed stamp and the fixture's attachment take the patches.</summary>
    [Fact]
    public async Task StampAndFileAttachmentPatchesRoundTrip()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var stamp = await PlaceStamp(annotations, page);
        var stampPatch = new StampAnnotationPatch
        {
            Box = new PdfRect(150, 310, 100, 40), Opacity = 0.5, Rotation = 90, Name = "Draft", Fit = AnnotationStampFit.Cover, Contents = "Changed",
            Common = SharedFields,
        };
        await annotations.UpdateAsync(stamp, stampPatch);
        Same.Written(stampPatch.ToJson(), (await Read(annotations, stamp)).FieldsJson, nameof(StampAnnotationPatch));

        var (kindsViewer, _, kinds) = await Opened("annotation_kinds");
        await using var _k = kindsViewer;
        var attachment = await FixtureAttachment(kinds);
        var attachmentPatch = new FileAttachmentAnnotationPatch
        {
            Rect = new PdfRect(470, 620, 24, 24), Icon = AnnotationFileAttachmentIcon.Tag, Color = C("#00ff00"), Opacity = 0.5, Contents = "Changed",
            BlendMode = AnnotationBlendMode.Multiply, Common = SharedFields,
        };
        await kinds.UpdateAsync(attachment.AnnotationRef, attachmentPatch);
        var attachmentRead = await Read(kinds, attachment.AnnotationRef);
        Same.Written(attachmentPatch.ToJson(), attachmentRead.FieldsJson, nameof(FileAttachmentAnnotationPatch));
        Assert.Equal(AnnotationFileAttachmentIcon.Tag, attachmentRead.FileAttachment?.Icon);
        Assert.Equal("notes.txt", attachmentRead.FileAttachment?.File?.Name);
    }

    /// <summary>A patch that changes nothing but the blend mode still redraws the appearance, which is where /BM lives.</summary>
    [Fact]
    public async Task ABlendModeAloneChanges()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var highlight = await annotations.CreateAsync(Drafts.FullHighlight, page);
        await annotations.UpdateAsync(highlight, new HighlightAnnotationPatch { BlendMode = AnnotationBlendMode.Normal });
        Assert.Equal(AnnotationBlendMode.Normal, (await annotations.GetAsync(highlight))?.BlendMode);
        var square = await annotations.CreateAsync(Drafts.FullSquare, page);
        await annotations.UpdateAsync(square, new SquareAnnotationPatch { BlendMode = AnnotationBlendMode.Screen });
        Assert.Equal(AnnotationBlendMode.Screen, (await annotations.GetAsync(square))?.BlendMode);
    }

    // ── AnnotationKindDraftTests.swift: common fields ──

    /// <summary>
    /// The fields every kind shares, each away from what a create defaults to.
    /// No nm, which is one annotation's on its page, and no reply, which needs
    /// an annotation to answer.
    /// </summary>
    static AnnotationCommonFields SharedFields => new() { Subject = "S", GroupId = "reviewers", Print = true, Locked = true };

    /// <summary>Every kind's draft writes the shared fields, and the engine reads them back as written.</summary>
    [Fact]
    public async Task EveryDraftWritesItsCommonFields()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        AnnotationDraft[] drafts = [Drafts.FullInk, Drafts.FullHighlight, Drafts.FullNote, .. Drafts.FullKindDrafts];
        foreach (var bare in drafts)
        {
            var draft = bare with { Common = SharedFields };
            var what = draft.GetType().Name;
            var read = await Created(annotations, draft, page);
            var fields = Drafts.Object(read.FieldsJson);
            Assert.True((string?)fields["subject"] == "S", what);
            Assert.True((bool?)fields["print"] == true, what);
            Assert.True((bool?)fields["locked"] == true, what);
            Assert.True(read.GroupId == "reviewers", what);
            Same.Written(draft.ToJson(), read.FieldsJson, what);
        }
    }

    /// <summary>Every kind's patch writes the shared fields. Each annotation starts from a draft without them.</summary>
    [Fact]
    public async Task EveryPatchWritesItsCommonFields()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var common = SharedFields;
        (AnnotationDraft Draft, AnnotationPatch Patch)[] cases =
        [
            (Drafts.FullInk, new InkAnnotationPatch { Common = common }),
            (Drafts.FullHighlight, new HighlightAnnotationPatch { Common = common }),
            (Drafts.FullNote, new NoteAnnotationPatch { Common = common }),
            (Drafts.FullUnderline, new UnderlineAnnotationPatch { Common = common }),
            (Drafts.FullSquiggly, new SquigglyAnnotationPatch { Common = common }),
            (Drafts.FullStrikeout, new StrikeoutAnnotationPatch { Common = common }),
            (Drafts.FullRedaction, new RedactionAnnotationPatch { Common = common }),
            (Drafts.FullSquare, new SquareAnnotationPatch { Common = common }),
            (Drafts.FullCircle, new CircleAnnotationPatch { Common = common }),
            (Drafts.FullLine, new LineAnnotationPatch { Common = common }),
            (Drafts.FullPolygon, new PolygonAnnotationPatch { Common = common }),
            (Drafts.FullPolyline, new PolylineAnnotationPatch { Common = common }),
            (Drafts.FullFreeText, new FreeTextAnnotationPatch { Common = common }),
            (Drafts.FullCaret, new CaretAnnotationPatch { Common = common }),
            (Drafts.FullLink, new LinkAnnotationPatch { Common = common }),
        ];
        foreach (var (draft, patch) in cases)
        {
            var annotationRef = await annotations.CreateAsync(draft, page);
            await annotations.UpdateAsync(annotationRef, patch);
            var read = await Read(annotations, annotationRef);
            Assert.Equal("""{"groupId":"reviewers","locked":true,"print":true,"subject":"S"}""", patch.ToJson());
            Same.Written(patch.ToJson(), read.FieldsJson, patch.GetType().Name);
        }
    }

    [Fact]
    public async Task AReplyLinksANoteToTheAnnotationItAnswers()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var other = await annotations.CreateAsync(new NoteAnnotationDraft(Drafts.Box), page);
        var answer = await annotations.CreateAsync(new NoteAnnotationDraft(Drafts.NoteRect) { Common = new() { Reply = new AnnotationReply(other) } }, page);
        var read = await Read(annotations, answer);
        Assert.Equal(other, read.InReplyTo);
        Assert.Equal("reply", (string?)Drafts.Object(read.FieldsJson)["reply"]?["type"]);

        await annotations.UpdateAsync(answer, new NoteAnnotationPatch { Common = new() { Reply = new AnnotationReply(other) { Type = AnnotationReplyType.Group } } });
        var grouped = await Read(annotations, answer);
        Assert.Equal(other, grouped.InReplyTo);
        Assert.Equal("group", (string?)Drafts.Object(grouped.FieldsJson)["reply"]?["type"]);

        await annotations.UpdateAsync(answer, new NoteAnnotationPatch { Common = new() { Reply = JsonNullable<AnnotationReply>.Null } });
        Assert.Null((await Read(annotations, answer)).InReplyTo);
    }

    /// <summary>A draft names its annotation; a patch that carries a name leaves it out, where the engine would refuse the change.</summary>
    [Fact]
    public async Task ANameIsTheDraftsToSet()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var annotationRef = await annotations.CreateAsync(new SquareAnnotationDraft(Drafts.Box) { Common = new() { Nm = "typed-name" } }, page);
        Assert.Equal("typed-name", (string?)Drafts.Object((await Read(annotations, annotationRef)).FieldsJson)["nm"]);

        await annotations.UpdateAsync(annotationRef, new SquareAnnotationPatch { Common = new() { Nm = "another-name", Subject = "Renamed" } });
        var fields = Drafts.Object((await Read(annotations, annotationRef)).FieldsJson);
        Assert.Equal("typed-name", (string?)fields["nm"]);
        Assert.Equal("Renamed", (string?)fields["subject"]);
        await ExpectInvalidInput(() => annotations.UpdateAsync(annotationRef, """{"nm":"another-name"}"""));

        var unnamed = await Created(annotations, new SquareAnnotationDraft(Drafts.Box) { Common = new() { Nm = JsonNullable<string>.Null } }, page);
        var unnamedFields = Drafts.Object(unnamed.FieldsJson);
        Assert.True(unnamedFields.TryGetPropertyValue("nm", out var nm));
        Assert.Null(nm);
    }

    // ── AnnotationKindDraftTests.swift: typed reads ──

    [Fact]
    public async Task CreatedDraftsReadBackTyped()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        Task<AnnotationInfo> Make(AnnotationDraft draft) => Created(annotations, draft, page);

        var underline = await Make(Drafts.FullUnderline);
        Assert.Equal(AnnotationBlendMode.Multiply, underline.BlendMode);
        Assert.Equal(C("#0000ff"), underline.Underline?.Color);
        Same.Equal(new[] { Drafts.Quad }, underline.Underline?.QuadPoints);
        Assert.Equal(0.75, (await Make(Drafts.FullSquiggly)).Squiggly?.Opacity);
        Assert.Equal(AnnotationIntent.StrikeoutTextEdit, Assert.IsType<AnnotationStrikeoutDetails>((await Make(Drafts.FullStrikeout)).Strikeout).Intent);

        var redaction = Assert.IsType<AnnotationRedactionDetails>((await Make(Drafts.FullRedaction)).Redaction);
        Assert.Equal("REDACTED", redaction.OverlayText);
        Assert.True(redaction.Repeat);
        Assert.Equal(AnnotationTextAlignment.Center, redaction.TextAlign);
        Assert.Equal(C("#000000"), redaction.InteriorColor);
        Assert.Equal(C("#ffffff"), redaction.FontColor);

        var square = Assert.IsType<AnnotationShapeDetails>((await Make(Drafts.FullSquare)).Square);
        Assert.Equal(Drafts.Box, square.Box);
        Assert.Equal(C("#00ff00"), square.InteriorColor);
        Assert.Equal(2, square.CloudyIntensity);
        Assert.Equal(15, square.Rotation);
        var circle = await Make(Drafts.FullCircle);
        Assert.Equal(C("#0000ff"), circle.Circle?.Color);
        Assert.Null(circle.Square);

        var line = Assert.IsType<AnnotationLineDetails>((await Make(Drafts.FullLine)).Line);
        Assert.Equal(new PdfPoint(10, 10), Same.Round(line.Start));
        Assert.Equal(new PdfPoint(100, 50), Same.Round(line.End));
        Assert.Equal(new AnnotationLineEndings(AnnotationLineEnding.OpenArrow, AnnotationLineEnding.ReversedClosedArrow), line.LineEndings);
        Assert.Equal(AnnotationIntent.LineArrow, line.Intent);
        Assert.Equal(AnnotationCaptionPosition.Top, line.CaptionPosition);
        Assert.Equal(new AnnotationCaptionOffset(1, 2), line.CaptionOffset);
        Assert.Equal(new AnnotationLineLeader(5) { Extension = 1, Offset = 2 }, line.Leader);

        var polygon = Assert.IsType<AnnotationPolygonDetails>((await Make(Drafts.FullPolygon)).Polygon);
        Assert.Equal(3, polygon.Vertices.Count);
        Assert.Equal(AnnotationIntent.PolygonCloud, polygon.Intent);
        Assert.Equal(new PdfPoint(50, 30), polygon.CaptionCenter);
        var polyline = Assert.IsType<AnnotationPolylineDetails>((await Make(Drafts.FullPolyline)).Polyline);
        Assert.Equal(new AnnotationLineEndings(AnnotationLineEnding.Circle, AnnotationLineEnding.Diamond), polyline.LineEndings);

        var freeTextInfo = await Make(Drafts.FullFreeText);
        Assert.Equal("Free text", freeTextInfo.Contents);
        var freeText = Assert.IsType<AnnotationFreeTextDetails>(freeTextInfo.FreeText);
        Assert.Equal(Drafts.Box, Same.Round(freeText.Box));
        Assert.Equal(AnnotationIntent.FreeTextCallout, freeText.Intent);
        Assert.Equal("times-roman", freeText.FontFamily);
        Assert.Equal(AnnotationTextAlignment.Center, freeText.TextAlign);
        Assert.Equal(AnnotationVerticalAlignment.Middle, freeText.VerticalAlign);
        Assert.Equal(C("#112233"), freeText.FontColor);
        Assert.Equal(AnnotationLineEnding.OpenArrow, freeText.LineEnding);
        Same.Equal(new PdfPoint[] { new(20, 20), new(60, 60), new(100, 130) }, freeText.CalloutLine?.Points.Select(Same.Round));

        Assert.Equal(AnnotationIntent.Replace, Assert.IsType<AnnotationCaretDetails>((await Make(Drafts.FullCaret)).Caret).Intent);
        Assert.Equal(new AnnotationLinkTarget.Uri("https://example.test/typed"), Assert.IsType<AnnotationLinkDetails>((await Make(Drafts.FullLink)).Link).Target);
    }

    [Fact]
    public async Task ALinkReadsAPageTargetBack()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var target = new AnnotationLinkTarget.GoTo(new PageDestination.Fit(page));
        var read = await Created(annotations, new LinkAnnotationDraft(Drafts.Box, target), page);
        Assert.Equal<AnnotationLinkTarget?>(target, read.Link?.Target);
    }

    [Fact]
    public async Task EveryFixtureKindReadsTyped()
    {
        var (viewer, _, annotations) = await Opened("annotation_kinds");
        await using var _v = viewer;
        var bySubtype = new Dictionary<AnnotationSubtype, AnnotationInfo>();
        foreach (var info in await annotations.ListAsync()) bySubtype.TryAdd(info.Subtype, info);
        foreach (var (subtype, info) in bySubtype)
        {
            if (subtype == AnnotationSubtype.Popup || subtype == AnnotationSubtype.Widget) continue;
            Assert.True(info.Details is not null, $"{subtype.Value} read untyped: {info.FieldsJson}");
            Assert.True(info.BlendMode is not null, $"{subtype.Value} has no blend mode");
        }
        Assert.Equal(AnnotationBlendMode.Multiply, bySubtype[AnnotationSubtype.Highlight].BlendMode);
        Assert.Null(bySubtype[AnnotationSubtype.Popup].Details);
        var line = Assert.IsType<AnnotationLineDetails>(bySubtype[AnnotationSubtype.Line].Line);
        Assert.Equal(new AnnotationLineEndings(AnnotationLineEnding.Circle, AnnotationLineEnding.ClosedArrow), line.LineEndings);
        Assert.False(line.CaptionEnabled);
        Assert.Null(bySubtype[AnnotationSubtype.Polyline].Polyline?.Color);
        Assert.Equal("Approved", bySubtype[AnnotationSubtype.Stamp].Stamp?.Name);
        Assert.Equal(AnnotationIntent.Replace, bySubtype[AnnotationSubtype.Caret].Caret?.Intent);
        Assert.Equal(AnnotationTextAlignment.Right, bySubtype[AnnotationSubtype.FreeText].FreeText?.TextAlign);
        Assert.Equal<AnnotationLinkTarget?>(new AnnotationLinkTarget.Uri("https://example.test/linked"), bySubtype[AnnotationSubtype.Link].Link?.Target);
        var attachment = Assert.IsType<AnnotationFileAttachmentDetails>(bySubtype[AnnotationSubtype.FileAttachment].FileAttachment);
        Assert.Equal(AnnotationFileAttachmentIcon.Paperclip, attachment.Icon);
        Assert.Equal("notes.txt", attachment.File?.Name);
        Assert.Equal("text/plain", attachment.File?.MimeType);
        Assert.Equal(15L, attachment.File?.SizeBytes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_709_294_400), attachment.File?.CreatedAt);
        var redaction = Assert.IsType<AnnotationRedactionDetails>(bySubtype[AnnotationSubtype.Redact].Redaction);
        Assert.Equal("REDACTED", redaction.OverlayText);
        Assert.Equal(AnnotationTextAlignment.Center, redaction.TextAlign);
    }

    // ── AnnotationKindDraftTests.swift: rich text ──

    static AnnotationRichText StyledText => new(
    [
        new AnnotationRichTextParagraph([new AnnotationRichTextRun("Plain and "), new AnnotationRichTextRun("bold") { Style = new JsonObject { ["weight"] = 700 } }]),
        new AnnotationRichTextParagraph([new AnnotationRichTextRun("Second line")]),
    ]);

    /// <summary>The text of each paragraph's runs, joined.</summary>
    static string[]? Texts(AnnotationRichText? richText) => richText?.Paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))).ToArray();

    [Fact]
    public async Task AFreeTextReadsItsRichTextBack()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var styled = StyledText;
        var annotationRef = await annotations.CreateAsync(
            new FreeTextAnnotationDraft(Drafts.Box) { Contents = "Plain and bold\nSecond line", FontSize = 14, RichText = styled }, page);
        var read = await Read(annotations, annotationRef);
        Assert.Equal("Plain and bold\nSecond line", read.Contents);
        var richText = Assert.IsType<AnnotationRichText>(read.FreeText?.RichText);
        Same.Equal(styled.Paragraphs.Select(p => p.Runs), richText.Paragraphs.Select(p => p.Runs), "runs");
        // The body is the draft's text style, stated whole.
        Assert.Equal(14, richText.Body?["size"]?.GetValue<double>());

        // What a read hands out, a patch takes back with one run changed, and
        // the body and the other runs are not lost. An unchanged value would
        // prove nothing: the engine treats it as no change.
        var second = richText.Paragraphs[1];
        var edited = richText with { Paragraphs = [richText.Paragraphs[0], second with { Runs = [second.Runs[0] with { Text = "Another line" }] }] };
        await annotations.UpdateAsync(annotationRef, new FreeTextAnnotationPatch { RichText = edited });
        var again = await Read(annotations, annotationRef);
        var patched = Assert.IsType<AnnotationRichText>(again.FreeText?.RichText);
        Assert.Equal(["Another line"], patched.Paragraphs[1].Runs.Select(r => r.Text));
        Same.Equal(richText.Body, patched.Body, "body");
        Same.Equal(richText.Paragraphs[0], patched.Paragraphs[0], "first paragraph");
        Same.Equal(edited, patched, "rich text");
        Assert.Equal("Plain and bold\nAnother line", again.Contents);

        // Contents that isn't the rich text's plain text is refused, on a
        // patch and on a draft.
        await ExpectInvalidInput(() => annotations.UpdateAsync(
            annotationRef, new FreeTextAnnotationPatch { Contents = "Something else", RichText = AnnotationRichText.FromPlainText("New text") }));
        await ExpectInvalidInput(() => annotations.CreateAsync(new FreeTextAnnotationDraft(Drafts.Box) { Contents = "Something else", RichText = StyledText }, page));
    }

    /// <summary>The engine keeps a free text's text as rich text, so plain contents reads back as one unstyled run per line.</summary>
    [Fact]
    public async Task APlainFreeTextReadsRichText()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var read = await Created(annotations, new FreeTextAnnotationDraft(Drafts.Box) { Contents = "One\nTwo" }, page);
        Same.Equal(AnnotationRichText.FromPlainText("One\nTwo").Paragraphs, read.FreeText?.RichText?.Paragraphs);
    }

    [Fact]
    public async Task ACommentsRichTextBringsItsContents()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var annotationRef = await annotations.CreateAsync(new SquareAnnotationDraft(Drafts.Box) { RichText = StyledText }, page);
        var rich = await Read(annotations, annotationRef);
        Assert.Equal("Plain and bold\nSecond line", rich.Contents);
        Assert.Equal(["Plain and bold", "Second line"], Texts(rich.Square?.RichText)!);

        // New plain contents on a rich comment become its paragraphs.
        await annotations.UpdateAsync(annotationRef, new SquareAnnotationPatch { Contents = "Changed" });
        Assert.Equal(["Changed"], Texts((await Read(annotations, annotationRef)).Square?.RichText)!);

        // Null makes the comment plain and keeps contents.
        await annotations.UpdateAsync(annotationRef, new SquareAnnotationPatch { RichText = JsonNullable<AnnotationRichText>.Null });
        var plain = await Read(annotations, annotationRef);
        Assert.Null(plain.Square?.RichText);
        Assert.Equal("Changed", plain.Contents);

        // A comment that was never rich reads none.
        Assert.Null((await Created(annotations, Drafts.FullSquare, page)).Square?.RichText);
    }

    /// <summary>Every kind that declares richText writes it and reads it back.</summary>
    [Fact]
    public async Task EveryKindWritesItsRichText()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var comment = AnnotationRichText.FromPlainText("A comment");
        (AnnotationDraft Draft, AnnotationPatch Patch, Func<AnnotationInfo, AnnotationRichText?> RichText)[] cases =
        [
            (Drafts.FullInk, new InkAnnotationPatch { RichText = comment }, i => i.Ink?.RichText),
            (Drafts.FullHighlight, new HighlightAnnotationPatch { RichText = comment }, i => i.Highlight?.RichText),
            (Drafts.FullNote, new NoteAnnotationPatch { RichText = comment }, i => i.Note?.RichText),
            (Drafts.FullUnderline, new UnderlineAnnotationPatch { RichText = comment }, i => i.Underline?.RichText),
            (Drafts.FullSquiggly, new SquigglyAnnotationPatch { RichText = comment }, i => i.Squiggly?.RichText),
            (Drafts.FullStrikeout, new StrikeoutAnnotationPatch { RichText = comment }, i => i.Strikeout?.RichText),
            (Drafts.FullRedaction, new RedactionAnnotationPatch { RichText = comment }, i => i.Redaction?.RichText),
            (Drafts.FullSquare, new SquareAnnotationPatch { RichText = comment }, i => i.Square?.RichText),
            (Drafts.FullCircle, new CircleAnnotationPatch { RichText = comment }, i => i.Circle?.RichText),
            (Drafts.FullLine, new LineAnnotationPatch { RichText = comment }, i => i.Line?.RichText),
            (Drafts.FullPolygon, new PolygonAnnotationPatch { RichText = comment }, i => i.Polygon?.RichText),
            (Drafts.FullPolyline, new PolylineAnnotationPatch { RichText = comment }, i => i.Polyline?.RichText),
            (Drafts.FullFreeText, new FreeTextAnnotationPatch { RichText = comment }, i => i.FreeText?.RichText),
            (Drafts.FullCaret, new CaretAnnotationPatch { RichText = comment }, i => i.Caret?.RichText),
        ];
        foreach (var (draft, patch, richText) in cases)
        {
            var annotationRef = await annotations.CreateAsync(draft, page);
            await annotations.UpdateAsync(annotationRef, patch);
            var read = await Read(annotations, annotationRef);
            Same.Equal(new[] { "A comment" }, Texts(richText(read)), patch.GetType().Name);
            Assert.True(read.Contents == "A comment", patch.GetType().Name);
        }

        (AnnotationDraft Draft, Func<AnnotationInfo, AnnotationRichText?> RichText)[] drafts =
        [
            (new InkAnnotationDraft([Drafts.Stroke]) { RichText = comment }, i => i.Ink?.RichText),
            (new HighlightAnnotationDraft([Drafts.Quad]) { RichText = comment }, i => i.Highlight?.RichText),
            (new NoteAnnotationDraft(Drafts.NoteRect) { RichText = comment }, i => i.Note?.RichText),
            (new UnderlineAnnotationDraft([Drafts.Quad]) { RichText = comment }, i => i.Underline?.RichText),
            (new SquigglyAnnotationDraft([Drafts.Quad]) { RichText = comment }, i => i.Squiggly?.RichText),
            (new StrikeoutAnnotationDraft([Drafts.Quad]) { RichText = comment }, i => i.Strikeout?.RichText),
            (new RedactionAnnotationDraft { QuadPoints = [Drafts.Quad], RichText = comment }, i => i.Redaction?.RichText),
            (new CircleAnnotationDraft(Drafts.Box) { RichText = comment }, i => i.Circle?.RichText),
            (new LineAnnotationDraft(new(10, 10), new(100, 50)) { RichText = comment }, i => i.Line?.RichText),
            (new PolygonAnnotationDraft([new(10, 10), new(100, 10), new(50, 80)]) { RichText = comment }, i => i.Polygon?.RichText),
            (new PolylineAnnotationDraft([new(10, 10), new(60, 80)]) { RichText = comment }, i => i.Polyline?.RichText),
            (new CaretAnnotationDraft(new PdfRect(72, 300, 12, 12)) { RichText = comment }, i => i.Caret?.RichText),
        ];
        foreach (var (draft, richText) in drafts)
        {
            var read = await Created(annotations, draft, page);
            Same.Equal(new[] { "A comment" }, Texts(richText(read)), draft.GetType().Name);
            Assert.True(read.Contents == "A comment", draft.GetType().Name);
        }
    }

    [Fact]
    public async Task StampAndFileAttachmentPatchesWriteRichText()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var comment = AnnotationRichText.FromPlainText("A comment");
        var stamp = await PlaceStamp(annotations, page);
        await annotations.UpdateAsync(stamp, new StampAnnotationPatch { RichText = comment });
        Assert.Equal(["A comment"], Texts((await Read(annotations, stamp)).Stamp?.RichText)!);

        var (kindsViewer, _, kinds) = await Opened("annotation_kinds");
        await using var _k = kindsViewer;
        var attachment = await FixtureAttachment(kinds);
        await kinds.UpdateAsync(attachment.AnnotationRef, new FileAttachmentAnnotationPatch { RichText = comment });
        Assert.Equal(["A comment"], Texts((await Read(kinds, attachment.AnnotationRef)).FileAttachment?.RichText)!);
    }

    // ── AnnotationKindDraftTests.swift: measurement scales ──

    [Fact]
    public async Task ADimensionLineReadsItsMeasureBack()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var annotationRef = await annotations.CreateAsync(
            new LineAnnotationDraft(new(72, 72), new(216, 72)) { Intent = AnnotationLineIntent.LineDimension, Measure = Drafts.Scale }, page);
        var read = await Read(annotations, annotationRef);
        Assert.Equal(AnnotationIntent.LineDimension, read.Line?.Intent);
        Same.Equal(Drafts.Scale, read.Line?.Measure, "measure");
        // The engine works the label out from the points and the scale: 144
        // points at 0.125 ft a point.
        Assert.Equal("18 ft", read.Contents);

        var metres = Drafts.Scale with { Ratio = "1 in = 1 m" };
        await annotations.UpdateAsync(annotationRef, new LineAnnotationPatch { Measure = metres });
        Same.Equal(metres, (await Read(annotations, annotationRef)).Line?.Measure, "measure");

        await annotations.UpdateAsync(annotationRef, new LineAnnotationPatch { Measure = JsonNullable<PdfMeasurement>.Null });
        var cleared = await Read(annotations, annotationRef);
        Assert.Null(cleared.Line?.Measure);
        Assert.NotNull(cleared.Details);
    }

    [Fact]
    public async Task PolygonsAndPolylinesReadTheirMeasureBack()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        PdfPoint[] vertices = [new(72, 72), new(216, 72), new(216, 216)];
        var polygon = await Created(annotations, new PolygonAnnotationDraft(vertices) { Intent = AnnotationPolygonIntent.PolygonDimension, Measure = Drafts.Scale }, page);
        Same.Equal(Drafts.Scale, polygon.Polygon?.Measure, "polygon measure");
        var polyline = await Created(annotations, new PolylineAnnotationDraft(vertices) { Intent = AnnotationPolylineIntent.PolylineDimension, Measure = Drafts.Scale }, page);
        Same.Equal(Drafts.Scale, polyline.Polyline?.Measure, "polyline measure");
    }

    /// <summary>A scale the engine can't model reads as its subtype alone, and a draft can't write one.</summary>
    [Fact]
    public async Task AForeignMeasureReadsAsItsSubtypeAndIsRefusedOnADraft()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var line = await Created(annotations, new LineAnnotationDraft(new(72, 72), new(216, 72)), page);
        var fields = Drafts.Object(line.FieldsJson);
        fields["measure"] = new JsonObject { ["subtype"] = "geospatial" };
        var record = line.ToRecord() with { FieldsJson = fields.ToJsonString() };
        Same.Equal(new PdfMeasurement("geospatial"), new AnnotationInfo(record).Line?.Measure, "measure");

        await ExpectInvalidInput(() => annotations.CreateAsync(
            new LineAnnotationDraft(new(72, 72), new(216, 72)) { Measure = new PdfMeasurement("geospatial") }, page));
    }

    // ── AnnotationKindDraftTests.swift: tool defaults ──

    [Fact]
    public async Task TypedDefaultsReachTheTool()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        (ToolId Tool, AnnotationPatch Patch)[] cases =
        [
            (ToolId.Underline, new UnderlineAnnotationPatch { Color = C("#123456"), Opacity = 0.5, BlendMode = AnnotationBlendMode.Multiply }),
            (ToolId.Squiggly, new SquigglyAnnotationPatch { Color = C("#123456"), BlendMode = AnnotationBlendMode.Darken }),
            (ToolId.Strikeout, new StrikeoutAnnotationPatch { Color = C("#123456"), BlendMode = AnnotationBlendMode.Screen }),
            (ToolId.Square, new SquareAnnotationPatch { Color = JsonNullable<AnnotationColor>.Null, StrokeWidth = 2, InteriorColor = C("#00ff00"), BlendMode = AnnotationBlendMode.Multiply }),
            (ToolId.Circle, new CircleAnnotationPatch { BorderStyle = AnnotationBorderStyle.Dashed, DashArray = JsonNullable.Of<IReadOnlyList<double>>([2, 2]), CloudyIntensity = 1 }),
            (ToolId.Line, new LineAnnotationPatch { LineEndings = new(AnnotationLineEnding.None, AnnotationLineEnding.OpenArrow), CaptionEnabled = true }),
            (ToolId.Polygon, new PolygonAnnotationPatch { InteriorColor = C("#ffff00"), CaptionCenter = new AnnotationCaptionCenter.Automatic() }),
            (ToolId.Polyline, new PolylineAnnotationPatch { LineEndings = new(AnnotationLineEnding.Circle, AnnotationLineEnding.Circle) }),
            (ToolId.FreeText, new FreeTextAnnotationPatch
            {
                FontFamily = "courier", FontSize = 18, FontColor = C("#ff0000"), TextAlign = AnnotationTextAlignment.Center,
                VerticalAlign = AnnotationVerticalAlignment.Middle, InteriorColor = C("#ffffcc"), StrokeWidth = 0,
            }),
            (ToolId.Redact, new RedactionAnnotationPatch { OverlayText = "X", Repeat = true, TextAlign = AnnotationTextAlignment.Right }),
        ];
        foreach (var (tool, patch) in cases)
        {
            await tools.UpdateDefaultsAsync(patch, tool);
            Same.Written(patch.ToJson(), await tools.GetDefaultsAsync(tool), $"{tool.Value} defaults");
        }
    }

    [Fact]
    public async Task ATypedDefaultReachesTheToolsNextAnnotation()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        await tools.UpdateDefaultsAsync(new SquareAnnotationPatch { Color = C("#00ff00"), StrokeWidth = 2, BlendMode = AnnotationBlendMode.Multiply }, ToolId.Square);
        var square = await Created(annotations, new SquareAnnotationDraft(Drafts.Box), page, ToolId.Square);
        Assert.Equal(C("#00ff00"), square.Square?.Color);
        Assert.Equal(2, square.Square?.StrokeWidth);
        Assert.Equal(AnnotationBlendMode.Multiply, square.BlendMode);
    }

    [Fact]
    public async Task EveryKindsStringOverloadsTakeTheTypedJsonAlike()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        foreach (var draft in Drafts.FullKindDrafts)
        {
            var typed = await Created(annotations, draft, page);
            var untyped = await Read(annotations, await annotations.CreateAsync(draft.ToJson(), page));
            Same.Equal(typed.Details, untyped.Details, draft.GetType().Name);
            Assert.Equal(typed.BlendMode, untyped.BlendMode);
            Assert.Equal(typed.Contents, untyped.Contents);
        }
    }

    // ── AnnotationPatchDecodingTests.swift: tool defaults ──

    [Fact]
    public async Task DefaultsReadBackWhatUpdateDefaultsWrote()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        await tools.UpdateDefaultsAsync(
            new InkAnnotationPatch { StrokeWidth = 5, Common = new() { Subject = "Review", GroupId = "reviewers", Locked = true } }, ToolId.Ink);
        var ink = await tools.GetDefaultsAsync<InkAnnotationPatch>(ToolId.Ink);
        Assert.Equal(5, ink.StrokeWidth);
        Assert.Equal((JsonNullable<string>)"Review", ink.Common.Subject);
        Assert.Equal((JsonNullable<string>)"reviewers", ink.Common.GroupId);
        Assert.Equal(true, ink.Common.Locked);
        // The members the patch left alone are the tool's own.
        Assert.Equal(true, ink.Common.Print);
        Assert.Equal(AnnotationBorderStyle.Solid, ink.BorderStyle);
    }

    /// <summary>The engine reports a default set to nothing as null and leaves an unset one out, and the typed read keeps the two apart.</summary>
    [Fact]
    public async Task ANullDefaultAndAnUnsetOneReadApart()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        var fresh = await tools.GetDefaultsAsync<InkAnnotationPatch>(ToolId.Ink);
        Assert.Equal((JsonNullable<AnnotationColor>)C("#ef4444"), fresh.Color);
        Assert.Null(fresh.DashArray);
        Assert.Null(fresh.Rotation);
        Assert.Null(fresh.Contents);
        Assert.Null(fresh.Common.Subject);

        await tools.UpdateDefaultsAsync(new InkAnnotationPatch { Color = JsonNullable<AnnotationColor>.Null, DashArray = JsonNullable<IReadOnlyList<double>>.Null }, ToolId.Ink);
        var raw = Drafts.Object(await tools.GetDefaultsAsync(ToolId.Ink));
        Assert.True(raw.TryGetPropertyValue("color", out var color) && color is null);
        Assert.True(raw.TryGetPropertyValue("dashArray", out var dashArray) && dashArray is null);
        Assert.False(raw.ContainsKey("rotation"));
        var ink = await tools.GetDefaultsAsync<InkAnnotationPatch>(ToolId.Ink);
        Assert.True(ink.Color is { IsNull: true });
        Assert.True(ink.DashArray is { IsNull: true });
        Assert.Null(ink.Rotation);
    }

    [Fact]
    public async Task EveryToolsDefaultsReadAsItsPatch()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        async Task Reads<T>(params ToolId[] ids) where T : AnnotationPatch, IAnnotationPatch<T>
        {
            foreach (var id in ids) _ = await tools.GetDefaultsAsync<T>(id);
        }
        await Reads<InkAnnotationPatch>(ToolId.Ink, ToolId.InkHighlight);
        await Reads<HighlightAnnotationPatch>(ToolId.Highlight);
        await Reads<UnderlineAnnotationPatch>(ToolId.Underline);
        await Reads<SquigglyAnnotationPatch>(ToolId.Squiggly);
        await Reads<StrikeoutAnnotationPatch>(ToolId.Strikeout);
        await Reads<RedactionAnnotationPatch>(ToolId.Redact);
        await Reads<SquareAnnotationPatch>(ToolId.Square);
        await Reads<CircleAnnotationPatch>(ToolId.Circle);
        await Reads<LineAnnotationPatch>(ToolId.Line, ToolId.Distance, ToolId.Calibrate);
        await Reads<PolygonAnnotationPatch>(ToolId.Polygon, ToolId.Area);
        await Reads<PolylineAnnotationPatch>(ToolId.Polyline, ToolId.Perimeter);
        await Reads<FreeTextAnnotationPatch>(ToolId.FreeText, ToolId.FreeTextCallout);
        await Reads<CaretAnnotationPatch>(ToolId.InsertText, ToolId.ReplaceText);
        await Reads<StampAnnotationPatch>(ToolId.Stamp);
        await Reads<LinkAnnotationPatch>(ToolId.Link);
        await Reads<NoteAnnotationPatch>(ToolId.Note);
        await Reads<FileAttachmentAnnotationPatch>(ToolId.Attachment);

        var distance = await tools.GetDefaultsAsync<LineAnnotationPatch>(ToolId.Distance);
        Assert.Equal((JsonNullable<AnnotationLineIntent>)AnnotationLineIntent.LineDimension, distance.Intent);
        Assert.Equal((JsonNullable<AnnotationLineLeader>)new AnnotationLineLeader(12) { Extension = 5, Offset = 0 }, distance.Leader);
        Assert.Equal(new AnnotationLineEndings(AnnotationLineEnding.ClosedArrow, AnnotationLineEnding.ClosedArrow), distance.LineEndings);
    }

    /// <summary>The defaults are an open field bag: the JSON overload stores a name, and a value no patch member holds.</summary>
    [Fact]
    public async Task DefaultsThatDoNotReadAsThePatchThrowInvalidInput()
    {
        var (viewer, _, annotations) = await Opened();
        await using var _v = viewer;
        using var tools = annotations.GetTools();
        await tools.UpdateDefaultsAsync("""{"nm":"fixed-name"}""", ToolId.Ink);
        Assert.Equal((JsonNullable<string>)"fixed-name", (await tools.GetDefaultsAsync<InkAnnotationPatch>(ToolId.Ink)).Common.Nm);

        await tools.UpdateDefaultsAsync("""{"strokeWidth":"wide"}""", ToolId.Ink);
        await ExpectInvalidInput(() => tools.GetDefaultsAsync<InkAnnotationPatch>(ToolId.Ink));
        // Another kind's patch doesn't read strokeWidth, so it still decodes.
        _ = await tools.GetDefaultsAsync<HighlightAnnotationPatch>(ToolId.Ink);
    }

    // ── AnnotationInfoTests.swift ──
    // theGeneratedRecordSpellingsStillRead isn't ported: it checks the
    // deprecated overloads that hand out the generated record, which C# keeps
    // internal.

    [Fact]
    public async Task TheFixturesIntentsAndBorderStylesReadAsConstants()
    {
        var (viewer, page, annotations) = await Opened("annotation_kinds");
        await using var _v = viewer;
        var infos = await annotations.ListAsync();

        // The fixture's line names no /IT, so its intent is absent.
        var line = infos.Select(i => i.Line).First(l => l is not null)!;
        Assert.Null(line.Intent);
        Assert.Equal((AnnotationBorderStyleName)"solid", line.BorderStyle);
        Assert.Equal(AnnotationBorderStyleName.Solid, line.BorderStyle);

        static IEnumerable<AnnotationIntent> Present(IEnumerable<AnnotationIntent?> intents) => intents.Where(i => i.HasValue).Select(i => i!.Value);
        Assert.Equal([AnnotationIntent.InkHighlight], Present(infos.Select(i => i.Ink?.Intent)));
        Assert.Equal([AnnotationIntent.StrikeoutTextEdit], Present(infos.Select(i => i.Strikeout?.Intent)));
        Assert.Equal([AnnotationIntent.Replace], Present(infos.Select(i => i.Caret?.Intent)));
        Assert.Contains(infos, i => i.FreeText?.Intent == AnnotationIntent.FreeTextCallout);
        var styles = infos
            .Select(i => i.Square?.BorderStyle ?? i.Circle?.BorderStyle ?? i.Ink?.BorderStyle ?? i.FreeText?.BorderStyle)
            .Where(s => s.HasValue).Select(s => s!.Value).ToHashSet();
        Assert.Subset(new HashSet<AnnotationBorderStyleName> { AnnotationBorderStyleName.Solid, AnnotationBorderStyleName.Dashed, AnnotationBorderStyleName.Beveled, AnnotationBorderStyleName.Inset }, styles);
        Assert.Contains(AnnotationBorderStyleName.Dashed, styles);

        // A created line reads its intent back as the constant.
        var created = Assert.IsType<AnnotationLineDetails>(
            (await Created(annotations, new LineAnnotationDraft(new(100, 100), new(200, 150)) { Intent = AnnotationLineIntent.LineArrow }, page)).Line);
        Assert.Equal(AnnotationIntent.LineArrow, created.Intent);
        Assert.Equal((AnnotationIntent)"line-arrow", created.Intent);
    }

    [Fact]
    public async Task CreatedAnnotationsReadBackTyped()
    {
        var (viewer, page, annotations) = await Opened();
        await using var _v = viewer;
        var events = annotations.ObserveEvents();

        var ink = await annotations.CreateAsync(new InkAnnotationDraft([Drafts.Stroke]) { Color = C("#1f6feb"), Opacity = 0.75, StrokeWidth = 3 }, page);
        var highlight = await annotations.CreateAsync(new HighlightAnnotationDraft([Drafts.Quad]) { Color = C("#ffcd45") }, page);
        var note = await annotations.CreateAsync(new NoteAnnotationDraft(Drafts.NoteRect) { Icon = AnnotationNoteIcon.Key, Contents = "Typed note" }, page);

        var readInk = await Read(annotations, ink);
        Assert.Equal(AnnotationSubtype.Ink, readInk.Subtype);
        var inkMembers = Assert.IsType<AnnotationInkDetails>(readInk.Ink);
        Same.Equal(new[] { Drafts.Stroke }, inkMembers.InkList, "inkList");
        Assert.Equal(C("#1f6feb"), inkMembers.Color);
        Assert.Equal(0.75, inkMembers.Opacity);
        Assert.Equal(3, inkMembers.StrokeWidth);

        var readHighlight = await Read(annotations, highlight);
        Assert.Equal(AnnotationSubtype.Highlight, readHighlight.Subtype);
        var highlightMembers = Assert.IsType<AnnotationHighlightDetails>(readHighlight.Highlight);
        Assert.Equal(C("#ffcd45"), highlightMembers.Color);
        Assert.Single(highlightMembers.QuadPoints);

        var readNote = await Read(annotations, note);
        Assert.Equal(AnnotationSubtype.Text, readNote.Subtype);
        Assert.Equal("Typed note", readNote.Contents);
        Assert.Equal("key", readNote.Note?.Icon);
        // The engine stamps a created annotation itself, in UTC with whole
        // seconds, and the stamp reads as the instant it names.
        var stamped = Assert.IsType<string>(readNote.RawCreatedAt);
        var createdAt = Assert.IsType<DateTimeOffset>(readNote.CreatedAt);
        Assert.Equal(stamped, createdAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Math.Abs((createdAt - DateTimeOffset.UtcNow).TotalSeconds) < 300);
        if (readNote.RawModifiedAt is { } modified) Assert.True(readNote.ModifiedAt is not null, $"the engine wrote {modified}");

        // Every read spelling agrees, and each record round-trips.
        var listed = await annotations.ListAsync(new AnnotationFilter { Pages = [page] });
        foreach (var created in new[] { readInk, readHighlight, readNote })
        {
            Assert.Contains(listed, l => Same.Is(created, l));
            Same.Equal(created, new AnnotationInfo(created.ToRecord()), "round trip");
        }
        using var selection = annotations.GetSelection();
        await selection.SelectAsync([note]);
        Same.Equal(new[] { readNote }, await annotations.GetSelectedAsync(), "selected");

        var createdSubtypes = new List<AnnotationSubtype>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is AnnotationEvent.Created c) createdSubtypes.Add(c.Annotation.Subtype);
            if (createdSubtypes.Count == 3) break;
        }
        Assert.Equal([AnnotationSubtype.Ink, AnnotationSubtype.Highlight, AnnotationSubtype.Text], createdSubtypes);
    }
}
