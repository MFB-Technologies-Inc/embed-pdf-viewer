using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class NativeLibraryResolverTests
{
    sealed class FakeLoader(params string[] present) : INativeLoader
    {
        readonly HashSet<string> files = [.. present];
        public readonly HashSet<string> Broken = [];
        public readonly List<string> Loaded = [];
        public int Calls;
        public bool Exists(string path)
        {
            Calls++;
            return files.Contains(path);
        }
        public IntPtr Load(string path)
        {
            Calls++;
            Loaded.Add(path);
            if (Broken.Contains(path)) throw new BadImageFormatException($"broken: {path}");
            if (!files.Contains(path)) throw new DllNotFoundException($"missing: {path}");
            return new IntPtr(Loaded.Count);
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
    public void FoundButUnloadableHostKeepsTheLoaderError()
    {
        var rid = NativeRuntime.RuntimeIdentifier;
        var directory = Directory.CreateTempSubdirectory("epdf-broken-");
        try
        {
            var host = Path.Combine(directory.FullName, NativeLibraryResolver.FileNames(rid).Host);
            File.WriteAllText(host, "not a native library");
            var error = Assert.Throws<NativeRuntimeNotFoundException>(() =>
                NativeLibraryResolver.Resolve(rid, directory.FullName, new SystemNativeLoader()));
            Assert.Contains("found but failed to load", error.Message);
            Assert.Contains($"'{rid}'", error.Message);
            Assert.Contains(host, error.Message);
            Assert.NotNull(error.InnerException);
            Assert.IsNotType<AggregateException>(error.InnerException);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void SeveralLoadFailuresAreAggregated()
    {
        var native = Path.Combine(Base, "runtimes", "linux-x64", "native");
        var hosts = new[] { Path.Combine(Base, "libepdf_viewer_host.so"), Path.Combine(native, "libepdf_viewer_host.so") };
        var loader = new FakeLoader(hosts);
        loader.Broken.UnionWith(hosts);
        var error = Assert.Throws<NativeRuntimeNotFoundException>(() =>
            NativeLibraryResolver.Resolve("linux-x64", Base, loader, () => IntPtr.Zero));
        Assert.Contains("found but failed to load", error.Message);
        var inner = Assert.IsType<AggregateException>(error.InnerException);
        Assert.Equal(2, inner.InnerExceptions.Count);
        Assert.All(inner.InnerExceptions, e => Assert.IsType<BadImageFormatException>(e));
    }

    [Fact]
    public void MissingFilesAreNotLoadFailures()
    {
        var error = Assert.Throws<NativeRuntimeNotFoundException>(() =>
            NativeLibraryResolver.Resolve("linux-arm64", Base, new FakeLoader()));
        Assert.DoesNotContain("failed to load", error.Message);
        Assert.Null(error.InnerException);
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
