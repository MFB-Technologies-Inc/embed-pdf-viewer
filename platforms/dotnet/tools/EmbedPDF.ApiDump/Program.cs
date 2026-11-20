using System.Reflection;
using System.Text.Json;
using EmbedPDF;
using Microsoft.Extensions.DependencyInjection;

// Prints {"members": [...], "signatures": [...]}. members holds each public
// type's full name and "<type>.<member>" for each public member it declares;
// signatures holds "<type>.<method>(<parameter types>)" for each public method,
// so the coverage check can name one overload. Nested types use a dot and
// generic types their arguments in angle brackets, as C# spells them.
// Loading the assemblies loads no native code.
var assemblies = new[] { typeof(PdfViewer).Assembly, typeof(EmbedPdfServiceCollectionExtensions).Assembly };
var names = new SortedSet<string>(StringComparer.Ordinal);
var signatures = new SortedSet<string>(StringComparer.Ordinal);
const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
{
    var typeName = Name(type);
    names.Add(typeName);
    foreach (var member in type.GetMembers(Flags))
    {
        names.Add($"{typeName}.{member.Name}");
    }
    foreach (var method in type.GetMethods(Flags))
    {
        signatures.Add($"{typeName}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => Name(p.ParameterType)))})");
    }
}
using var output = Console.OpenStandardOutput();
using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
writer.WriteStartObject();
Write("members", names);
Write("signatures", signatures);
writer.WriteEndObject();

void Write(string property, IEnumerable<string> values)
{
    writer.WriteStartArray(property);
    foreach (var value in values)
    {
        writer.WriteStringValue(value);
    }
    writer.WriteEndArray();
}

static string Name(Type type)
{
    if (type.IsByRef) return Name(type.GetElementType()!) + "&";
    if (type.IsArray) return Name(type.GetElementType()!) + "[]";
    if (type.IsGenericParameter) return type.Name;
    var full = (type.IsGenericType ? type.GetGenericTypeDefinition().FullName! : type.FullName ?? type.Name).Replace('+', '.');
    if (!type.IsGenericType) return full;
    var tick = full.IndexOf('`', StringComparison.Ordinal);
    return $"{full[..tick]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";
}
