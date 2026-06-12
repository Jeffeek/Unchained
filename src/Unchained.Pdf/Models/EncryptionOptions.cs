namespace Unchained.Pdf.Models;

/// <summary>
///     Algorithms supported for PDF Standard Security Handler encryption.
/// </summary>
public enum PdfEncryptionAlgorithm
{
    /// <summary>RC4 with 128-bit key (V=2, R=3). Legacy — use only for maximum compatibility.</summary>
    // ReSharper disable once InconsistentNaming
    Rc4_128,

    /// <summary>AES with 128-bit key (V=4, R=4). PDF 1.5+.</summary>
    Aes128,

    /// <summary>AES with 256-bit key (V=5, R=6). PDF 2.0. Recommended default.</summary>
    Aes256
}

/// <summary>
///     Password-protection settings applied when saving a PDF document.
/// </summary>
/// <param name="UserPassword">
///     Password required to open and read the document.
///     An empty string means any user can open the file (the document is still encrypted).
/// </param>
/// <param name="OwnerPassword">
///     Password that grants unrestricted access and overrides <see cref="Permissions" />.
///     If empty, defaults to the same value as <paramref name="UserPassword" />.
/// </param>
/// <param name="Algorithm">Encryption algorithm (default <see cref="PdfEncryptionAlgorithm.Aes256" />).</param>
/// <param name="Permissions">
///     Operations permitted when the document is opened with the user password
///     (default <see cref="PdfPermissions.All" />).
/// </param>
public sealed record EncryptionOptions(
    string UserPassword = "",
    string OwnerPassword = "",
    PdfEncryptionAlgorithm Algorithm = PdfEncryptionAlgorithm.Aes256,
    PdfPermissions Permissions = PdfPermissions.All
);
