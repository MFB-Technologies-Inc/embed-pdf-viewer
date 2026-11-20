namespace EmbedPDF;

/// <summary>
/// The native viewer host for the running platform could not be loaded: the
/// EmbedPDF.Runtime package ships no library for <see cref="RuntimeIdentifier"/>,
/// the library is missing from every place the resolver looked, or it was
/// found and failed to load (then <see cref="Exception.InnerException"/> holds
/// the loader's error).
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

    /// <summary>Creates the exception for a library that was found but failed to load.</summary>
    public NativeRuntimeNotFoundException(string runtimeIdentifier, IReadOnlyList<string> searchedPaths, string message, Exception innerException)
        : base(message, innerException)
    {
        RuntimeIdentifier = runtimeIdentifier;
        SearchedPaths = searchedPaths;
    }

    /// <summary>The runtime identifier of the running process, such as <c>linux-arm64</c>.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>The library paths the resolver tried, in order. Empty for an unsupported runtime.</summary>
    public IReadOnlyList<string> SearchedPaths { get; }
}
