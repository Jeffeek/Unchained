using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Unchained.Pptx.Core;

namespace Unchained.Pptx.Security;

/// <summary>
/// OOXML Agile Encryption per ECMA-376 Part 4 §3.4 and [MS-OFFCRYPTO] §2.3.4.
/// Uses AES-256-CBC with SHA-512 PBKDF key derivation. All crypto from BCL.
/// </summary>
internal static class AgileEncryption
{
    // EncryptionInfo stream header for Agile Encryption (version 4.4)
    private static readonly byte[] EncryptionInfoHeader =
        [0x04, 0x00, 0x04, 0x00, 0x40, 0x00, 0x00, 0x00];

    // Block keys for password-based key encryptor (ECMA-376 Part 4 §3.4.4.5 / [MS-OFFCRYPTO] §2.3.4.13)
    private static readonly byte[] BlockKeyVerifierInput =
        [0xfe, 0xa7, 0xd2, 0x76, 0x3b, 0x4b, 0x9e, 0x79];
    private static readonly byte[] BlockKeyVerifierHash =
        [0xd7, 0xaa, 0x0f, 0x6d, 0x30, 0x61, 0x34, 0x4e];
    private static readonly byte[] BlockKeyEncryptedKey =
        [0x14, 0x6e, 0x0b, 0xe7, 0xab, 0xac, 0xd0, 0xd6];

    // Block keys for dataIntegrity HMAC ([MS-OFFCRYPTO] §2.3.4.14)
    private static readonly byte[] BlockKeyHmacKey =
        [0x5f, 0xb3, 0xd3, 0xfb, 0xa0, 0x6a, 0x62, 0x36];
    private static readonly byte[] BlockKeyHmacValue =
        [0xa0, 0x67, 0x7f, 0x02, 0xb2, 0x2c, 0x84, 0x33];

    private const int SpinCount = 100_000;
    private const int KeyBytes = 32;   // AES-256
    private const int SaltSize = 16;
    private const int BlockSize = 16;  // AES block size
    private const int SegmentSize = 4096;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when the first 8 bytes are the OLE CFB magic.
    /// </summary>
    public static bool IsCfb(byte[] data)
    {
        if (data.Length < 8) return false;
        ReadOnlySpan<byte> magic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        return data.AsSpan(0, 8).SequenceEqual(magic);
    }

    /// <summary>
    /// Encrypts <paramref name="zipBytes"/> with Agile AES-256-CBC / SHA-512 encryption
    /// and wraps the result in an OLE CFB document.
    /// </summary>
    public static byte[] Encrypt(byte[] zipBytes, string password)
    {
        var documentSalt = RandomBytes(SaltSize);
        var documentKey = RandomBytes(KeyBytes);
        var passwordSalt = RandomBytes(SaltSize);

        // Step 1: Compute the iterated password hash (shared across all block-key derivations)
        var iterHash = IteratedHash(password, passwordSalt, SpinCount);

        // Step 2: Derive three distinct keys from the iterated hash + block keys
        var keyForVerifierInput = DeriveWithBlock(iterHash, BlockKeyVerifierInput);
        var keyForVerifierHash = DeriveWithBlock(iterHash, BlockKeyVerifierHash);
        var keyForEncryptedKey = DeriveWithBlock(iterHash, BlockKeyEncryptedKey);

        // Step 3: Encrypt verifier and document key using the derived keys (IV = passwordSalt)
        var verifier = RandomBytes(SaltSize);
        var verifierHash = SHA512.HashData(verifier);
        var encryptedVerifierInput = AesCbcEncryptZeroPad(verifier, keyForVerifierInput, passwordSalt);
        var encryptedVerifierHash = AesCbcEncryptZeroPad(verifierHash, keyForVerifierHash, passwordSalt);
        var encryptedKeyValue = AesCbcEncryptZeroPad(documentKey, keyForEncryptedKey, passwordSalt);

        // Step 4: Encrypt the ZIP content in 4096-byte segments (each with its own IV)
        var encryptedPackage = EncryptPackage(zipBytes, documentKey, documentSalt);

        // Step 5: HMAC integrity — key/value encrypted with document key
        var hmacKey = RandomBytes(64);
        using var hmac = new HMACSHA512(hmacKey);
        var hmacValue = hmac.ComputeHash(encryptedPackage);
        var hmacKeyIv = DeriveHmacIv(documentKey, documentSalt, BlockKeyHmacKey);
        var hmacValIv = DeriveHmacIv(documentKey, documentSalt, BlockKeyHmacValue);
        var encryptedHmacKey = AesCbcEncryptZeroPad(hmacKey, hmacKeyIv.key, hmacKeyIv.iv);
        var encryptedHmacValue = AesCbcEncryptZeroPad(hmacValue, hmacValIv.key, hmacValIv.iv);

        // Step 6: Build EncryptionInfo XML and wrap in CFB
        var xmlBytes = BuildEncryptionInfoXml(
            documentSalt, passwordSalt, encryptedKeyValue,
            encryptedVerifierInput, encryptedVerifierHash,
            encryptedHmacKey, encryptedHmacValue);

        var encryptionInfoStream = new byte[EncryptionInfoHeader.Length + xmlBytes.Length];
        EncryptionInfoHeader.CopyTo(encryptionInfoStream, 0);
        xmlBytes.CopyTo(encryptionInfoStream, EncryptionInfoHeader.Length);

        return CfbDocument.Write([
            ("EncryptionInfo", encryptionInfoStream),
            ("EncryptedPackage", encryptedPackage),
        ]);
    }

