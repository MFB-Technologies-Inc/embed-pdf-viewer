namespace EmbedPDF.Models;

/// <summary>
/// Runs a model's calls one at a time, in the order they are enqueued. Each
/// call may suspend, and the next starts only after it returns. Enqueue on
/// the model's home context: each call starts there, after a post, as
/// Swift's main-actor task does.
/// </summary>
internal sealed class SerialCalls
{
    const ConfigureAwaitOptions AfterAnyOutcome =
        ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext;

    /// <summary>Finishes when every call enqueued so far has.</summary>
    Task last = Task.CompletedTask;

    /// <summary>
    /// Runs <paramref name="work"/> after every earlier call. The call joins
    /// the chain before this returns, so a later enqueue always runs after it.
    /// </summary>
    public Task<T> Enqueue<T>(Func<Task<T>> work)
    {
        var call = Run(last, work);
        last = call;
        return call;
    }

    public Task Enqueue(Func<Task> work) => Enqueue(async () =>
    {
        await work();
        return true;
    });

    public Task Enqueue(Action work) => Enqueue(() =>
    {
        work();
        return Task.FromResult(true);
    });

    static async Task<T> Run<T>(Task previous, Func<Task<T>> work)
    {
        await Task.Yield();
        await previous.ConfigureAwait(AfterAnyOutcome);
        return await work();
    }
}
