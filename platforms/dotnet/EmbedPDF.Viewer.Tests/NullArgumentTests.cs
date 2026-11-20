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
        _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) => Array.CreateInstance(type.GetGenericArguments()[0], 0),
        _ when type == typeof(IReadOnlyDictionary<string, string?>) => new Dictionary<string, string?>(),
        _ when type == typeof(SearchQuery) => new SearchQuery("x"),
        _ when type == typeof(AttachedFile) => new AttachedFile("x", new AttachmentHome.Document(new AttachmentRef("x")), "x", null, null, null, null, null, null),
        _ when type == typeof(PageEditPlacement) => new PageEditPlacement.End(),
        _ when type == typeof(OutlinePosition) => new OutlinePosition.End(),
        _ when type == typeof(RenderPageViewDemand) => new RenderPageViewDemand(80),
        _ when type == typeof(ShellSnapshot) => new ShellSnapshot([], []),
        _ when type == typeof(StyleProps) => new StyleProps("a", 1),
        _ when type == typeof(System.Text.Json.Serialization.Metadata.JsonTypeInfo<StyleProps>) => ShellJson.Default.StyleProps,
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
        let closed = method.IsGenericMethodDefinition ? method.MakeGenericMethod(typeof(StyleProps)) : method
        let parameters = closed.GetParameters()
        from index in Enumerable.Range(0, parameters.Length)
        let parameter = parameters[index]
        where !parameter.ParameterType.IsValueType && Nullability.Create(parameter).WriteState == NullabilityState.NotNull
        select (closed, index);

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
    public async Task EveryWaveACapabilityChecksEveryReferenceArgument()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Metadata = true, Search = new(), Render = new(), History = true, Outline = true, Attachment = true,
            PageEdit = true, ViewManager = true, Shell = true,
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var render = await viewer.GetRenderAsync(document);
        object[] targets =
        [
            await viewer.GetMetadataAsync(document), await viewer.GetSearchAsync(document), render, await viewer.GetHistoryAsync(document),
            await viewer.GetOutlineAsync(document), await viewer.GetAttachmentsAsync(document), await viewer.GetPageEditAsync(document),
            await viewer.GetViewManagerAsync(), await viewer.GetShellAsync(document), await render.CreateViewDemandAsync("main"),
        ];
        foreach (var target in targets) Assert.Empty(await Unchecked(target));
        Assert.Empty(await Unchecked(new PixelBuffer(new byte[4], 1, 1, 4)));
    }

    /// <summary>Each (method, parameter index) whose parameter is a collection of a reference type.</summary>
    static IEnumerable<(MethodInfo Method, int Index, Type Element)> CollectionCases(Type type) =>
        from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        where !method.IsSpecialName && !method.IsGenericMethodDefinition
        let parameters = method.GetParameters()
        from index in Enumerable.Range(0, parameters.Length)
        let parameter = parameters[index].ParameterType
        where parameter.IsGenericType && parameter.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        let element = parameter.GetGenericArguments()[0]
        where !element.IsValueType
        select (method, index, element);

    [Fact]
    public async Task EveryWaveACapabilityRefusesANullElement()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Attachment = true, ViewManager = true, Shell = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var shell = await viewer.GetShellAsync(document);
        object[] targets = [await viewer.GetAttachmentsAsync(document), await viewer.GetViewManagerAsync(), shell];
        var missing = new List<string>();
        var checkedCount = 0;
        foreach (var target in targets)
        {
            foreach (var (method, index, element) in CollectionCases(target.GetType()))
            {
                checkedCount++;
                var withNull = Array.CreateInstance(element, 1);
                var arguments = method.GetParameters().Select((p, i) => i == index ? withNull : Valid(p.ParameterType)).ToArray();
                Exception? error;
                try
                {
                    // Up front: the call itself throws, before any task exists.
                    _ = method.Invoke(target, arguments);
                    error = null;
                }
                catch (TargetInvocationException invocation)
                {
                    error = invocation.InnerException;
                }
                if (error is not ArgumentException) missing.Add($"{target.GetType().Name}.{method.Name}: {error?.GetType().Name ?? "no error"}");
            }
        }
        Assert.Empty(missing);
        Assert.Equal(3, checkedCount);
        Assert.Throws<ArgumentException>(() => { _ = shell.ApplySnapshotAsync(new ShellSnapshot([null!], [])); });
        Assert.Throws<ArgumentException>(() => { _ = shell.ApplySnapshotAsync(new ShellSnapshot([], [null!])); });
    }

    [Fact]
    public async Task TheBuilderAndTheFetcherCheckEveryReferenceArgument()
    {
        Assert.Empty(await Unchecked(new PdfViewerOptionsBuilder()));
        Assert.Empty(await Unchecked(new HttpClientDocumentFetcher()));
    }
}
