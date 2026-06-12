namespace Unchained.Pdf.Core;

/// <summary>
///     Thrown when a PDF document is encrypted and cannot be opened without a valid password,
///     or when the supplied password is incorrect.
/// </summary>
public sealed class PdfEncryptedException : Exception
{
    /// <summary>Initializes a new instance with a descriptive message.</summary>
    public PdfEncryptedException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public PdfEncryptedException(string message, Exception inner) : base(message, inner) { }
}
