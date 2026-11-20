using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class NativeLibraryResolverTests
{
    sealed class FakeLoader(params string[] present) : INativeLoader
    {
        readonly HashSet<string> files = [.. present];
        public readonly List<string> Loaded = [];
        public int Calls;
        public bool Exists(string path)
        {
            Calls++;
            return files.Contains(path);
        }
        public bool TryLoad(string path, out IntPtr handle)
        {
            Calls++;
            Loaded.Add(path);
            handle = files.Contains(path) ? new IntPtr(Loaded.Count) : IntPtr.Zero;
            return handle != IntPtr.Zero;
        }
    }

    static readonly string Base = Path.Combine(Path.GetTempPath(), "epdf-app");

    [Fact]
    public void MissingLibraryThrowsTypedExceptionNamingRid()
    {
        var error = Assert.Throws<NativeRuntimeNotFoundException>(() =>
            NativeLibraryResolver.Resolve("linux-arm64", Base, new FakeLoader()));
        Assert.Equal("linux-arm64", error.RuntimeIdentifier);
        Assert.Contains("'linux-arm64'", error.Message);
        Assert.Equal(
            [Path.Combine(Base, "libepdf_viewer_host.so"),
             Path.Combine(Base, "runtimes", "linux-arm64", "native", "libepdf_viewer_host.so")],
            error.SearchedPaths);
    }

    [Fact]
    public void FallbackRunsBeforeTheError()
    {
        var calls = 0;
        var handle = NativeLibraryResolver.Resolve("win-x64", Base, new FakeLoader(), () => { calls++; return new IntPtr(42); });
        Assert.Equal(new IntPtr(42), handle);
        Assert.Equal(1, calls);
        Assert.Throws<NativeRuntimeNotFoundException>(() =>
            NativeLibraryResolver.Resolve("win-x64", Base, new FakeLoader(), () => IntPtr.Zero));
    }

    [Fact]
    public void UnsupportedRidThrowsAtOnce()
    {
        var loader = new FakeLoader();
        var error = Assert.Throws<NativeRuntimeNotFoundException>(() =>
            NativeLibraryResolver.Resolve("linux-musl-x64", Base, loader));
        Assert.Equal("linux-musl-x64", error.RuntimeIdentifier);
        Assert.Contains("linux-musl-x64", error.Message);
        Assert.Empty(error.SearchedPaths);
        Assert.Equal(0, loader.Calls);
    }

    [Fact]
    public void PreloadsPdfiumBesideHostInPackageLayout()
    {
        var native = Path.Combine(Base, "runtimes", "linux-x64", "native");
        var loader = new FakeLoader(Path.Combine(native, "libembedpdf.so"), Path.Combine(native, "libepdf_viewer_host.so"));
        var handle = NativeLibraryResolver.Resolve("linux-x64", Base, loader);
        Assert.Equal([Path.Combine(native, "libembedpdf.so"), Path.Combine(native, "libepdf_viewer_host.so")], loader.Loaded);
        Assert.Equal(new IntPtr(2), handle);
    }

    [Fact]
    public void PrefersApplicationDirectory()
    {
        var loader = new FakeLoader(
            Path.Combine(Base, "libepdf_viewer_host.dylib"), Path.Combine(Base, "libembedpdf.dylib"),
            Path.Combine(Base, "runtimes", "osx-arm64", "native", "libepdf_viewer_host.dylib"));
        NativeLibraryResolver.Resolve("osx-arm64", Base, loader);
        Assert.Equal([Path.Combine(Base, "libembedpdf.dylib"), Path.Combine(Base, "libepdf_viewer_host.dylib")], loader.Loaded);
    }

    [Fact]
    public void IsAssignableToPdfException()
    {
        PdfException error = new NativeRuntimeNotFoundException("osx-x64", [], "missing");
        Assert.IsType<NativeRuntimeNotFoundException>(error);
    }

    [Fact]
    public void CurrentRuntimeIsSupportedHere()
    {
        Assert.Contains(NativeRuntime.RuntimeIdentifier, NativeLibraryResolver.SupportedRuntimes);
    }

    [Fact]
    public async Task ReadPageCountLoadsTheHost()
    {
        var pdf = ViewerHostTests.FixtureBytes("pdfium-multiplatform-prototype/fixtures/report.pdf");
        Assert.Equal(14, await NativeRuntime.ReadPageCountAsync(pdf));
    }
}
