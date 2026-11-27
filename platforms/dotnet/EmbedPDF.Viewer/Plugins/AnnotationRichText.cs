using System.Text.Json.Nodes;

namespace EmbedPDF;

/// <summary>
/// An annotation's comment as styled text (<c>/RC</c>): paragraphs of runs.
/// The style maps are open JSON objects, written as given (CSS-like keys such
/// as <c>font-weight</c>). Writing one sets <c>contents</c> to its plain text.
/// </summary>
/// <param name="Paragraphs">The paragraphs, in order.</param>
public sealed record AnnotationRichText(IReadOnlyList<AnnotationRichTextParagraph> Paragraphs)
{
    /// <summary>The body's style, if any.</summary>
    public JsonObject? Body { get; init; }

    /// <summary>One unstyled paragraph per line; <c>\r\n</c> and <c>\r</c> break lines too.</summary>
    public static AnnotationRichText FromPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        return new([.. lines.Select(line => new AnnotationRichTextParagraph([new AnnotationRichTextRun(line)]))]);
    }

    /// <summary>The text with no styling, paragraphs joined by <c>\n</c>.</summary>
    public string PlainText => string.Join('\n', Paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))));

    internal JsonNode ToNode(string key)
    {
        var paragraphs = new JsonArray();
        foreach (var paragraph in Paragraphs ?? throw new FieldBagException($"{key} has no paragraphs"))
        {
            var p = paragraph?.Properties?.DeepClone().AsObject() ?? [];
            var runs = new JsonArray();
            foreach (var run in paragraph?.Runs ?? throw new FieldBagException($"{key} holds a null paragraph"))
            {
                var r = new JsonObject { ["text"] = (run ?? throw new FieldBagException($"{key} holds a null run")).Text };
                if (run.Style is { } style) r["style"] = style.DeepClone();
                runs.Add((JsonNode)r);
            }
            p["runs"] = runs;
            paragraphs.Add((JsonNode)p);
        }
        var node = new JsonObject { ["paragraphs"] = paragraphs };
        if (Body is { } body) node["body"] = body.DeepClone();
        return node;
    }

    internal static AnnotationRichText Read(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["paragraphs"] is not JsonArray paragraphs) throw new FieldBagException($"{key} is not rich text");
        var list = new List<AnnotationRichTextParagraph>();
        foreach (var p in paragraphs)
        {
            if (p is not JsonObject po || po["runs"] is not JsonArray runs) throw new FieldBagException($"{key} has a paragraph without runs");
            var runList = new List<AnnotationRichTextRun>();
            foreach (var r in runs)
            {
                if (r is not JsonObject ro || ro["text"] is not { } text) throw new FieldBagException($"{key} has a run without text");
                runList.Add(new AnnotationRichTextRun(AnnotationWire.ReadString(text, key))
                {
                    Style = ro["style"] switch { null => null, JsonObject s => s.DeepClone().AsObject(), _ => throw new FieldBagException($"{key} has a run style that is not an object") },
                });
            }
            JsonObject? properties = null;
            foreach (var (name, value) in po)
            {
                if (name == "runs") continue;
                properties ??= [];
                properties[name] = value?.DeepClone();
            }
            list.Add(new AnnotationRichTextParagraph(runList) { Properties = properties });
        }
        return new(list)
        {
            Body = o["body"] switch { null => null, JsonObject b => b.DeepClone().AsObject(), _ => throw new FieldBagException($"{key} has a body that is not an object") },
        };
    }
}

/// <summary>A paragraph of styled runs.</summary>
/// <param name="Runs">Its runs, in order.</param>
public sealed record AnnotationRichTextParagraph(IReadOnlyList<AnnotationRichTextRun> Runs)
{
    /// <summary>The paragraph's own members besides <c>runs</c> (alignment, spacing), written flat beside them.</summary>
    public JsonObject? Properties { get; init; }
}

/// <summary>A run of text with one style.</summary>
/// <param name="Text">The text.</param>
public sealed record AnnotationRichTextRun(string Text)
{
    /// <summary>The run's style, if any.</summary>
    public JsonObject? Style { get; init; }
}
