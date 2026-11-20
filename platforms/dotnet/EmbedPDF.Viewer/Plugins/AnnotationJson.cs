using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EmbedPDF;

/// <summary>
/// A value, or JSON <c>null</c>: a draft or patch member the engine can set
/// to nothing. A member typed <c>JsonNullable&lt;T&gt;?</c> has three states:
/// <see langword="null"/> leaves it out of the JSON (on a draft the engine's
/// default, on a patch the current value), <see cref="Null"/> writes
/// <c>null</c> (no stroke colour, no dash pattern, no rotation), and a value
/// writes the value. A value converts implicitly:
/// <c>Rotation = 90</c>, <c>Contents = "Note"</c>.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "JsonNullable.Of is the named alternate.")]
public readonly struct JsonNullable<T> : IEquatable<JsonNullable<T>> where T : notnull
{
    readonly T? value;
    readonly bool hasValue;

    internal JsonNullable(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
        hasValue = true;
    }

    /// <summary>JSON <c>null</c>: nothing.</summary>
    public static JsonNullable<T> Null => default;

    /// <summary>Whether this is <see cref="Null"/>.</summary>
    public bool IsNull => !hasValue;

    /// <summary>The value. Throws <see cref="InvalidOperationException"/> on <see cref="Null"/>.</summary>
    public T Value => hasValue ? value! : throw new InvalidOperationException("The value is JSON null.");

    /// <summary>The value.</summary>
    public static implicit operator JsonNullable<T>(T value) => new(value);

    /// <inheritdoc/>
    public bool Equals(JsonNullable<T> other) =>
        hasValue == other.hasValue && (!hasValue || EqualityComparer<T>.Default.Equals(value, other.value));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is JsonNullable<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hasValue ? HashCode.Combine(true, value) : 0;

    /// <summary>The value's text, or <c>null</c>.</summary>
    public override string ToString() => hasValue ? value!.ToString() ?? string.Empty : "null";

    /// <summary>Whether both hold the same value, or are both <see cref="Null"/>.</summary>
    public static bool operator ==(JsonNullable<T> left, JsonNullable<T> right) => left.Equals(right);

    /// <summary>Whether they differ.</summary>
    public static bool operator !=(JsonNullable<T> left, JsonNullable<T> right) => !left.Equals(right);
}

/// <summary>Builds <see cref="JsonNullable{T}"/> values where the implicit conversion doesn't apply (an interface-typed value).</summary>
public static class JsonNullable
{
    /// <summary>The value.</summary>
    public static JsonNullable<T> Of<T>(T value) where T : notnull => new(value);
}

// ── wire records, serialized by the source-generated context ────────────────

internal sealed record WirePoint(double X, double Y);

internal sealed record WireRect(double X, double Y, double Width, double Height);

internal sealed record WireQuad(WirePoint UpperLeft, WirePoint UpperRight, WirePoint LowerLeft, WirePoint LowerRight);

internal sealed record WireLinePoints(WirePoint Start, WirePoint End);

internal sealed record WireOrigin(double X, double Y);

internal sealed record WireNumberFormat(
    string Unit, double? Conversion = null, string? Fraction = null, double? Precision = null, bool? Fixed = null,
    string? Thousands = null, string? Decimal = null, string? PrefixSpacing = null, string? SuffixSpacing = null,
    string? LabelPosition = null);

internal sealed record WireMeasurement(
    string Subtype, string? Ratio = null, WireNumberFormat[]? X = null, WireNumberFormat[]? Y = null,
    WireNumberFormat[]? Distance = null, WireNumberFormat[]? Area = null, WireNumberFormat[]? Angle = null,
    WireNumberFormat[]? Slope = null, WireOrigin? Origin = null, double? Cyx = null);

internal sealed record WireCaptionOffset(double Along, double Perpendicular);

internal sealed record WireLineLeader(double Length, double? Extension = null, double? Offset = null);

internal sealed record WireAttachedFile(
    string Name, string? MimeType = null, string? Description = null, long? SizeBytes = null, string? Md5 = null,
    string? CreatedAt = null, string? ModifiedAt = null);