    /// <summary>
    /// Decrypts an OOXML Agile-encrypted CFB document.
    /// </summary>
    /// <exception cref="PptxEncryptedException">
    /// Thrown when the password is incorrect or the file is not valid.
    /// </exception>
    public static byte[] Decrypt(byte[] cfbBytes, string password)
    {
        Dictionary<string, byte[]> streams;
        try { streams = CfbDocument.Read(cfbBytes); }
        catch (PptxException ex)
        { throw new PptxEncryptedException("The file is not a valid OOXML encrypted file.", ex); }

        if (!streams.TryGetValue("EncryptionInfo", out var encryptionInfoStream))
            throw new PptxEncryptedException("EncryptionInfo stream not found.");

        if (!streams.TryGetValue("EncryptedPackage", out var encryptedPackage))
            throw new PptxEncryptedException("EncryptedPackage stream not found.");

        if (encryptionInfoStream.Length < 8)
            throw new PptxEncryptedException("EncryptionInfo stream too short.");

        // Parse encryption parameters (skip 8-byte version header)
        var xmlBytes = encryptionInfoStream.AsSpan(8);
        var (documentSalt, passwordSalt, spinCount, encryptedKeyValue,
             encryptedVerifierInput, encryptedVerifierHash) = ParseEncryptionInfo(xmlBytes);

        // Derive iterated hash and block-key keys
        var iterHash = IteratedHash(password, passwordSalt, spinCount);
        var keyForVerifierInput = DeriveWithBlock(iterHash, BlockKeyVerifierInput);
        var keyForVerifierHash = DeriveWithBlock(iterHash, BlockKeyVerifierHash);
        var keyForEncryptedKey = DeriveWithBlock(iterHash, BlockKeyEncryptedKey);

        // Verify password (constant-time comparison to prevent timing attacks)
        var verifier = AesCbcDecryptTruncate(encryptedVerifierInput, keyForVerifierInput, passwordSalt, SaltSize);
        var expectedHash = AesCbcDecryptTruncate(encryptedVerifierHash, keyForVerifierHash, passwordSalt, 64);
        var actualHash = SHA512.HashData(verifier);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash.AsSpan(0, Math.Min(64, expectedHash.Length))))
            throw new PptxEncryptedException(
                "The supplied password is incorrect. Supply the correct password via OpenOptions.Password.");

        // Decrypt the document key and the package
        var documentKey = AesCbcDecryptTruncate(encryptedKeyValue, keyForEncryptedKey, passwordSalt, KeyBytes);
        return DecryptPackage(encryptedPackage, documentKey, documentSalt);
    }

    // ── Key derivation ────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the iterated hash: H_0 = SHA512(salt + pwd_utf16le), then
    /// H_i = SHA512(i_LE4 + H_{i-1}) for i = 0..spinCount-1.
    /// </summary>
    private static byte[] IteratedHash(string password, byte[] salt, int spinCount)
    {
        var pwdBytes = Encoding.Unicode.GetBytes(password);
        var initial = new byte[salt.Length + pwdBytes.Length];
        salt.CopyTo(initial, 0);
        pwdBytes.CopyTo(initial, salt.Length);
        var h = SHA512.HashData(initial);

        var iterBuf = new byte[4 + h.Length];
        for (var i = 0; i < spinCount; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(iterBuf, i);
            h.CopyTo(iterBuf, 4);
            h = SHA512.HashData(iterBuf);
        }
        return h;
    }

    /// <summary>
    /// Derives a 32-byte (AES-256) key from an iterated hash and a block key:
    /// SHA512(iterHash || blockKey)[:32]
    /// </summary>
    private static byte[] DeriveWithBlock(byte[] iterHash, byte[] blockKey)
    {
        var input = new byte[iterHash.Length + blockKey.Length];
        iterHash.CopyTo(input, 0);
        blockKey.CopyTo(input, iterHash.Length);
        var hash = SHA512.HashData(input);
        var key = new byte[KeyBytes];
        Array.Copy(hash, key, KeyBytes);
        return key;
    }

    /// <summary>
    /// Derives the key and IV for HMAC block encryption (using document key + block key).
    /// </summary>
    private static (byte[] key, byte[] iv) DeriveHmacIv(byte[] documentKey, byte[] salt, byte[] blockKey)
    {
        // IV derived from the document salt + blockKey hash (truncated to block size)
        var ivInput = new byte[salt.Length + blockKey.Length];
        salt.CopyTo(ivInput, 0);
        blockKey.CopyTo(ivInput, salt.Length);
        var ivHash = SHA512.HashData(ivInput);
        var iv = new byte[BlockSize];
        Array.Copy(ivHash, iv, BlockSize);

        return (documentKey, iv);
    }

    // ── Package encryption / decryption ───────────────────────────────────────

    private static byte[] EncryptPackage(byte[] zipBytes, byte[] documentKey, byte[] salt)
    {
        var result = new MemoryStream();

        // 8-byte original size header
        var sizeBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(sizeBytes, zipBytes.Length);
        result.Write(sizeBytes);

        var segmentCount = (zipBytes.Length + SegmentSize - 1) / SegmentSize;
        if (segmentCount == 0) segmentCount = 1; // always at least one segment

        for (var i = 0; i < segmentCount; i++)
        {
            var segStart = i * SegmentSize;
            var segLen = Math.Min(SegmentSize, zipBytes.Length - segStart);
            var paddedLen = PadTo16(segLen);
            var segment = new byte[paddedLen];
            Array.Copy(zipBytes, segStart, segment, 0, segLen);
            // trailing bytes are already 0

            var iv = SegmentIv(salt, i);
            result.Write(AesCbcEncryptNoPad(segment, documentKey, iv));
        }

        return result.ToArray();
    }

    private static byte[] DecryptPackage(byte[] encryptedPackage, byte[] documentKey, byte[] salt)
    {
        if (encryptedPackage.Length < 8)
            throw new PptxEncryptedException("EncryptedPackage too short.");

        var originalSize = (int)BinaryPrimitives.ReadInt64LittleEndian(encryptedPackage.AsSpan(0, 8));
        var result = new MemoryStream(originalSize);

        var pos = 8;
        var segIndex = 0;
        while (pos < encryptedPackage.Length)
        {
            var remaining = encryptedPackage.Length - pos;
            var segLen = Math.Min(SegmentSize, remaining);
            var paddedLen = PadTo16(segLen);

            var segment = new byte[paddedLen];
            var toCopy = Math.Min(paddedLen, remaining);
            Array.Copy(encryptedPackage, pos, segment, 0, toCopy);

            var iv = SegmentIv(salt, segIndex);
            result.Write(AesCbcDecryptNoPad(segment, documentKey, iv));

            pos += paddedLen; // advance by full padded size
            segIndex++;
        }

        var raw = result.ToArray();
        if (raw.Length > originalSize) Array.Resize(ref raw, originalSize);
        return raw;
    }

    private static byte[] SegmentIv(byte[] salt, int segmentIndex)
    {
        var input = new byte[salt.Length + 4];
        salt.CopyTo(input, 0);
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(salt.Length), segmentIndex);
        var hash = SHA512.HashData(input);
        var iv = new byte[BlockSize];
        Array.Copy(hash, iv, BlockSize);
        return iv;
    }

    // ── AES helpers ───────────────────────────────────────────────────────────

    private static byte[] AesCbcEncryptZeroPad(byte[] plaintext, byte[] key, byte[] iv)
    {
        var padded = new byte[PadTo16(plaintext.Length)];
        plaintext.CopyTo(padded, 0);
        return AesCbcEncryptNoPad(padded, key, iv);
    }

    private static byte[] AesCbcEncryptNoPad(byte[] plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var enc = aes.CreateEncryptor();
        return enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }

    private static byte[] AesCbcDecryptNoPad(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private static byte[] AesCbcDecryptTruncate(byte[] ciphertext, byte[] key, byte[] iv, int expectedLength)
    {
        var padded = new byte[PadTo16(ciphertext.Length)];
        ciphertext.CopyTo(padded, 0);
        var decrypted = AesCbcDecryptNoPad(padded, key, iv);
        if (decrypted.Length > expectedLength)
            Array.Resize(ref decrypted, expectedLength);
        return decrypted;
    }

    // ── EncryptionInfo XML ────────────────────────────────────────────────────

    private static byte[] BuildEncryptionInfoXml(
        byte[] documentSalt, byte[] passwordSalt, byte[] encryptedKeyValue,
        byte[] encryptedVerifierInput, byte[] encryptedVerifierHash,
        byte[] encryptedHmacKey, byte[] encryptedHmacValue)
    {
        var enc = XNamespace.Get("http://schemas.microsoft.com/office/2006/encryption");
        var pwd = XNamespace.Get("http://schemas.microsoft.com/office/2006/keyEncryptor/password");

        var encEl = new XElement(enc + "encryption",
            new XAttribute(XNamespace.Xmlns + "p", pwd.NamespaceName));

        encEl.Add(new XElement(enc + "keyData",
            new XAttribute("saltSize", SaltSize),
            new XAttribute("blockSize", BlockSize),
            new XAttribute("keyBits", KeyBytes * 8),
            new XAttribute("hashSize", 64),
            new XAttribute("cipherAlgorithm", "AES"),
            new XAttribute("cipherChaining", "ChainingModeCBC"),
            new XAttribute("hashAlgorithm", "SHA512"),
            new XAttribute("saltValue", Convert.ToBase64String(documentSalt))));

        encEl.Add(new XElement(enc + "dataIntegrity",
            new XAttribute("encryptedHmacKey", Convert.ToBase64String(encryptedHmacKey)),
            new XAttribute("encryptedHmacValue", Convert.ToBase64String(encryptedHmacValue))));

        var keyEncryptor = new XElement(enc + "keyEncryptor",
            new XAttribute("uri", "http://schemas.microsoft.com/office/2006/keyEncryptor/password"));
        keyEncryptor.Add(new XElement(pwd + "encryptedKey",
            new XAttribute("spinCount", SpinCount),
            new XAttribute("saltSize", SaltSize),
            new XAttribute("blockSize", BlockSize),
            new XAttribute("keyBits", KeyBytes * 8),
            new XAttribute("hashSize", 64),
            new XAttribute("cipherAlgorithm", "AES"),
            new XAttribute("cipherChaining", "ChainingModeCBC"),
            new XAttribute("hashAlgorithm", "SHA512"),
            new XAttribute("saltValue", Convert.ToBase64String(passwordSalt)),
            new XAttribute("encryptedVerifierHashInput", Convert.ToBase64String(encryptedVerifierInput)),
            new XAttribute("encryptedVerifierHash", Convert.ToBase64String(encryptedVerifierHash)),
            new XAttribute("encryptedKeyValue", Convert.ToBase64String(encryptedKeyValue))));

        encEl.Add(new XElement(enc + "keyEncryptors", keyEncryptor));

        return Encoding.UTF8.GetBytes(
            new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), encEl).ToString());
    }

    private static (byte[] documentSalt, byte[] passwordSalt, int spinCount,
                   byte[] encryptedKeyValue, byte[] encryptedVerifierInput,
                   byte[] encryptedVerifierHash)
        ParseEncryptionInfo(ReadOnlySpan<byte> xmlSpan)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(xmlSpan));
        var enc = XNamespace.Get("http://schemas.microsoft.com/office/2006/encryption");
        var pwd = XNamespace.Get("http://schemas.microsoft.com/office/2006/keyEncryptor/password");

        var keyDataEl = doc.Root?.Element(enc + "keyData")
            ?? throw new PptxEncryptedException("Missing keyData element.");
        var encKeyEl = doc.Root?.Descendants(pwd + "encryptedKey").FirstOrDefault()
            ?? throw new PptxEncryptedException("Missing encryptedKey element.");

        static byte[] B64(XElement el, string attr) =>
            Convert.FromBase64String((string?)el.Attribute(attr)
                ?? throw new PptxEncryptedException($"Missing attribute: {attr}"));

        return (
            documentSalt: B64(keyDataEl, "saltValue"),
            passwordSalt: B64(encKeyEl, "saltValue"),
            spinCount: (int?)encKeyEl.Attribute("spinCount") ?? SpinCount,
            encryptedKeyValue: B64(encKeyEl, "encryptedKeyValue"),
            encryptedVerifierInput: B64(encKeyEl, "encryptedVerifierHashInput"),
            encryptedVerifierHash: B64(encKeyEl, "encryptedVerifierHash"));
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    private static int PadTo16(int length) => (length + 15) & ~15;
}
