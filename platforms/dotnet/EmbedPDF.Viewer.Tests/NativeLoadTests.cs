using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Every public entry point loads the native runtime before it touches a
/// generated type, so a missing runtime reaches the caller as
/// <see cref="NativeRuntimeNotFoundException"/> rather than a
/// <see cref="TypeInitializationException"/> from a generated static
/// constructor. The library is already loaded in this process, so the seam
/// stands in for a failed load: an entry point that skipped the load would
/// succeed and fail the test.
/// </summary>
public sealed class NativeLoadTests
{
    static NativeRuntimeNotFoundException Missing() =>
        new("linux-test", ["/nowhere/libepdf_viewer_host.so"], "no native runtime for linux-test");

    static async Task<Exception> Failing(Func<Task> entry)
    {
        NativeRuntime.LoadOverride.Value = () => throw Missing();
        try
        {
            return await Record.ExceptionAsync(entry) ?? new Exception("the entry point did not load the runtime");
        }
        finally
        {
            NativeRuntime.LoadOverride.Value = null;
        }
    }

    [Fact]
    public async Task TheConstructorReportsAMissingRuntime() =>
        Assert.IsType<NativeRuntimeNotFoundException>(await Failing(() => Task.FromResult(new PdfViewer())));

    [Fact]
    public async Task CreateAsyncReportsAMissingRuntime() =>
        Assert.IsType<NativeRuntimeNotFoundException>(await Failing(() => PdfViewer.CreateAsync()));

    [Fact]
    public async Task ReadPageCountReportsAMissingRuntime() =>
        Assert.IsType<NativeRuntimeNotFoundException>(await Failing(() => NativeRuntime.ReadPageCountAsync(Fixture.Data("report"))));

    [Fact]
    public async Task TheMissingRuntimeIsAPdfExceptionNamingTheRuntime()
    {
        var error = Assert.IsAssignableFrom<PdfException>(await Failing(() => PdfViewer.CreateAsync()));
        Assert.Contains("linux-test", error.Message);
    }
}
