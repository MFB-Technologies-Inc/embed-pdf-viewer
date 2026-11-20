using System.Reflection;
using System.Text.Json;
using EmbedPDF;
using Microsoft.Extensions.DependencyInjection;

// Prints {"members": [...]}: each public type's full name, and
// "<type>.<member>" for each public member it declares. Nested types use a
// dot, as C# spells them. Loading the assemblies loads no native code.
var assemblies = new[] { typeof(PdfViewer).Assembly, typeof(EmbedPdfServiceCollectionExtensions).Assembly };
var names = new SortedSet<string>(StringComparer.Ordinal);
foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
{
    var typeName = type.FullName!.Replace('+', '.');
    names.Add(typeName);
    const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    foreach (var member in type.GetMembers(flags))
    {
        names.Add($"{typeName}.{member.Name}");
    }
}
using var output = Console.OpenStandardOutput();
using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
writer.WriteStartObject();
writer.WriteStartArray("members");
foreach (var name in names)
{
    writer.WriteStringValue(name);
}
writer.WriteEndArray();
writer.WriteEndObject();
