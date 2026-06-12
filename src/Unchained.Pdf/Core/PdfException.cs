namespace Unchained.Pdf.Core;

/// <summary>
///     Exception thrown when a PDF document is structurally malformed or when
///     an unsupported PDF feature is encountered during parsing or serialization.
///     Optionally carries the <see cref="ByteOffset" /> in the source file where
///     the problem was detected to aid debugging.
/// </summary>
public sealed class PdfException : Exception
{
    /// <summary>Creates an exception with a descriptive message.</summary>
    public PdfException(string message) : base(message) { }

    /// <summary>Creates an exception wrapping an inner exception.</summary>
    public PdfException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    ///     Creates an exception that includes the byte offset in its message.
    ///     The offset is formatted as a hexadecimal address for easy correlation
    ///     with a hex editor view of the source file.
    /// </summary>
    /// <param name="message">Human-readable description of the error.</param>
    /// <param name="byteOffset">Absolute byte offset in the PDF source where the error was detected.</param>
    public PdfException(string message, long byteOffset)
        : base($"{message} (offset 0x{byteOffset:X})") => ByteOffset = byteOffset;

    /// <summary>
    ///     The absolute byte offset from the beginning of the PDF source file where the error
    ///     was detected, or <see langword="null" /> if the location is unknown.
    /// </summary>
    public long? ByteOffset { get; }
}
