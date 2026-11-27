using uniffi.epdf_ffi;
using uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>
/// The native viewer host this assembly runs on. Use <see cref="EnsureLoaded"/>
/// to fail early, with a <see cref="NativeRuntimeNotFoundException"/> naming the
/// runtime, when the host can't be found.
/// </summary>
public static class NativeRuntime
{
    /// <summary>The runtime identifier the resolver looks for, such as <c>linux-arm64</c>.</summary>
    public static string RuntimeIdentifier => NativeLibraryResolver.CurrentRuntimeIdentifier();

    /// <summary>Loads the native host now rather than on first use.</summary>
    /// <exception cref="NativeRuntimeNotFoundException">No host for this runtime could be loaded.</exception>
    public static void EnsureLoaded()
    {
        if (LoadOverride.Value is { } load)
        {
            load();
            return;
        }
        NativeLibraryResolver.Load(typeof(NativeRuntime).Assembly, null);
    }

    /// <summary>A test seam: when set, runs in place of the real load on this async flow.</summary>
    internal static readonly AsyncLocal<Action?> LoadOverride = new();

    /// <summary>
    /// A load check: opens <paramref name="pdf"/> in a throwaway viewer and
    /// returns its page count. It proves the native host and its PDFium load
    /// and work; it is not the document API.
    /// </summary>
    /// <exception cref="NativeRuntimeNotFoundException">No host for this runtime could be loaded.</exception>
    public static async Task<int> ReadPageCountAsync(ReadOnlyMemory<byte> pdf, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLoaded();
        using var host = new ViewerHost(new ViewerConfig(), new SilentDelegate());
        await host.Start().ConfigureAwait(false);
        try
        {
            var info = await host.OpenDocument(pdf.ToArray(), new uniffi.epdf_viewer_host.OpenDocumentOptions()).ConfigureAwait(false);
            return checked((int)info.PageCount);
        }
        finally
        {
            await host.DestroyViewer().ConfigureAwait(false);
            host.Shutdown();
        }
    }

    sealed class SilentDelegate : ViewerDelegate
    {
        public void StateChanged(ulong revision) { }
        public void Report(ViewerException error) { }
    }
}
