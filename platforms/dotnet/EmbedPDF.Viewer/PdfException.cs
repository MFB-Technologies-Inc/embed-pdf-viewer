namespace EmbedPDF;

/// <summary>The base of every error the EmbedPDF library raises.</summary>
public class PdfException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public PdfException() { }

    /// <summary>Creates an exception with a message.</summary>
    public PdfException(string message) : base(message) { }

    /// <summary>Creates an exception with a message and the error that caused it.</summary>
    public PdfException(string message, Exception innerException) : base(message, innerException) { }
}
