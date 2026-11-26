using System.Collections.Concurrent;

namespace EmbedPDF.Models.Tests;

/// <summary>
/// Records task exceptions nobody observed, the approach of
/// <c>CancellationTests.UnobservedWatch</c> in <c>EmbedPDF.Viewer.Tests</c>.
/// A model's loop that faults after its document closes or its viewer is
/// disposed shows up here when the faulted task is collected.
/// </summary>
sealed class UnobservedWatch : IDisposable
{
    public readonly ConcurrentQueue<Exception> Seen = new();

    public UnobservedWatch() => TaskScheduler.UnobservedTaskException += OnUnobserved;

    void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args) => Seen.Enqueue(args.Exception);

    public void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    public void Dispose() => TaskScheduler.UnobservedTaskException -= OnUnobserved;
}
