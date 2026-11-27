using System.Text.Json.Nodes;

namespace EmbedPDF;

/// <summary>
/// Where a link goes, as a link draft or patch writes it: the targets the
/// engine writes. A link read from a file can point elsewhere too (another
/// file, a launch, a script); read those from <c>AnnotationInfo.FieldsJson</c>.
/// </summary>
public abstract record AnnotationLinkTarget
{
    private protected AnnotationLinkTarget() { }

    /// <summary>A place in this document, page space: from the top-left of the page it goes to, y down.</summary>
    /// <param name="Destination">The place.</param>
    public sealed record GoTo(PageDestination Destination) : AnnotationLinkTarget;

    /// <summary>A URI, the app's to open.</summary>
    /// <param name="Value">The URI.</param>
    public sealed record Uri(string Value) : AnnotationLinkTarget;

    /// <summary>A page verb.</summary>
    /// <param name="Verb">The verb.</param>
    public sealed record Named(AnnotationLinkVerb Verb) : AnnotationLinkTarget;

    /// <summary>The engine's <c>LinkTarget</c> record. Throws <c>invalid-input</c> for a value JSON can't hold.</summary>
    public string ToJson() => FieldBag.Encode(bag =>
    {
        foreach (var (key, value) in (JsonObject)ToNode("target")) bag.Set(key, value?.DeepClone());
    });

    internal JsonNode ToNode(string key) => this switch
    {
        GoTo g => new JsonObject { ["kind"] = "goto", ["destination"] = DestinationNode(g.Destination ?? throw new FieldBagException($"{key} has no destination"), key) },
        Uri u => new JsonObject { ["kind"] = "uri", ["uri"] = u.Value ?? throw new FieldBagException($"{key} has no URI") },
        Named n => new JsonObject { ["kind"] = "named", ["name"] = Wire<AnnotationLinkVerb>.Name(n.Verb, key) },
        _ => throw Ffi.Unknown(this),
    };

    static JsonNode DestinationNode(PageDestination destination, string key)
    {
        JsonNode? Coordinate(double? value) => value is { } v ? AnnotationWire.Number(v, key) : null;
        JsonObject Place(string kind) => new() { ["kind"] = kind, ["page"] = AnnotationWire.Page(destination.Page) };
        switch (destination)
        {
            case PageDestination.Xyz d:
                var xyz = Place("xyz");
                xyz["x"] = Coordinate(d.X);
                xyz["y"] = Coordinate(d.Y);
                xyz["zoom"] = Coordinate(d.Zoom);
                return xyz;
            case PageDestination.Fit:
                return Place("fit");
            case PageDestination.FitH d:
                var fitH = Place("fitH");
                fitH["y"] = Coordinate(d.Y);
                return fitH;
            case PageDestination.FitV d:
                var fitV = Place("fitV");
                fitV["x"] = Coordinate(d.X);
                return fitV;
            case PageDestination.FitR d:
                var fitR = Place("fitR");
                fitR["x"] = AnnotationWire.Number(d.X, key);
                fitR["y"] = AnnotationWire.Number(d.Y, key);
                fitR["width"] = AnnotationWire.Number(d.Width, key);
                fitR["height"] = AnnotationWire.Number(d.Height, key);
                return fitR;
            case PageDestination.FitB:
                return Place("fitB");
            case PageDestination.FitBH d:
                var fitBh = Place("fitBH");
                fitBh["y"] = Coordinate(d.Y);
                return fitBh;
            case PageDestination.FitBV d:
                var fitBv = Place("fitBV");
                fitBv["x"] = Coordinate(d.X);
                return fitBv;
            default:
                throw Ffi.Unknown(destination);
        }
    }

    internal static AnnotationLinkTarget Read(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["kind"] is not { } kind) throw new FieldBagException($"{key} is not a link target");
        return AnnotationWire.ReadString(kind, key) switch
        {
            "goto" => new GoTo(ReadDestination(o["destination"] ?? throw new FieldBagException($"{key} has no destination"), key)),
            "uri" => new Uri(AnnotationWire.ReadString(o["uri"] ?? throw new FieldBagException($"{key} has no uri"), key)),
            "named" => new Named(AnnotationWire.ReadEnum<AnnotationLinkVerb>(o["name"] ?? throw new FieldBagException($"{key} has no name"), key)),
            var other => throw new FieldBagException($"not a link target a draft writes: {other}"),
        };
    }

    static PageDestination ReadDestination(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["kind"] is not { } kind || o["page"] is not { } pageNode) throw new FieldBagException($"{key} is not a destination");
        var page = AnnotationWire.ReadPage(pageNode, key);
        double? Coordinate(string name) => o[name] is { } v ? AnnotationWire.ReadDouble(v, key) : null;
        double Required(string name) => Coordinate(name) ?? throw new FieldBagException($"{key} has no {name}");
        return AnnotationWire.ReadString(kind, key) switch
        {
            "xyz" => new PageDestination.Xyz(page, Coordinate("x"), Coordinate("y"), Coordinate("zoom")),
            "fit" => new PageDestination.Fit(page),
            "fitH" => new PageDestination.FitH(page, Coordinate("y")),
            "fitV" => new PageDestination.FitV(page, Coordinate("x")),
            "fitR" => new PageDestination.FitR(page, Required("x"), Required("y"), Required("width"), Required("height")),
            "fitB" => new PageDestination.FitB(page),
            "fitBH" => new PageDestination.FitBH(page, Coordinate("y")),
            "fitBV" => new PageDestination.FitBV(page, Coordinate("x")),
            var other => throw new FieldBagException($"not a destination kind: {other}"),
        };
    }

    /// <summary>The target in a <c>LinkTarget</c> record the engine returned; <c>operation-failed</c> for one this type can't hold.</summary>
    internal static AnnotationLinkTarget FromEngineJson(string json)
    {
        try
        {
            return Read(JsonNode.Parse(json) ?? throw new FieldBagException("null"), "target");
        }
        catch (Exception e) when (e is FieldBagException or System.Text.Json.JsonException or InvalidOperationException)
        {
            throw new PdfException("DecodingError", PdfErrorCode.OperationFailed, $"the link target doesn't read as an AnnotationLinkTarget: {e.Message}");
        }
    }
}
