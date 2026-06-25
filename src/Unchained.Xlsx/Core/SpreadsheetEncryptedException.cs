namespace Unchained.Xlsx.Core;

/// <summary>
///     The exception thrown when a password-protected spreadsheet is opened without a
///     password, or when the supplied password is incorrect.
/// </summary>
public sealed class SpreadsheetEncryptedException : SpreadsheetException
{
    /// <summary>Initialises a new <see cref="SpreadsheetEncryptedException" /> with a default message.</summary>
    public SpreadsheetEncryptedException()
        : base("The spreadsheet is password-protected and no valid password was supplied.") { }

    /// <summary>Initialises a new <see cref="SpreadsheetEncryptedException" /> with the given message.</summary>
    public SpreadsheetEncryptedException(string message) : base(message) { }
}
