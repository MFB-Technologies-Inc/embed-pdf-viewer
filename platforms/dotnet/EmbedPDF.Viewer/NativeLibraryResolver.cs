using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EmbedPDF;

/// <summary>Loads native libraries for <see cref="NativeLibraryResolver"/>; a seam for tests.</summary>
internal interface INativeLoader
{
    bool Exists(string path);

    /// <summary>Loads <paramref name="path"/>, throwing the loader's own error on failure.</summary>
    IntPtr Load(string path);
}

internal sealed class SystemNativeLoader : INativeLoader
{
    public bool Exists(string path) => File.Exists(path);
    public IntPtr Load(string path) => NativeLibrary.Load(path);
}

/// <summary>
/// Maps the generated bindings' library name to the EmbedPDF.Runtime layout.
/// The host links PDFium dynamically, so the resolver loads PDFium from the
/// host's own directory first; the packaged host also carries a runpath to
/// its own directory, so either alone is enough.
/// </summary>
internal static class NativeLibraryResolver
{
    internal const string LibraryName = "epdf_viewer_host";

    internal static readonly IReadOnlyList<string> SupportedRuntimes =
        ["linux-x64", "linux-arm64", "osx-arm64", "osx-x64", "win-x64"];

    static readonly object Gate = new();
    static IntPtr loaded;

    // CA2255 warns that module initializers surprise library callers. This
    // one only registers a resolver for this assembly's own P/Invokes, which
    // has to happen before the first generated call.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() =>
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, OnResolve);

    static IntPtr OnResolve(string name, Assembly assembly, DllImportSearchPath? searchPath) =>
        name == LibraryName ? Load(assembly, searchPath) : IntPtr.Zero;

    /// <summary>Loads the host once per process, throwing <see cref="NativeRuntimeNotFoundException"/> when it can't.</summary>
    internal static IntPtr Load(Assembly assembly, DllImportSearchPath? searchPath)
    {
        lock (Gate)
        {
            if (loaded == IntPtr.Zero)
            {
                loaded = Resolve(CurrentRuntimeIdentifier(), AppContext.BaseDirectory, new SystemNativeLoader(),
                    () => NativeLibrary.TryLoad(LibraryName, assembly, searchPath, out var handle) ? handle : IntPtr.Zero);
            }
            return loaded;
        }
    }

    /// <summary>The portable runtime identifier of this process, such as <c>linux-arm64</c>.</summary>
    internal static string CurrentRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";
        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.Ordinal)
                ? $"linux-musl-{arch}" : $"linux-{arch}";
        }
        return $"{RuntimeInformation.OSDescription.Split(' ')[0].ToLowerInvariant()}-{arch}";
    }

    internal static (string Host, string Pdfium) FileNames(string runtimeIdentifier) => runtimeIdentifier switch
    {
        _ when runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal) => ("epdf_viewer_host.dll", "embedpdf.dll"),
        _ when runtimeIdentifier.StartsWith("osx-", StringComparison.Ordinal) => ("libepdf_viewer_host.dylib", "libembedpdf.dylib"),
        _ => ("libepdf_viewer_host.so", "libembedpdf.so"),
    };

    /// <summary>
    /// Finds and loads the host for <paramref name="runtimeIdentifier"/>: in
    /// <paramref name="baseDirectory"/>, where a RID-specific build copies it,
    /// then in <c>runtimes/&lt;rid&gt;/native</c> under it, where a portable
    /// build leaves it, then through <paramref name="fallback"/> (the
    /// runtime's own probing). Throws <see cref="NativeRuntimeNotFoundException"/>
    /// naming the RID when none of them yields a library. When a host file was
    /// there but would not load, the message says so and the loader's errors
    /// are the inner exception (an <see cref="AggregateException"/> when there
    /// were several).
    /// </summary>
    internal static IntPtr Resolve(string runtimeIdentifier, string baseDirectory, INativeLoader loader, Func<IntPtr>? fallback = null)
    {
        if (!SupportedRuntimes.Contains(runtimeIdentifier))
        {
            throw new NativeRuntimeNotFoundException(runtimeIdentifier, [],
                $"EmbedPDF.Runtime ships no native library for runtime '{runtimeIdentifier}'. " +
                $"Supported runtimes: {string.Join(", ", SupportedRuntimes)}.");
        }
        var (host, pdfium) = FileNames(runtimeIdentifier);
        var searched = new List<string>();
        var unloadable = new List<string>();
        var errors = new List<Exception>();
        foreach (var directory in new[] { baseDirectory, Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native") })
        {
            var hostPath = Path.Combine(directory, host);
            searched.Add(hostPath);
            if (!loader.Exists(hostPath)) continue;
            var pdfiumPath = Path.Combine(directory, pdfium);
            try
            {
                // A PDFium that won't load surfaces again as the host's own
                // failure; its error is kept for the message all the same.
                if (loader.Exists(pdfiumPath)) loader.Load(pdfiumPath);
            }
            catch (Exception error) when (error is DllNotFoundException or BadImageFormatException)
            {
                errors.Add(error);
            }
            try
            {
                return loader.Load(hostPath);
            }
            catch (Exception error) when (error is DllNotFoundException or BadImageFormatException)
            {
                unloadable.Add(hostPath);
                errors.Add(error);
            }
        }
        var probed = fallback?.Invoke() ?? IntPtr.Zero;
        if (probed != IntPtr.Zero) return probed;
        if (unloadable.Count > 0)
        {
            Exception inner = errors.Count == 1 ? errors[0] : new AggregateException(errors);
            throw new NativeRuntimeNotFoundException(runtimeIdentifier, searched,
                $"The EmbedPDF native library for runtime '{runtimeIdentifier}' was found but failed to load: " +
                $"{string.Join(", ", unloadable)}. The inner exception has the loader's error.", inner);
        }
        throw new NativeRuntimeNotFoundException(runtimeIdentifier, searched,
            $"No EmbedPDF native library could be loaded for runtime '{runtimeIdentifier}'. " +
            $"Reference EmbedPDF.Runtime, which ships {host} and {pdfium} for this runtime, " +
            $"or put both beside the application. Searched: {string.Join(", ", searched)}.");
    }
}