/// <summary>
/// The annotation wire's leaf records. Reflection-free, so trimming and
/// NativeAOT keep them. Constructor parameters are required, so a read
/// record missing one fails as Swift's <c>Decodable</c> does.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectRequiredConstructorParameters = true,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(WirePoint))]
[JsonSerializable(typeof(WirePoint[]))]
[JsonSerializable(typeof(WirePoint[][]))]
[JsonSerializable(typeof(WireRect))]
[JsonSerializable(typeof(WireQuad))]
[JsonSerializable(typeof(WireQuad[]))]
[JsonSerializable(typeof(WireLinePoints))]
[JsonSerializable(typeof(WireMeasurement))]
[JsonSerializable(typeof(WireCaptionOffset))]
[JsonSerializable(typeof(WireLineLeader))]
[JsonSerializable(typeof(WireAttachedFile))]
[JsonSerializable(typeof(double[]))]
internal sealed partial class AnnotationJsonContext : JsonSerializerContext;

/// <summary>Thrown inside a field-bag read or write; the entry points turn it into a <see cref="PdfException"/>.</summary>
internal sealed class FieldBagException(string message) : Exception(message);

/// <summary>
/// Writes an engine field bag: a JSON object, keys sorted ordinally at every
/// level, so the same draft is always the same bytes, with no escaping beyond
/// what JSON needs (Swift's <c>.sortedKeys, .withoutEscapingSlashes</c>).
/// </summary>
internal sealed class FieldBag
{
    static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    readonly JsonObject fields = [];

    internal void Set(string key, JsonNode? node) => fields[key] = node;

    internal void Put(string key, string? value)
    {
        if (value is not null) fields[key] = JsonValue.Create(value);
    }

    internal void Put(string key, double? value)
    {
        if (value is { } v) fields[key] = AnnotationWire.Number(v, key);
    }

    internal void Put(string key, bool? value)
    {
        if (value is { } v) fields[key] = JsonValue.Create(v);
    }

    internal void Put<T>(string key, T? value) where T : struct, Enum
    {
        if (value is { } v) fields[key] = JsonValue.Create(Wire<T>.Name(v, key));
    }

    internal void Put(string key, AnnotationColor? value)
    {
        if (value is { } v) fields[key] = JsonValue.Create(v.Hex);
    }

    internal void Put(string key, PdfRect? value)
    {
        if (value is { } v) fields[key] = AnnotationWire.Rect(v, key);
    }

    internal void Put<T>(string key, JsonNullable<T>? value, Func<T, JsonNode?> map) where T : notnull
    {
        if (value is { } v) fields[key] = v.IsNull ? null : map(v.Value);
    }

    internal void Put(string key, JsonNullable<string>? value) => Put(key, value, s => JsonValue.Create(s));

    internal void Put(string key, JsonNullable<double>? value) => Put(key, value, d => AnnotationWire.Number(d, key));

    internal void Put(string key, JsonNullable<bool>? value) => Put(key, value, b => JsonValue.Create(b));

    internal void Put(string key, JsonNullable<AnnotationColor>? value) => Put(key, value, c => JsonValue.Create(c.Hex));

    internal void PutEnum<T>(string key, JsonNullable<T>? value) where T : struct, Enum =>
        Put(key, value, e => JsonValue.Create(Wire<T>.Name(e, key)));

    internal string ToJson()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options)) Write(writer, fields);
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static string Sorted(JsonNode? node)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options)) Write(writer, node);
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject o:
                writer.WriteStartObject();
                foreach (var (key, value) in o.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }
                writer.WriteEndObject();
                break;
            case JsonArray a:
                writer.WriteStartArray();
                foreach (var item in a) Write(writer, item);
                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer);
                break;
        }
    }

    /// <summary>Runs a write, turning a value JSON can't hold into <c>invalid-input</c>.</summary>
    internal static string Encode(Action<FieldBag> write)
    {
        try
        {
            var bag = new FieldBag();
            write(bag);
            return bag.ToJson();
        }
        catch (FieldBagException e)
        {
            throw new PdfException("EncodingError", PdfErrorCode.InvalidInput, $"the annotation fields don't encode as JSON: {e.Message}");
        }
    }
}

