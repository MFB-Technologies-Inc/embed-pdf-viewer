using System.Collections.Concurrent;
using System.ComponentModel;

namespace EmbedPDF.Models.Tests;

/// <summary>
/// A UI thread whose dispatcher installs a fresh context instance for each
/// callback it runs, as some UI frameworks' dispatchers do by default. The
/// context a model captures at construction is then never the same object as
/// <see cref="SynchronizationContext.Current"/> later on that same thread.
/// </summary>
sealed class FreshInstanceContext : SynchronizationContext
{
    readonly Pump pump;

    FreshInstanceContext(Pump pump) => this.pump = pump;

    sealed class Pump
    {
        public readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> Queue = new();
        public Thread? Thread;
        public readonly ConcurrentQueue<string> OffThread = new();
    }

    public Thread Thread => pump.Thread!;

    public static Task Run(Func<FreshInstanceContext, Task> body)
    {
        var pump = new Pump();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pump.Thread = new Thread(() =>
        {
            new FreshInstanceContext(pump).Post(async _ =>
            {
                try
                {
                    await body((FreshInstanceContext)Current!);
                    done.TrySetResult();
                }
                catch (Exception e)
                {
                    done.TrySetException(e);
                }
            }, null);
            foreach (var (callback, state) in pump.Queue.GetConsumingEnumerable())
            {
                // A fresh instance per callback.
                SetSynchronizationContext(new FreshInstanceContext(pump));
                try { callback(state); }
                catch (Exception e) { done.TrySetException(e); }
            }
        }) { IsBackground = true, Name = "fresh instance main" };
        pump.Thread.Start();
        return Finish(pump, done.Task);
    }

    static async Task Finish(Pump pump, Task body)
    {
        var finished = await Task.WhenAny(body, Task.Delay(TimeSpan.FromSeconds(60)));
        pump.Queue.CompleteAdding();
        if (finished != body) throw new TimeoutException("the test ran past 60 s");
        await body;
        Assert.True(pump.OffThread.IsEmpty, "PropertyChanged off the main thread: " + string.Join(", ", pump.OffThread));
    }

    /// <summary>Fails the test on any notification of <paramref name="model"/> raised off this thread.</summary>
    public T Watch<T>(T model) where T : INotifyPropertyChanged
    {
        model.PropertyChanged += (sender, e) =>
        {
            if (Thread.CurrentThread != pump.Thread) pump.OffThread.Enqueue($"{sender?.GetType().Name}.{e.PropertyName}");
        };
        return model;
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        try { pump.Queue.Add((d, state)); }
        catch (InvalidOperationException) { /* the test has finished */ }
    }

    public override SynchronizationContext CreateCopy() => new FreshInstanceContext(pump);
}
