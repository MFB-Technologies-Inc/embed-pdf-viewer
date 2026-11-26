namespace EmbedPDF.Models;

/// <summary>
/// Applies a refresh's reads only when no refresh started after it, so an
/// older read never overwrites a newer one.
/// </summary>
internal sealed class SnapshotRefresh
{
    long version;

    /// <summary>
    /// Runs after a read finishes and before its result is applied or
    /// dropped. Tests hold a read here so a newer one can overtake it.
    /// </summary>
    public Func<Task>? ReadFinished { get; set; }

    public void Invalidate() => version++;

    public async Task RunAsync<T>(
        Func<Task<T>> load, Action<T> apply, Action<Exception> failure, CancellationToken cancellationToken = default)
    {
        var request = ++version;
        T value;
        try
        {
            value = await load();
        }
        catch (Exception error)
        {
            if (ReadFinished is { } held) await held();
            if (request != version || cancellationToken.IsCancellationRequested) return;
            failure(error);
            return;
        }
        if (ReadFinished is { } hold) await hold();
        if (request != version || cancellationToken.IsCancellationRequested) return;
        apply(value);
    }
}