/// <summary>Reads an engine field bag, as Swift's <c>Decodable</c> patches do.</summary>
internal sealed class FieldReader(JsonObject fields)
{
    internal JsonObject Fields => fields;

    internal bool Has(string key) => fields.ContainsKey(key);

    /// <summary>The member's node: absent and <c>null</c> are both <see langword="null"/>.</summary>
    internal JsonNode? Node(string key) => fields.TryGetPropertyValue(key, out var node) ? node : null;

    internal T? Value<T>(string key, Func<JsonNode, string, T> read) where T : class =>
        Node(key) is { } node ? read(node, key) : null;

    internal T? Struct<T>(string key, Func<JsonNode, string, T> read) where T : struct =>
        Node(key) is { } node ? read(node, key) : null;

    internal JsonNullable<T>? Nullable<T>(string key, Func<JsonNode, string, T> read) where T : notnull
    {
        if (!fields.TryGetPropertyValue(key, out var node)) return null;
        return node is null ? JsonNullable<T>.Null : new JsonNullable<T>(read(node, key));
    }

    internal string? String(string key) => Value(key, AnnotationWire.ReadString);

    internal double? Double(string key) => Struct(key, AnnotationWire.ReadDouble);

    internal bool? Bool(string key) => Struct(key, AnnotationWire.ReadBool);

    internal T? Enum<T>(string key) where T : struct, System.Enum => Struct(key, AnnotationWire.ReadEnum<T>);

    internal AnnotationColor? Color(string key) => Struct(key, AnnotationWire.ReadColor);

    internal PdfRect? Rect(string key) => Struct(key, AnnotationWire.ReadRect);

    internal JsonNullable<string>? NullableString(string key) => Nullable(key, AnnotationWire.ReadString);

    internal JsonNullable<double>? NullableDouble(string key) => Nullable(key, AnnotationWire.ReadDouble);

    internal JsonNullable<bool>? NullableBool(string key) => Nullable(key, AnnotationWire.ReadBool);

    internal JsonNullable<AnnotationColor>? NullableColor(string key) => Nullable(key, AnnotationWire.ReadColor);

    internal JsonNullable<T>? NullableEnum<T>(string key) where T : struct, System.Enum => Nullable(key, AnnotationWire.ReadEnum<T>);

    internal JsonNullable<IReadOnlyList<double>>? NullableNumbers(string key) => Nullable(key, AnnotationWire.ReadNumbers);

    /// <summary>Parses a patch, turning a member of the wrong shape into <c>invalid-input</c>.</summary>
    internal static T Decode<T>(string json, Func<FieldReader, T> read)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            if (JsonNode.Parse(json) is not JsonObject fields) throw new FieldBagException("a field bag is a JSON object");
            return read(new FieldReader(fields));
        }
        catch (Exception e) when (e is FieldBagException or JsonException or InvalidOperationException or FormatException)
        {
            throw new PdfException("DecodingError", PdfErrorCode.InvalidInput, $"the fields don't read as {typeof(T).Name}: {e.Message}");
        }
    }
}

/// <summary>The leaf values of the annotation wire, written and read.</summary>
internal static class AnnotationWire
{
    static AnnotationJsonContext Context => AnnotationJsonContext.Default;

    internal static JsonNode Number(double value, string key) =>
        double.IsFinite(value) ? JsonValue.Create(value) : throw new FieldBagException($"{key} is {value}, which JSON can't hold");

    static double Finite(double value, string key) =>
        double.IsFinite(value) ? value : throw new FieldBagException($"{key} is {value}, which JSON can't hold");

    internal static WirePoint ToWire(PdfPoint p, string key) => new(Finite(p.X, key), Finite(p.Y, key));

    internal static JsonNode Point(PdfPoint p, string key) => JsonSerializer.SerializeToNode(ToWire(p, key), Context.WirePoint)!;

    internal static JsonNode Rect(PdfRect r, string key) =>
        JsonSerializer.SerializeToNode(new WireRect(Finite(r.X, key), Finite(r.Y, key), Finite(r.Width, key), Finite(r.Height, key)), Context.WireRect)!;

