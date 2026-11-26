using System.Runtime.CompilerServices;

namespace EmbedPDF.Models;

/// <summary>
/// A model's home context: the <see cref="SynchronizationContext"/> it was
/// made on or given, where all of its state lives and every
/// <c>PropertyChanged</c> is raised. It plays the part of Swift's main actor.
/// </summary>
internal sealed class ModelContext
{
    public ModelContext(SynchronizationContext? context) =>
        Context = context ?? SynchronizationContext.Current ?? new SerialSynchronizationContext();

    public SynchronizationContext Context { get; }

    /// <summary>Whether the calling code already runs on the home context.</summary>
    public bool IsCurrent => ReferenceEquals(SynchronizationContext.Current, Context);

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
