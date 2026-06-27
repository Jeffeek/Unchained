namespace Unchained.Ooxml.Security;

/// <summary>
///     Thrown when an OOXML package is encrypted and cannot be decrypted — typically because the
///     password is missing or incorrect, or the encryption container is malformed. Format layers
///     (Pptx, Xlsx, Docx) translate this into their own format-specific encrypted exception.
/// </summary>
public class OoXmlEncryptedException : OoXmlException
{
    /// <summary>Initialises a new <see cref="OoXmlEncryptedException" /> with the given message.</summary>
    public OoXmlEncryptedException(string message) : base(message) { }

    /// <summary>Initialises a new <see cref="OoXmlEncryptedException" /> with the given message and inner exception.</summary>
    public OoXmlEncryptedException(string message, Exception innerException) : base(message, innerException) { }
}