    internal static JsonNode Points(IEnumerable<PdfPoint> points, string key) =>
        JsonSerializer.SerializeToNode([.. points.Select(p => ToWire(p, key))], Context.WirePointArray)!;

    internal static JsonNode PointLists(IEnumerable<IEnumerable<PdfPoint>> lists, string key) =>
        JsonSerializer.SerializeToNode([.. lists.Select(l => (WirePoint[])[.. (l ?? throw new FieldBagException($"{key} holds a null stroke")).Select(p => ToWire(p, key))])], Context.WirePointArrayArray)!;

    internal static JsonNode Quads(IEnumerable<PdfQuad> quads, string key) =>
        JsonSerializer.SerializeToNode(
            [.. quads.Select(q => new WireQuad(ToWire(q.UpperLeft, key), ToWire(q.UpperRight, key), ToWire(q.LowerLeft, key), ToWire(q.LowerRight, key)))],
            Context.WireQuadArray)!;

    internal static JsonNode LinePoints(PdfPoint start, PdfPoint end, string key) =>
        JsonSerializer.SerializeToNode(new WireLinePoints(ToWire(start, key), ToWire(end, key)), Context.WireLinePoints)!;

    internal static JsonNode Numbers(IEnumerable<double> values, string key) =>
        JsonSerializer.SerializeToNode([.. values.Select(v => Finite(v, key))], Context.DoubleArray)!;

    internal static JsonNode Page(PageRef page) => new JsonObject
    {
        ["kind"] = "objectNumber",
        ["objectNumber"] = WireInteger.ToUInt32(page.PageObjectNumber, "page object number"),
    };

    internal static JsonNode Annotation(AnnotationRef annotation, string key)
    {
        ArgumentNullException.ThrowIfNull(annotation, key);
        return annotation switch
        {
            AnnotationRef.ObjectNumber n => new JsonObject
            {
                ["kind"] = "objectNumber",
                ["page"] = Page(n.Page),
                ["objectNumber"] = WireInteger.ToUInt32(n.AnnotObjectNumber, "annotation object number"),
            },
            AnnotationRef.Index i => new JsonObject
            {
                ["kind"] = "baseIndex",
                ["page"] = Page(i.Page),
                ["baseIndex"] = WireInteger.ToUInt32(i.Value, "annotation index"),
            },
            _ => throw Ffi.Unknown(annotation),
        };
    }

    // ── reads ───────────────────────────────────────────────────────────

    static FieldBagException Shape(string key, string what) => new($"{key} is not {what}");

    internal static string ReadString(JsonNode node, string key) =>
        node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : throw Shape(key, "a string");

    internal static double ReadDouble(JsonNode node, string key) =>
        node.GetValueKind() == JsonValueKind.Number ? node.GetValue<double>() : throw Shape(key, "a number");

