using Unchained.Ooxml;

namespace Unchained.Xlsx.Core;

/// <summary>
///     The exception thrown when an error is encountered while reading, writing, or
///     processing a spreadsheet (<c>.xlsx</c>) file.
/// </summary>
public class SpreadsheetException : OoXmlException
{
    /// <summary>Initialises a new <see cref="SpreadsheetException" /> with the given message.</summary>
    public SpreadsheetException(string message) : base(message) { }

    /// <summary>
    ///     Initialises a new <see cref="SpreadsheetException" /> with the given message and inner exception.
    /// </summary>
    public SpreadsheetException(string message, Exception innerException) : base(message, innerException) { }
}
