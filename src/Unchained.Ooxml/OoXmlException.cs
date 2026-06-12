namespace Unchained.Ooxml;

/// <summary>
///     The base exception for errors encountered while reading or writing OOXML documents.
/// </summary>
public class OoXmlException : Exception
{
    /// <summary>Initialises a new <see cref="OoXmlException" /> with the given message.</summary>
    public OoXmlException(string message) : base(message) { }

    /// <summary>
    ///     Initialises a new <see cref="OoXmlException" /> with the given message and inner exception.
    /// </summary>
    public OoXmlException(string message, Exception innerException) : base(message, innerException) { }
}
