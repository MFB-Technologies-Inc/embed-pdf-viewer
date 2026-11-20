using System.Threading.Tasks.Sources;
using Microsoft.Extensions.Logging;

namespace EmbedPDF;

/// <summary>
/// One viewer's identity on its kernel thread. Adapters hold it instead of the
/// <see cref="PdfViewer"/>, so a callback the kernel keeps never keeps the
/// viewer alive.
/// </summary>
internal sealed class KernelMark;

/// <summary>
/// Marks the current thread while app code runs inside a callback the kernel
/// makes on its own thread. Disposal refuses to wait there, since waiting
/// would hold the thread that disposal needs.
/// </summary>
internal static class KernelCallback
{
    [ThreadStatic]
    static Frame? top;

    internal sealed record Frame(KernelMark Mark, Frame? Previous);

    internal readonly struct Scope(Frame? previous) : IDisposable
    {
        public void Dispose() => top = previous;
    }

    /// <summary>Marks the thread for <paramref name="mark"/> until the scope is disposed. Scopes nest.</summary>
    internal static Scope Enter(KernelMark mark)
    {
        var previous = top;
        top = new Frame(mark, previous);
        return new Scope(previous);
    }

    /// <summary>Whether the current thread is inside a callback of <paramref name="mark"/>'s viewer.</summary>
    internal static bool IsInside(KernelMark mark)
    {
        for (var frame = top; frame is not null; frame = frame.Previous)
        {
            if (ReferenceEquals(frame.Mark, mark)) return true;
        }
        return false;
    }

    /// <summary>Runs app code marked. What it throws is logged and dropped, so it never reaches the kernel.</summary>
    internal static void Run(KernelMark mark, ILogger logger, string what, Action body)
    {
        using var _ = Enter(mark);
        try
        {
            body();
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "{Callback} threw; the kernel ignores it.", what);
        }
    }

    /// <summary>Runs app code marked. What it throws becomes <paramref name="onError"/>'s answer.</summary>
    internal static T Run<T>(KernelMark mark, ILogger logger, string what, Func<T> body, Func<Exception, T> onError)
    {
        using var _ = Enter(mark);
        try
        {
            return body();
        }
        catch (Exception error)
        {
            logger.LogDebug(error, "{Callback} threw; the kernel gets the mapped error.", what);
            return onError(error);
        }
    }

    /// <summary>
    /// Runs app code marked for a trait member that declares the host error:
    /// whatever the code throws reaches the kernel as
    /// <c>HostException.Failed</c> with its message.
    /// </summary>
    internal static void RunOrFail(KernelMark mark, Action body)
    {
        using var _ = Enter(mark);
        try
        {
            body();
        }
        catch (Exception error)
        {
            throw new uniffi.epdf_ffi.HostException.Failed(error.Message);
        }
    }

    /// <summary>
    /// A disposal started inside a callback: awaiting it works, blocking on it
    /// before it finishes throws, because the blocked thread is the one the
    /// disposal needs.
    /// </summary>
    internal sealed class GuardedDisposal(Task disposal) : IValueTaskSource
    {
        public ValueTaskSourceStatus GetStatus(short token) => disposal.Status switch
        {
            TaskStatus.RanToCompletion => ValueTaskSourceStatus.Succeeded,
            TaskStatus.Faulted => ValueTaskSourceStatus.Faulted,
            TaskStatus.Canceled => ValueTaskSourceStatus.Canceled,
            _ => ValueTaskSourceStatus.Pending,
        };

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            var context = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0 ? SynchronizationContext.Current : null;
            var flowed = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0 ? ExecutionContext.Capture() : null;
            void Invoke()
            {
                if (context is null) continuation(state);
                else context.Post(s => continuation(s), state);
            }
            disposal.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
            {
                if (flowed is null) Invoke();
                else ExecutionContext.Run(flowed, _ => Invoke(), null);
            });
        }

        public void GetResult(short token)
        {
            if (!disposal.IsCompleted) throw DisposalWait();
            disposal.GetAwaiter().GetResult();
        }
    }

    internal static InvalidOperationException DisposalWait() => new(
        "A viewer cannot be disposed synchronously from a callback the kernel makes on its own thread; " +
        "call DisposeAsync there and do not wait for it.");
}
