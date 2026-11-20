namespace EmbedPDF;

/// <summary>
/// A kernel error code. <see cref="Value"/> is the wire string.
/// </summary>
/// <remarks>
/// The set is open, so this is a struct rather than an enum: a code the
/// kernel adds later needs no new constant to round-trip, and adding one
/// breaks no caller's <c>switch</c>. A string converts to a code, so
/// <c>error.Code == "quota-exceeded"</c> compares as expected.
/// </remarks>
/// <param name="Value">The wire string, such as <c>not-found</c>.</param>
public readonly record struct PdfErrorCode(string Value)
{
    /// <summary>The wire string; empty for <c>default(PdfErrorCode)</c>.</summary>
    public string Value { get => field ?? string.Empty; init => field = value ?? throw new ArgumentNullException(nameof(value)); } = Value;

    /// <summary><c>permission-denied</c>: the scope, the document's permissions or its password refused it (a wrong unlock password is this).</summary>
    public static PdfErrorCode PermissionDenied { get; } = new("permission-denied");

    /// <summary><c>unsupported</c>: the plugin isn't in the <see cref="PdfViewerOptions"/>, or the engine can't do it.</summary>
    public static PdfErrorCode Unsupported { get; } = new("unsupported");

    /// <summary><c>not-found</c>: no such document, page, annotation or other resource.</summary>
    public static PdfErrorCode NotFound { get; } = new("not-found");

    /// <summary><c>not-ready</c>: the document is still loading, is locked, or failed.</summary>
    public static PdfErrorCode NotReady { get; } = new("not-ready");

    /// <summary><c>invalid-input</c>: an argument, or bytes that aren't a PDF.</summary>
    public static PdfErrorCode InvalidInput { get; } = new("invalid-input");

    /// <summary><c>conflict</c>: the document changed under the call.</summary>
    public static PdfErrorCode Conflict { get; } = new("conflict");

    /// <summary><c>instance-closed</c>: the document closed or the viewer shut down.</summary>
    public static PdfErrorCode InstanceClosed { get; } = new("instance-closed");

    /// <summary><c>operation-cancelled</c>: the call was cancelled.</summary>
    public static PdfErrorCode OperationCancelled { get; } = new("operation-cancelled");

    /// <summary><c>operation-failed</c>: anything else.</summary>
    public static PdfErrorCode OperationFailed { get; } = new("operation-failed");

    /// <summary>
    /// <c>invalid-state</c>, the bindings' own code: a value that didn't
    /// survive the trip across the boundary, such as a count past
    /// <see cref="int.MaxValue"/>. It is a bug to report, not something a
    /// caller did.
    /// </summary>
    public static PdfErrorCode InvalidState { get; } = new("invalid-state");

    /// <summary>The code whose wire string is <paramref name="value"/>.</summary>
    public static implicit operator PdfErrorCode(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;
}
