using uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// What every viewer and capability call throws.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Code"/> is one of the kernel's nine plugin error codes, the
/// bindings' own <c>invalid-state</c>, or any other string the kernel adds
/// later. An engine's own code (<c>NotFound</c>, <c>DocPasswordIncorrect</c>)
/// never appears here: it maps as the kernel's <c>toPluginError</c> maps it,
/// and <see cref="Detail"/> is the engine's message with no code in front.
/// <see cref="Name"/> says what raised the error (<c>PluginError</c>,
/// <c>EngineError</c>, <c>AbortError</c>, or a foreign error's type) and is
/// for logging rather than matching.
/// </para>
/// <para>
/// Match a code with an exception filter:
/// <c>catch (PdfException e) when (e.Code == PdfErrorCode.PermissionDenied)</c>.
/// A call whose <see cref="CancellationToken"/> fired throws
/// <see cref="OperationCanceledException"/> instead, with this exception as
/// its inner exception when the kernel answered. Argument errors throw
/// <see cref="ArgumentException"/> before any call, and an open that reads the
/// caller's file or stream lets that read's own exception escape unwrapped.
/// </para>
/// </remarks>
public class PdfException : Exception
{
    /// <summary>Creates an <c>operation-failed</c> exception with no message.</summary>
    public PdfException() : this(string.Empty) { }

    /// <summary>Creates an <c>operation-failed</c> exception whose detail is <paramref name="message"/>.</summary>
    public PdfException(string message) : base(message)
    {
        Name = GetType().Name;
        Code = PdfErrorCode.OperationFailed;
        Detail = message;
    }

    /// <summary>Creates an <c>operation-failed</c> exception with a message and the error that caused it.</summary>
    public PdfException(string message, Exception innerException) : base(message, innerException)
    {
        Name = GetType().Name;
        Code = PdfErrorCode.OperationFailed;
        Detail = message;
    }

    /// <summary>
    /// For a handler or port that fails with a kernel code of its own, such
    /// as <c>permission-denied</c>. <paramref name="name"/> is for logging;
    /// most callers keep <c>PluginError</c>.
    /// </summary>
    public PdfException(PdfErrorCode code, string detail)
        : this("PluginError", code, detail, null) { }

    /// <summary>A handler or port failure with its own <paramref name="name"/>.</summary>
    public PdfException(PdfErrorCode code, string detail, string name)
        : this(name, code, detail, null) { }

    /// <summary>Creates an exception with every member named.</summary>
    public PdfException(string name, PdfErrorCode code, string detail)
        : this(name, code, detail, null) { }

    /// <summary>Creates an exception with every member named and the error that caused it.</summary>
    public PdfException(string name, PdfErrorCode code, string detail, Exception? innerException)
        : base(detail, innerException)
    {
        Name = name;
        Code = code;
        Detail = detail;
    }

    /// <summary>What raised the error, for logging.</summary>
    public string Name { get; }

    /// <summary>The kernel error code.</summary>
    public PdfErrorCode Code { get; }

    /// <summary>The engine's message, which is also <see cref="Exception.Message"/>.</summary>
    public string Detail { get; }

    internal static PdfException Closed(Exception? inner = null) =>
        new("Closed", PdfErrorCode.InstanceClosed, "The viewer has shut down.", inner);

    /// <summary>Maps anything a generated call throws to the facade's error.</summary>
    internal static PdfException From(Exception error) => error switch
    {
        PdfException own => own,
        ViewerException.Kernel kernel => new PdfException(kernel.@name,
            kernel.@code is { } code ? new PdfErrorCode(code) : PdfErrorCode.OperationFailed, kernel.@detail, kernel),
        ViewerException.Closed or ObjectDisposedException => Closed(error),
        OperationCanceledException => new PdfException("AbortError", PdfErrorCode.OperationCancelled, "operation cancelled", error),
        _ => new PdfException(error.GetType().Name, PdfErrorCode.OperationFailed, error.Message, error),
    };
}
