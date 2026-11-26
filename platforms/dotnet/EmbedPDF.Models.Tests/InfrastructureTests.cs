using System.Reflection;

namespace EmbedPDF.Models.Tests;

/// <summary>Ports of <c>SerialCallsTests.swift</c>, <c>SnapshotRefreshTests.swift</c>, <c>PageRenderTaskTests.swift</c> and <c>ImportTests.swift</c>.</summary>
public class SerialCallsTests
{
    [Fact]
    public Task CallsRunInTheOrderEnqueued() => SingleThreadContext.Run(async () =>
    {
        var calls = new SerialCalls();
        var order = new List<int>();
        // An earlier call suspends for longer, so calls that only shared the
        // main thread would finish in reverse.
        foreach (var (number, yields) in new[] { (1, 8), (2, 4), (3, 0) })
        {
            _ = calls.Enqueue(async () =>
            {
                for (var i = 0; i < yields; i++) await Task.Yield();
                order.Add(number);
            });
        }
        await calls.Enqueue(() => { });
        Assert.Equal([1, 2, 3], order);
    });

    [Fact]
    public Task ACallReturnsItsResult() => SingleThreadContext.Run(async () =>
    {
        var calls = new SerialCalls();
        _ = calls.Enqueue(async () => await Task.Yield());
        Assert.Equal(7, await calls.Enqueue(() => Task.FromResult(7)));
    });
}

public class SnapshotRefreshTests
{
    [Fact]
    public Task ASlowerReadCannotOverwriteANewerSnapshot() => SingleThreadContext.Run(async () =>
    {
        var refresh = new SnapshotRefresh();
        var applied = new List<string>();
        var started = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        var slow = refresh.RunAsync(async () =>
        {
            started.SetResult();
            await gate.Task;
            return "old";
        }, applied.Add, _ => Assert.Fail("unexpected read failure"));
        await started.Task;
        await refresh.RunAsync(() => Task.FromResult("new"), applied.Add, _ => Assert.Fail("unexpected read failure"));
        gate.SetResult();
        await slow;
        Assert.Equal(["new"], applied);
    });

    [Fact]
    public Task InvalidationDiscardsAPendingFailure() => SingleThreadContext.Run(async () =>
    {
        var refresh = new SnapshotRefresh();
        var failures = 0;
        var started = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        var slow = refresh.RunAsync<string>(async () =>
        {
            started.SetResult();
            await gate.Task;
            throw new InvalidOperationException();
        }, _ => Assert.Fail("unexpected snapshot"), _ => failures++);
        await started.Task;
        refresh.Invalidate();
        gate.SetResult();
        await slow;
        Assert.Equal(0, failures);
    });
}

public class PageRenderTaskTests
{
    [Fact]
    public Task ReplacementWaitsForCancelledWorkToFinish() => SingleThreadContext.Run(async () =>
    {
        var work = new PageRenderTask();
        var entered = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var events = new List<string>();
        var first = work.RunAsync(async _ =>
        {
            entered.TrySetResult();
            await finish.Task;
            events.Add("first finished");
        });
        await entered.Task;
        var second = work.RunAsync(_ => { events.Add("replacement"); return Task.CompletedTask; });
        await Observed.Turns();
        Assert.Empty(events);
        finish.SetResult();
        await first;
        await second;
        Assert.Equal(["first finished", "replacement"], events);
    });

    [Fact]
    public Task TheNewestQueuedRequestSupersedesEarlierWaitingWork() => SingleThreadContext.Run(async () =>
    {
        var work = new PageRenderTask();
        var entered = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var events = new List<string>();
        var first = work.RunAsync(async _ => { entered.TrySetResult(); await finish.Task; events.Add("first"); });
        await entered.Task;
        var second = work.RunAsync(_ => { events.Add("superseded"); return Task.CompletedTask; });
        await Observed.Turns();
        var third = work.RunAsync(_ => { events.Add("newest"); return Task.CompletedTask; });
        await Observed.Turns();
        finish.SetResult();
        await Task.WhenAll(first, second, third);
        Assert.Equal(["first", "newest"], events);
    });

    [Fact]
    public Task CancellingTheCallerCancelsItsQueuedOperation() => SingleThreadContext.Run(async () =>
    {
        var work = new PageRenderTask();
        var entered = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var events = new List<string>();
        var first = work.RunAsync(async _ => { entered.TrySetResult(); await finish.Task; events.Add("first"); });
        await entered.Task;
        using var caller = new CancellationTokenSource();
        var second = work.RunAsync(_ => { events.Add("cancelled"); return Task.CompletedTask; }, caller.Token);
        await Observed.Turns();
        caller.Cancel();
        finish.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(["first"], events);
    });

    [Fact]
    public Task BackgroundWorkDoesNotCompeteWithAnActiveRequest() => SingleThreadContext.Run(async () =>
    {
        var work = new PageRenderTask();
        var entered = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var backgroundFinished = new TaskCompletionSource();
        var events = new List<string>();
        var first = work.RunAsync(async _ => { entered.TrySetResult(); await finish.Task; events.Add("first"); });
        await entered.Task;
        work.StartIfIdle(_ => { events.Add("duplicate"); return Task.CompletedTask; });
        finish.SetResult();
        await first;
        work.StartIfIdle(_ => { events.Add("background"); backgroundFinished.SetResult(); return Task.CompletedTask; });
        await backgroundFinished.Task;
        Assert.Equal(["first", "background"], events);
    });
}

public class ImportTests
{
    /// <summary>
    /// The models need no UI framework: the assembly references the facade
    /// and the shared framework only.
    /// </summary>
    [Fact]
    public void TheModelsReferenceNoUiAssembly()
    {
        var references = typeof(ObservableModel).Assembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();
        Assert.Contains("EmbedPDF.Viewer", references);
        Assert.All(references, name => Assert.True(
            name == "EmbedPDF.Viewer" || name.StartsWith("System", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal),
            $"unexpected reference {name}"));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("System.Windows", StringComparison.Ordinal) || name.StartsWith("System.Xaml", StringComparison.Ordinal)
            || name.StartsWith("PresentationFramework", StringComparison.Ordinal) || name.StartsWith("WindowsBase", StringComparison.Ordinal));
        Assert.Equal("EmbedPDF.Models.TextSelectionModel", typeof(TextSelectionModel).FullName);
    }
}
