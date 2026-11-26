using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace EmbedPDF.Models.Tests;

/// <summary>
/// A test's main thread: one dedicated thread pumping a
/// <see cref="SynchronizationContext"/>, the stand-in for Swift's main actor
/// and for a UI thread. <see cref="Watch"/> records every
/// <c>PropertyChanged</c> raised off that thread, and <see cref="Run"/>
/// fails the test on one (plan Review Focus 1).
/// </summary>
sealed class SingleThreadContext : SynchronizationContext
{
    readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
    readonly ConcurrentQueue<string> offThread = new();
    Thread? thread;

    /// <summary>The pumping thread.</summary>
    public Thread Thread => thread!;

    /// <summary>Notifications seen so far, on any thread.</summary>
    public int Notifications;

    public static Task Run(Func<SingleThreadContext, Task> body, int timeoutSeconds = 60)
    {
        var context = new SingleThreadContext();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.thread = new Thread(() => context.Pump(body, done)) { IsBackground = true, Name = "model test main" };
        context.thread.Start();
        return Finish(context, done.Task, timeoutSeconds);
    }

    public static Task Run(Func<Task> body, int timeoutSeconds = 60) => Run(_ => body(), timeoutSeconds);

    /// <summary>
    /// <see cref="Run(Func{SingleThreadContext, Task}, int)"/> that also fails
    /// on a task exception nobody observed, collected after the body ends. For
    /// tests that close a document or dispose a viewer under live models, or
    /// release a model.
    /// </summary>
    public static async Task RunWatched(Func<SingleThreadContext, Task> body, int timeoutSeconds = 60)
    {
        using var unobserved = new UnobservedWatch();
        await Run(body, timeoutSeconds);
        unobserved.Collect();
        Assert.True(unobserved.Seen.IsEmpty, "unobserved task exceptions: " + string.Join("; ", unobserved.Seen.Select(e => e.InnerException?.Message ?? e.Message)));
    }

    public static Task RunWatched(Func<Task> body, int timeoutSeconds = 60) => RunWatched(_ => body(), timeoutSeconds);

    static async Task Finish(SingleThreadContext context, Task body, int timeoutSeconds)
    {
        var finished = await Task.WhenAny(body, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
        context.queue.CompleteAdding();
        if (finished != body) throw new TimeoutException($"the test ran past {timeoutSeconds} s");
        await body;
        Assert.True(context.offThread.IsEmpty, "PropertyChanged off the main thread: " + string.Join(", ", context.offThread));
    }

    /// <summary>Fails the test on any notification of <paramref name="model"/> raised off this thread.</summary>
    public T Watch<T>(T model) where T : INotifyPropertyChanged
    {
        model.PropertyChanged += Check;
        return model;
    }

    void Check(object? sender, PropertyChangedEventArgs e)
    {
        Interlocked.Increment(ref Notifications);
        if (Thread.CurrentThread != thread) offThread.Enqueue($"{sender?.GetType().Name}.{e.PropertyName}");
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        try { queue.Add((d, state)); }
        catch (InvalidOperationException) { /* the test has finished */ }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Thread.CurrentThread == thread) { d(state); return; }
        using var sent = new ManualResetEventSlim();
        ExceptionDispatchInfo? failure = null;
        Post(_ =>
        {
            try { d(state); }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
            finally { sent.Set(); }
        }, null);
        sent.Wait();
        failure?.Throw();
    }

    public override SynchronizationContext CreateCopy() => this;

    void Pump(Func<SingleThreadContext, Task> body, TaskCompletionSource done)
    {
        SetSynchronizationContext(this);
        Post(async _ =>
        {
            try
            {
                await body(this);
                done.TrySetResult();
            }
            catch (Exception e)
            {
                done.TrySetException(e);
            }
        }, null);
        foreach (var (callback, state) in queue.GetConsumingEnumerable())
        {
            try { callback(state); }
            catch (Exception e) { done.TrySetException(e); }
        }
    }
}
