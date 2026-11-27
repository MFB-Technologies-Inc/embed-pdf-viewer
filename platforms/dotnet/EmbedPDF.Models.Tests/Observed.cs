using System.ComponentModel;

namespace EmbedPDF.Models.Tests;

/// <summary>The ports of Swift's <c>expectObserved</c>, <c>observationFires</c> and <c>holdingFirstRead</c>.</summary>
static class Observed
{
    /// <summary>
    /// Runs <paramref name="during"/>, then waits until <paramref name="read"/>
    /// equals <paramref name="expected"/>, rechecking after each notification
    /// of <paramref name="model"/>; fails after 20 seconds.
    /// </summary>
    public static async Task ExpectAsync<T>(INotifyPropertyChanged model, Func<T> read, T expected, Func<Task>? during = null)
    {
        var changed = new SemaphoreSlim(0);
        void OnChanged(object? sender, PropertyChangedEventArgs e) => changed.Release();
        model.PropertyChanged += OnChanged;
        try
        {
            if (during is not null) await during();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (!EqualityComparer<T>.Default.Equals(read(), expected))
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero || !await changed.WaitAsync(left))
                {
                    Assert.Fail($"expected {expected}, still {read()}");
                }
            }
        }
        finally
        {
            model.PropertyChanged -= OnChanged;
        }
        Assert.Equal(expected, read());
    }

    /// <summary>Waits until <paramref name="condition"/> holds, rechecking after each notification.</summary>
    public static Task UntilAsync(INotifyPropertyChanged model, Func<bool> condition) => ExpectAsync(model, condition, true);

    /// <summary>Whether a notification of <paramref name="property"/> is raised before <paramref name="action"/> returns.</summary>
    public static async Task<bool> FiresAsync(INotifyPropertyChanged model, string property, Func<Task> action)
    {
        var fired = false;
        void OnChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == property) fired = true; }
        model.PropertyChanged += OnChanged;
        try { await action(); }
        finally { model.PropertyChanged -= OnChanged; }
        return fired;
    }

    /// <summary>The properties notified before <paramref name="action"/> returns.</summary>
    public static async Task<HashSet<string>> NotifiedAsync(INotifyPropertyChanged model, Func<Task> action)
    {
        var names = new HashSet<string>();
        void OnChanged(object? sender, PropertyChangedEventArgs e) => names.Add(e.PropertyName!);
        model.PropertyChanged += OnChanged;
        try { await action(); }
        finally { model.PropertyChanged -= OnChanged; }
        return names;
    }

    /// <summary>Polls <paramref name="condition"/> every 20 ms until it holds or 10 seconds pass.</summary>
    public static async Task<bool> PollAsync(Func<Task<bool>> condition, int seconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(20);
        }
        return await condition();
    }

    /// <summary>
    /// Holds the first read that finishes on <paramref name="snapshots"/> until
    /// <c>release</c> runs, so a refresh started after it can land first.
    /// <c>reached</c> completes once the read is held.
    /// </summary>
    public static (Task Reached, Action Release) HoldFirstRead(SnapshotRefresh snapshots)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = false;
        snapshots.ReadFinished = async () =>
        {
            if (held) return;
            held = true;
            reached.TrySetResult();
            await gate.Task;
        };
        return (reached.Task, () => gate.TrySetResult());
    }

    /// <summary>Lets the main thread run what it has queued, <paramref name="turns"/> times.</summary>
    public static async Task Turns(int turns = 20)
    {
        for (var i = 0; i < turns; i++) await Task.Yield();
    }

    /// <summary>
    /// Collects until a weak reference dies, or fails. Yields first, so the
    /// frames of the helper that made the object have left the stack: an
    /// await can resume inline inside the awaited method's completion.
    /// </summary>
    public static async Task CollectedAsync(WeakReference reference)
    {
        await Task.Yield();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive, "the model was not collected");
    }
}
