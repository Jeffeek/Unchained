using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Unchained.Pptx.Security;

/// <summary>
/// Exposes the protection state of a presentation and provides methods to manage
/// encryption and write-protection.
/// </summary>
public sealed class ProtectionInfo
{
    /// <summary>
    /// <see langword="true"/> when the presentation file was loaded from an encrypted
    /// (password-protected) source. Encrypt the saved output by setting
    /// <see cref="Models.SaveOptions.Password"/> at save time.
    /// </summary>
    public bool IsEncrypted { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the presentation has a write-protection password.
    /// PowerPoint requires the correct password to enable editing; the file itself is
    /// <em>not</em> encrypted. Use <see cref="Models.SaveOptions.Password"/> for full
    /// file encryption.
    /// </summary>
    public bool IsWriteProtected => WriteProtectionSaltBase64 != null;

    /// <summary>
    /// When <see langword="true"/>, PowerPoint suggests opening the file in read-only mode.
    /// Advisory only — does not encrypt or prevent opening the file.
    /// </summary>
    public bool ReadOnlyRecommended { get; set; }

    /// <summary>
    /// Sets a write-protection password.
    /// The verifier hash is stored in <c>presentation.xml</c>; the file itself is not encrypted.
    /// </summary>
    public void SetWriteProtection(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = ComputeVerifierHash(password, salt, WriteProtectionSpinCount);
        WriteProtectionSaltBase64 = Convert.ToBase64String(salt);
        WriteProtectionHashBase64 = Convert.ToBase64String(hash);
    }

    /// <summary>Removes write-protection from the presentation.</summary>
    public void RemoveWriteProtection()
    {
        WriteProtectionSaltBase64 = null;
        WriteProtectionHashBase64 = null;
    }

    /// <summary>
    /// Verifies <paramref name="password"/> against the stored write-protection verifier.
    /// Returns <see langword="false"/> when write-protection is not active.
    /// </summary>
    public bool CheckWriteProtection(string password)
    {
        if (!IsWriteProtected) return false;
        var salt = Convert.FromBase64String(WriteProtectionSaltBase64!);
        var expected = Convert.FromBase64String(WriteProtectionHashBase64!);
        var actual = ComputeVerifierHash(password, salt, WriteProtectionSpinCount);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    // ── Internal state (written to presentation.xml) ──────────────────────────

    internal string? WriteProtectionSaltBase64 { get; set; }
    internal string? WriteProtectionHashBase64 { get; set; }
    internal const int WriteProtectionSpinCount = 100_000;

    // ── Hash computation ──────────────────────────────────────────────────────

    internal static byte[] ComputeVerifierHash(string password, byte[] salt, int spinCount)
    {
        var pwdBytes = Encoding.Unicode.GetBytes(password);
        var buf = new byte[salt.Length + pwdBytes.Length];
        salt.CopyTo(buf, 0);
        pwdBytes.CopyTo(buf, salt.Length);

        var h = SHA512.HashData(buf);
        var iterBuf = new byte[4 + h.Length];
        for (var i = 0; i < spinCount; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(iterBuf, i);
            h.CopyTo(iterBuf, 4);
            h = SHA512.HashData(iterBuf);
        }
        return h;
    }
}