    internal static bool ReadBool(JsonNode node, string key) => node.GetValueKind() switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw Shape(key, "a Boolean"),
    };

    internal static T ReadEnum<T>(JsonNode node, string key) where T : struct, Enum =>
        Wire<T>.TryParse(ReadString(node, key), out var value) ? value : throw Shape(key, $"a {typeof(T).Name}");

    internal static AnnotationColor ReadColor(JsonNode node, string key) =>
        AnnotationColor.TryParse(ReadString(node, key), out var color) ? color : throw Shape(key, "a colour as \"#rrggbb\"");

    static T Read<T>(JsonNode node, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, string key, string what)
    {
        try
        {
            return node.Deserialize(info) ?? throw Shape(key, what);
        }
        catch (JsonException e)
        {
            throw new FieldBagException($"{key} is not {what}: {e.Message}");
        }
    }

    internal static PdfPoint ReadPoint(JsonNode node, string key)
    {
        var p = Read(node, Context.WirePoint, key, "a point");
        return new(p.X, p.Y);
    }

    internal static PdfRect ReadRect(JsonNode node, string key)
    {
        var r = Read(node, Context.WireRect, key, "a rect");
        return new(r.X, r.Y, r.Width, r.Height);
    }

    internal static IReadOnlyList<PdfPoint> ReadPoints(JsonNode node, string key) =>
        [.. Read(node, Context.WirePointArray, key, "a list of points").Select(p => new PdfPoint(p.X, p.Y))];

    internal static IReadOnlyList<IReadOnlyList<PdfPoint>> ReadPointLists(JsonNode node, string key) =>
        [.. Read(node, Context.WirePointArrayArray, key, "a list of strokes").Select(l => (IReadOnlyList<PdfPoint>)[.. (l ?? throw Shape(key, "a list of strokes")).Select(p => new PdfPoint(p.X, p.Y))])];

    internal static IReadOnlyList<PdfQuad> ReadQuads(JsonNode node, string key) =>
        [.. Read(node, Context.WireQuadArray, key, "a list of quads").Select(q => new PdfQuad(
            new(q.UpperLeft.X, q.UpperLeft.Y), new(q.UpperRight.X, q.UpperRight.Y), new(q.LowerLeft.X, q.LowerLeft.Y), new(q.LowerRight.X, q.LowerRight.Y)))];

    internal static (PdfPoint Start, PdfPoint End) ReadLinePoints(JsonNode node, string key)
    {
        var l = Read(node, Context.WireLinePoints, key, "a line's two points");
        return (new(l.Start.X, l.Start.Y), new(l.End.X, l.End.Y));
    }

    internal static IReadOnlyList<double> ReadNumbers(JsonNode node, string key) => Read(node, Context.DoubleArray, key, "a list of numbers");

    internal static WireMeasurement ReadMeasurement(JsonNode node, string key) => Read(node, Context.WireMeasurement, key, "a measure");

    internal static WireCaptionOffset ReadCaptionOffset(JsonNode node, string key) => Read(node, Context.WireCaptionOffset, key, "a caption offset");

    internal static WireLineLeader ReadLineLeader(JsonNode node, string key) => Read(node, Context.WireLineLeader, key, "a leader");

    internal static WireAttachedFile ReadAttachedFile(JsonNode node, string key) => Read(node, Context.WireAttachedFile, key, "an attached file");

    internal static JsonNode Serialize(WireMeasurement measure) => JsonSerializer.SerializeToNode(measure, Context.WireMeasurement)!;

    internal static JsonNode Serialize(WireCaptionOffset offset) => JsonSerializer.SerializeToNode(offset, Context.WireCaptionOffset)!;

    internal static JsonNode Serialize(WireLineLeader leader) => JsonSerializer.SerializeToNode(leader, Context.WireLineLeader)!;

    internal static PageRef ReadPage(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["objectNumber"] is not { } number) throw Shape(key, "a page");
        return new(WireInteger.ToInt((uint)ReadDouble(number, key), "page object number"));
    }

    internal static AnnotationRef ReadAnnotation(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["page"] is not { } pageNode || o["kind"] is not { } kindNode) throw Shape(key, "an annotation ref");
        var page = ReadPage(pageNode, key);
        uint Number(string name) => o[name] is { } n ? checked((uint)ReadDouble(n, key)) : throw Shape(key, "an annotation ref");
        return ReadString(kindNode, key) switch
        {
            "objectNumber" => new AnnotationRef.ObjectNumber(page, WireInteger.ToInt(Number("objectNumber"), "annotation object number")),
            "baseIndex" => new AnnotationRef.Index(page, WireInteger.ToInt(Number("baseIndex"), "annotation index")),
            var kind => throw new FieldBagException($"not an annotation ref kind: {kind}"),
        };
    }
}

/// <summary>The wire spellings of a closed annotation vocabulary, indexed by the enum's value.</summary>
internal static class Wire<T> where T : struct, Enum
{
    internal static readonly string[] Names = AnnotationVocabulary.NamesOf(typeof(T));

    internal static string Name(T value, string what)
    {
        var index = Unsafe.As<T, int>(ref value);
        return (uint)index < (uint)Names.Length ? Names[index] : throw new FieldBagException($"{what} {value} is not a {typeof(T).Name}");
    }

    internal static bool TryParse(string text, out T value)
    {
        var index = Array.IndexOf(Names, text);
        value = Unsafe.As<int, T>(ref index);
        return index >= 0;
    }
}
