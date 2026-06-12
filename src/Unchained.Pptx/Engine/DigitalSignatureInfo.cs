namespace Unchained.Pptx.Engine;

/// <summary>
///     Metadata extracted from a single OOXML XML-DSig digital signature part.
///     Signatures are read-only in Unchained.Pptx — creation and re-signing require
///     an X.509 certificate and are not yet implemented.
/// </summary>
public sealed class DigitalSignatureInfo
{
    /// <summary>
    ///     The signer's distinguished name (CN= value from the X.509 certificate subject),
    ///     or an empty string when the certificate subject cannot be parsed from the signature XML.
    /// </summary>
    public string SignerName { get; init; } = string.Empty;

    /// <summary>
    ///     The signing time recorded in the signature's <c>&lt;xades:SigningTime&gt;</c> element,
    ///     or <see langword="null" /> when no signing time is present.
    /// </summary>
    public DateTimeOffset? SigningTime { get; init; }

    /// <summary>
    ///     The OPC part URI of the signature XML part, e.g.
    ///     <c>/_xmlsignatures/sig1.xml</c>.
    /// </summary>
    public string PartUri { get; init; } = string.Empty;

    /// <summary>
    ///     <see langword="true" /> when the signature XML is structurally present and could be
    ///     parsed; <see langword="false" /> when the XML could not be read.
    /// </summary>
    public bool IsReadable { get; init; }
}
