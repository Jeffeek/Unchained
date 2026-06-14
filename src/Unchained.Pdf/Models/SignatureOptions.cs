using System.Security.Cryptography.X509Certificates;

namespace Unchained.Pdf.Models;

/// <summary>
///     Options applied when digitally signing a PDF document.
/// </summary>
/// <param name="Reason">Human-readable reason for signing (e.g. "I approve this document").</param>
/// <param name="Location">Physical or network location where signing occurred.</param>
/// <param name="ContactInfo">Signer's contact information (e.g. email address).</param>
/// <param name="SigningTime">
///     Override the signing timestamp. Defaults to <see cref="DateTimeOffset.UtcNow" /> when
///     <see langword="null" />.
/// </param>
/// <param name="FieldName">Name of the signature form field added to the document (default <c>Signature1</c>).</param>
/// <param name="IncludeCertificateChain">
///     When <see langword="true" /> (default) the full certificate chain is embedded in the PKCS#7 container;
///     otherwise only the end-entity certificate is included.
/// </param>
public sealed record SignatureOptions(
    string? Reason = null,
    string? Location = null,
    string? ContactInfo = null,
    DateTimeOffset? SigningTime = null,
    string FieldName = "Signature1",
    bool IncludeCertificateChain = true,
    X509IncludeOption CertificateIncludeOption = X509IncludeOption.WholeChain
)
{
    /// <summary>Default signing options with no metadata.</summary>
    public static readonly SignatureOptions Default = new();
}
