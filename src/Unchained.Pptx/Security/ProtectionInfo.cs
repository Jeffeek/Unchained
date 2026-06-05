namespace Unchained.Pptx.Security;

/// <summary>
/// Exposes the protection state of a presentation and provides methods to
/// manage encryption and write-protection.
/// </summary>
/// <remarks>
/// Encryption (AES-256 OOXML) will be fully implemented in a later milestone.
/// In M1–M4 this class surfaces the protection state read from the file and
/// exposes stubs for future write operations.
/// </remarks>
public sealed class ProtectionInfo
{
    /// <summary>
    /// <see langword="true"/> when the presentation file is currently encrypted.
    /// A password must be supplied via <see cref="Models.OpenOptions.Password"/> to open it.
    /// </summary>
    public bool IsEncrypted { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the presentation has been marked as write-protected.
    /// </summary>
    public bool IsWriteProtected { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the file requests that the reader open it as read-only,
    /// though this is advisory and can be overridden by the user.
    /// </summary>
    public bool ReadOnlyRecommended { get; set; }
}
