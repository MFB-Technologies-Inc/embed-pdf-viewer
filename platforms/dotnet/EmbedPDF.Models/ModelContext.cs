using System.Runtime.CompilerServices;

namespace EmbedPDF.Models;

/// <summary>
/// A model's home context: the <see cref="SynchronizationContext"/> it was
/// made on or given, where all of its state lives and every
/// <c>PropertyChanged</c> is raised. It plays the part of Swift's main actor.
/// </summary>
internal sealed class ModelContext
{
    public ModelContext(SynchronizationContext? context)
    {
        var current = SynchronizationContext.Current;
        Context = context ?? current ?? new SerialSynchronizationContext();
        // A context captured as the current one belongs to this thread. Some
        // UI dispatchers install a fresh instance of it for each callback, so
        // on that thread a later Current is another object of the same type;
        // the thread recognises those. A context made here, or given while a
        // different one is current, has no known thread and keeps the
        // identity check.
        if (current is not null && ReferenceEquals(Context, current))
        {
            homeThread = Environment.CurrentManagedThreadId;
            Threads.AddOrUpdate(Context, new StrongBox<int>(Environment.CurrentManagedThreadId));
        }
        else if (Threads.TryGetValue(Context, out var known))
        {
            // Handed on by another model, which captured it on its thread.
            homeThread = known.Value;
        }
    }

    /// <summary>The thread each captured context was captured on, so a model handed that context by another model knows it too.</summary>
    static readonly ConditionalWeakTable<SynchronizationContext, StrongBox<int>> Threads = new();

    readonly int? homeThread;

    public SynchronizationContext Context { get; }

    /// <summary>
    /// Whether the calling code already runs on the home context: it is the
    /// same object, or, for a context captured on its own thread, this is that
    /// thread and the current context is of the same type.
    /// </summary>
    public bool IsCurrent
    {
        get
        {
            var current = SynchronizationContext.Current;
            if (ReferenceEquals(current, Context)) return true;
            return homeThread is { } thread && current is not null
                && Environment.CurrentManagedThreadId == thread && current.GetType() == Context.GetType();
        }
    }

    /// <summary>
    /// Continues on the home context: at once when the caller is already on
    /// it, through a post otherwise. Awaits after it, which keep the default
    /// <c>ConfigureAwait(true)</c>, come back to it.
    /// </summary>
    public SwitchAwaitable SwitchTo() => new(this);

    /// <summary>Runs <paramref name="action"/> now when on the home context, and posts it there otherwise.</summary>
    public void Run(Action action)
    {
        if (IsCurrent) action();
        else Context.Post(static state => ((Action)state!)(), action);
    }

    /// <summary>Starts <paramref name="work"/> on the home context and returns its task.</summary>
    public Task RunAsync(Func<Task> work)
    {
        if (IsCurrent) return work();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Context.Post(static state =>
        {
            var (work, done) = ((Func<Task>, TaskCompletionSource))state!;
            Forward(work, done);
        }, (work, done));
        return done.Task;
    }

    static async void Forward(Func<Task> work, TaskCompletionSource done)
    {
        try
        {
            await work();
            done.TrySetResult();
        }
        catch (OperationCanceledException e)
        {
            done.TrySetCanceled(e.CancellationToken);
        }
        catch (Exception e)
        {
            done.TrySetException(e);
        }
    }

    public readonly struct SwitchAwaitable(ModelContext home) : INotifyCompletion
    {
        public SwitchAwaitable GetAwaiter() => this;

        public bool IsCompleted => home.IsCurrent;

        public void OnCompleted(Action continuation) =>
            home.Context.Post(static state => ((Action)state!)(), continuation);

        public void GetResult()
        {
        }
    }
}
