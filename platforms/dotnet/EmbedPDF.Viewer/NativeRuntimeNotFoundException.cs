namespace EmbedPDF;

/// <summary>
/// The native viewer host for the running platform could not be loaded: the
/// EmbedPDF.Runtime package ships no library for <see cref="RuntimeIdentifier"/>,
/// or the library is missing from every place the resolver looked.
/// </summary>
public sealed class NativeRuntimeNotFoundException : PdfException
{
    /// <summary>Creates the exception for a runtime identifier and the paths that were searched.</summary>
    public NativeRuntimeNotFoundException(string runtimeIdentifier, IReadOnlyList<string> searchedPaths, string message)
        : base(message)
    {
        RuntimeIdentifier = runtimeIdentifier;
        SearchedPaths = searchedPaths;
    }

    /// <summary>The runtime identifier of the running process, such as <c>linux-arm64</c>.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>The library paths the resolver tried, in order. Empty for an unsupported runtime.</summary>
    public IReadOnlyList<string> SearchedPaths { get; }
}
