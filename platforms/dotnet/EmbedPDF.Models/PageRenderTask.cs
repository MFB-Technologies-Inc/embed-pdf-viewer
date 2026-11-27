namespace EmbedPDF.Models;

/// <summary>
/// Owns one page's asynchronous render work, including superseded work still
/// unwinding. Use from the model's home context.
/// </summary>
internal sealed class PageRenderTask
{
    const ConfigureAwaitOptions AfterAnyOutcome =
        ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext;

    Task? task;
    CancellationTokenSource? cancel;
    int generation;

    /// <summary>
    /// Replaces the current work, waiting for it to finish before
    /// <paramref name="operation"/> starts. Cancelling
    /// <paramref name="cancellationToken"/> cancels this call's operation.
    /// </summary>
    public async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        var current = ++generation;
        var previous = task;
        cancel?.Cancel();
        var mine = new CancellationTokenSource();
        cancel = mine;
        var next = Body(previous, operation, mine.Token);
        task = next;
        using (cancellationToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), mine))
        {
            await next.ConfigureAwait(AfterAnyOutcome);
        }
        if (generation == current)
        {
            task = null;
            cancel = null;
        }
    }

    /// <summary>Starts <paramref name="operation"/> only when no work owns the page.</summary>
    public void StartIfIdle(Func<CancellationToken, Task> operation)
    {
        if (task is not null) return;
        var current = ++generation;
        var mine = new CancellationTokenSource();
        cancel = mine;
        task = Idle();

        async Task Idle()
        {
            await Task.Yield();
            if (!mine.IsCancellationRequested)
            {
                try { await operation(mine.Token); }
                catch (OperationCanceledException) { }
            }
            if (generation == current)
            {
                task = null;
                cancel = null;
            }
        }
    }

    /// <summary>Cancels whatever work owns the page.</summary>
    public void Cancel() => cancel?.Cancel();

    static async Task Body(Task? previous, Func<CancellationToken, Task> operation, CancellationToken token)
    {
        await Task.Yield();
        if (previous is not null) await previous.ConfigureAwait(AfterAnyOutcome);
        if (token.IsCancellationRequested) return;
        await operation(token);
    }
}
