namespace EmbedPDF.Models;

/// <summary>The port of Swift's <c>controlError</c>.</summary>
internal static class ModelErrors
{
    /// <summary>
    /// A <see cref="PdfException"/> as it is; a cancellation as
    /// <c>operation-cancelled</c>; anything else as <c>operation-failed</c>,
    /// named by its type, with its message as the detail.
    /// </summary>
    public static PdfException Control(Exception error) => error switch
    {
        PdfException pdf => pdf,
        OperationCanceledException => new PdfException(error.GetType().Name, PdfErrorCode.OperationCancelled, error.Message, error),
        _ => new PdfException(error.GetType().Name, PdfErrorCode.OperationFailed, error.Message, error),
    };
}
