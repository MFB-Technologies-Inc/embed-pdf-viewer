using Microsoft.Extensions.Logging;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// Disposing a viewer needs its kernel thread, so a synchronous disposal from
/// a callback the kernel makes on that thread would wait for itself. The
/// adapters mark the thread while app code runs, and disposal refuses there.
/// These tests mark the test thread directly; the plugin tests do it through
/// real callbacks.
/// </summary>
public sealed class KernelCallbackTests
{
    [Fact]
    public async Task DisposeInsideAMarkedScopeThrows()
    {
        var viewer = await PdfViewer.CreateAsync();
        using (KernelCallback.Enter(viewer.Mark))
        {
            Assert.Throws<InvalidOperationException>(viewer.Dispose);
        }
        viewer.Dispose();
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task AnotherViewersMarkDoesNotBlockDispose()
    {
        await using var other = await PdfViewer.CreateAsync();
        var viewer = await PdfViewer.CreateAsync();
        using (KernelCallback.Enter(other.Mark))
        {
            viewer.Dispose();
        }
        await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync());
    }

    [Fact]
    public async Task DisposeAsyncInsideAMarkedScopeStartsDisposalAndBlockingOnItThrows()
    {
        var viewer = await PdfViewer.CreateAsync();
        ValueTask pending;
        using (KernelCallback.Enter(viewer.Mark))
        {
            pending = viewer.DisposeAsync();
            if (!pending.IsCompleted)
            {
                Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult());
            }
        }
        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
    }

    [Fact]
    public async Task AwaitingDisposeAsyncInsideAMarkedScopeCompletes()
    {
        var viewer = await PdfViewer.CreateAsync();
        ValueTask pending;
        using (KernelCallback.Enter(viewer.Mark))
        {
            pending = viewer.DisposeAsync();
        }
        await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task TheGuardedDisposalFlowsTheExecutionContext()
    {
        var viewer = await PdfViewer.CreateAsync();
        var local = new AsyncLocal<string?>();
        var seen = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ValueTask pending;
        using (KernelCallback.Enter(viewer.Mark))
        {
            pending = viewer.DisposeAsync();
        }
        Assert.False(pending.IsCompleted);
        local.Value = "flowed";
        // The safe OnCompleted asks the source to flow the caller's context.
        pending.GetAwaiter().OnCompleted(() => seen.TrySetResult(local.Value));
        local.Value = null;
        Assert.Equal("flowed", await seen.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task TheGuardedDisposalCannotBeBlockedOnOffTheKernelThreadEither()
    {
        var viewer = await PdfViewer.CreateAsync();
        ValueTask pending;
        using (KernelCallback.Enter(viewer.Mark))
        {
            pending = viewer.DisposeAsync();
        }
        Assert.False(pending.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult());
        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void NestedScopesRestore()
    {
        var a = new KernelMark();
        var b = new KernelMark();
        using (KernelCallback.Enter(a))
        {
            using (KernelCallback.Enter(b))
            {
                Assert.True(KernelCallback.IsInside(a));
                Assert.True(KernelCallback.IsInside(b));
            }
            Assert.True(KernelCallback.IsInside(a));
            Assert.False(KernelCallback.IsInside(b));
        }
        Assert.False(KernelCallback.IsInside(a));
    }

    [Fact]
    public void RunMarksTheThreadAndLogsWhatTheBodyThrows()
    {
        var mark = new KernelMark();
        var logger = new RecordingLogger();
        var inside = false;
        KernelCallback.Run(mark, logger, "a test callback", () =>
        {
            inside = KernelCallback.IsInside(mark);
            throw new InvalidOperationException("boom");
        });
        Assert.True(inside);
        Assert.False(KernelCallback.IsInside(mark));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Error);
        Assert.Contains("a test callback", entry.Message);
    }

    [Fact]
    public void RunWithAResultMapsWhatTheBodyThrows()
    {
        var mark = new KernelMark();
        var answer = KernelCallback.Run(mark, new RecordingLogger(), "a test callback",
            () => throw new InvalidOperationException("boom"), error => error.Message.Length);
        Assert.Equal(4, answer);
        Assert.Equal(7, KernelCallback.Run(mark, new RecordingLogger(), "a test callback", () => 7, _ => -1));
    }
}
