using System.Reflection;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Every public member that takes a non-nullable reference throws
/// <see cref="ArgumentNullException"/> for null before doing anything else.
/// </summary>
public sealed class NullArgumentTests
{
    static readonly NullabilityInfoContext Nullability = new();

    static object? Valid(Type type) => type switch
    {
        _ when type == typeof(string) => "x",
        _ when type == typeof(DocumentInfo) => new DocumentInfo("x", null, 0, DocumentStatus.Ready, null),
        _ when type == typeof(Stream) => new MemoryStream(),
        _ when type == typeof(Uri) => new Uri("https://embedpdf.test/x.pdf"),
        _ when type == typeof(IEnumerable<string>) => Array.Empty<string>(),
        _ when type == typeof(IEnumerable<StandardCommandList>) => Array.Empty<StandardCommandList>(),
        _ when type == typeof(CancellationToken) => CancellationToken.None,
        _ when type.IsValueType => Activator.CreateInstance(type),
        _ when type.GetConstructor(Type.EmptyTypes) is { } constructor => constructor.Invoke(null),
        _ when type == typeof(IDocumentFetcher) => new HttpClientDocumentFetcher(),
        _ when type == typeof(Microsoft.Extensions.Logging.ILogger) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
        _ => throw new InvalidOperationException($"no valid value for {type}"),
    };

    /// <summary>Each (method, parameter index) whose parameter is a non-nullable reference.</summary>
    static IEnumerable<(MethodInfo Method, int Index)> Cases(Type type) =>
        from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        where !method.IsSpecialName && method.DeclaringType == type
        let parameters = method.GetParameters()
        from index in Enumerable.Range(0, parameters.Length)
        let parameter = parameters[index]
        where !parameter.ParameterType.IsValueType && Nullability.Create(parameter).WriteState == NullabilityState.NotNull
        select (method, index);

    static async Task<List<string>> Unchecked(object target)
    {
        var missing = new List<string>();
        foreach (var (method, index) in Cases(target.GetType()))
        {
            var arguments = method.GetParameters().Select((p, i) => i == index ? null : Valid(p.ParameterType)).ToArray();
            Exception? error;
            try
            {
                var result = method.Invoke(target, arguments);
                error = result is Task task ? await Record.ExceptionAsync(() => task) : null;
            }
            catch (TargetInvocationException invocation)
            {
                error = invocation.InnerException;
            }
            if (error is not ArgumentNullException) missing.Add($"{method.Name}({method.GetParameters()[index].Name}): {error?.GetType().Name ?? "no error"}");
        }
        return missing;
    }

    [Fact]
    public async Task PdfViewerChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        Assert.Empty(await Unchecked(viewer));
    }

    [Fact]
    public async Task TheBuilderAndTheFetcherCheckEveryReferenceArgument()
    {
        Assert.Empty(await Unchecked(new PdfViewerOptionsBuilder()));
        Assert.Empty(await Unchecked(new HttpClientDocumentFetcher()));
    }
}
