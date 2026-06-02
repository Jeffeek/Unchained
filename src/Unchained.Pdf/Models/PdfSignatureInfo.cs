using System.Security.Cryptography.X509Certificates;

namespace Unchained.Pdf.Models;

/// <summary>
/// Information about a digital signature found in a PDF document,
/// including the result of cryptographic verification.
/// </summary>
public sealed class PdfSignatureInfo
{
    /// <summary>Common name of the signer extracted from the certificate subject.</summary>
    public string SignerName { get; init; } = string.Empty;

    /// <summary>Reason for signing declared by the signer, or <see langword="null"/>.</summary>
    public string? Reason { get; init; }

    /// <summary>Location declared by the signer, or <see langword="null"/>.</summary>
    public string? Location { get; init; }

    /// <summary>Claimed signing timestamp, or <see langword="null"/> if not present.</summary>
    public DateTimeOffset? SigningTime { get; init; }

    /// <summary>Name of the signature form field.</summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> when the cryptographic signature over the document byte ranges is valid.
    /// A <see langword="false"/> value means the document has been modified after signing.
    /// </summary>
    public bool IsSignatureValid { get; init; }

    /// <summary>
    /// <see langword="true"/> when the signer's certificate chain validates successfully.
    /// </summary>
    public bool IsCertificateValid { get; init; }

    /// <summary>Reason why validation failed, or <see langword="null"/> when valid.</summary>
    public string? ValidationError { get; init; }

    /// <summary>The signer's end-entity certificate, or <see langword="null"/> if unavailable.</summary>
    public X509Certificate2? Certificate { get; init; }
}
