using System.Security.Cryptography;
using System.Text;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Core;

/// <summary>
///     PDF Standard Security Handler cryptographic operations (ISO 32000-1 §7.6 and ISO 32000-2 §7.6).
///     Supports RC4-128 (V=2, R=3), AES-128 (V=4, R=4), and AES-256 (V=5, R=6) for reading;
///     AES-256 (V=5, R=6) for writing.
/// </summary>
internal static class PdfEncryption
{
    // PDF §7.6.3.3 password padding constant.
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];

    // ── Read path: derive context from /Encrypt dict + password ──────────────

    /// <summary>
    ///     Creates an <see cref="PdfEncryptionContext" /> for reading an encrypted document.
    ///     Returns <see langword="null" /> when the encryption handler or revision is unsupported.
    ///     Throws <see cref="PdfEncryptedException" /> when the password is invalid.
    /// </summary>
    internal static PdfEncryptionContext? CreateReadContext(PdfDictionary encryptDict, byte[] fileId, string password)
    {
        var filter = encryptDict.GetName("Filter");
        if (filter != "Standard") return null; // unsupported handler

        var v = (int)(encryptDict.Get<PdfInteger>("V")?.Value ?? 0);
        var r = (int)(encryptDict.Get<PdfInteger>("R")?.Value ?? 0);
        var keyBits = (int)(encryptDict.Get<PdfInteger>("Length")?.Value ?? (v == 1 ? 40 : 128));

        var oBytes = GetStringBytes(encryptDict, "O");
        var uBytes = GetStringBytes(encryptDict, "U");
        var pFlags = (int)(encryptDict.Get<PdfInteger>("P")?.Value ?? -3904);
        var permissions = DecodePermissions(pFlags);

        switch (v)
        {
            case 5 when r >= 5:
            {
                // AES-256 (V=5, R=5/6 — PDF 2.0)
                var ueBytes = GetStringBytes(encryptDict, "UE");
                var oeBytes = GetStringBytes(encryptDict, "OE");
                // ReSharper disable BadListLineBreaks
                var fileKey = DeriveKeyV5(password,
                                  uBytes,
                                  ueBytes,
                                  oBytes,
                                  oeBytes,
                                  false)
                              ?? DeriveKeyV5(password,
                                  uBytes,
                                  ueBytes,
                                  oBytes,
                                  oeBytes,
                                  true);
                // ReSharper restore BadListLineBreaks

                return fileKey is null
                    ? throw new PdfEncryptedException("Incorrect password for AES-256 encrypted PDF.")
                    : new PdfEncryptionContext(fileKey, PdfEncryptionAlgorithm.Aes256, permissions);
            }
            case <= 4:
            {
                // RC4-40, RC4-128, or AES-128 (V=1..4)
                var algo = v == 4 ? PdfEncryptionAlgorithm.Aes128 : PdfEncryptionAlgorithm.Rc4_128;
                var keyLen = v == 1 ? 5 : Math.Clamp(keyBits / 8, 5, 16);

                // ReSharper disable once BadListLineBreaks
                var encKey = DeriveKeyV2V4(password,
                    oBytes,
                    pFlags,
                    fileId,
                    r,
                    keyLen);
                if (ValidateUserPasswordV2V4(encKey, uBytes, fileId, r))
                    return new PdfEncryptionContext(encKey, algo, permissions);

                // Try with empty password before failing
                // ReSharper disable once BadListLineBreaks
                var emptyKey = DeriveKeyV2V4(string.Empty,
                    oBytes,
                    pFlags,
                    fileId,
                    r,
                    keyLen);

                return !ValidateUserPasswordV2V4(emptyKey, uBytes, fileId, r)
                    ? throw new PdfEncryptedException("Incorrect password for encrypted PDF.")
                    : new PdfEncryptionContext(emptyKey, algo, permissions);
            }
            default:
                return null; // revision not supported
        }
    }

    // ── Write path: generate /Encrypt dict + context for AES-256 ─────────────

    /// <summary>
    ///     Creates a write-path <see cref="PdfEncryptionContext" /> and the corresponding
    ///     <c>/Encrypt</c> dictionary for AES-256 (V=5, R=6).
    /// </summary>
    internal static (PdfEncryptionContext Context, PdfDictionary EncryptDict) CreateWriteContext(EncryptionOptions opts, byte[] fileId)
    {
        // Generate 32-byte random file encryption key.
        var fileKey = RandomNumberGenerator.GetBytes(32);

        var up = NormalizePasswordV5(opts.UserPassword);
        var op = NormalizePasswordV5(opts.OwnerPassword.Length > 0 ? opts.OwnerPassword : opts.UserPassword);

        var (uBytes, ueBytes) = ComputeUUE_V5(up, fileKey);
        var (oBytes, oeBytes) = ComputeOOE_V5(op, uBytes, fileKey);
        var permsBytes = ComputePerms_V5(fileKey, (int)opts.Permissions);

        // All binary values in the /Encrypt dict use hex encoding for safe round-trip.
        var encryptDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Filter"] = PdfName.Get("Standard"),
            ["V"] = new PdfInteger(5),
            ["R"] = new PdfInteger(6),
            ["Length"] = new PdfInteger(256),
            ["P"] = new PdfInteger(EncodePermissions(opts.Permissions)),
            ["O"] = new PdfString(oBytes, true),
            ["U"] = new PdfString(uBytes, true),
            ["OE"] = new PdfString(oeBytes, true),
            ["UE"] = new PdfString(ueBytes, true),
            ["Perms"] = new PdfString(permsBytes, true),
            ["EncryptMetadata"] = PdfBoolean.True,
            ["StmF"] = PdfName.Get("StdCF"),
            ["StrF"] = PdfName.Get("StdCF"),
            ["CF"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["StdCF"] = new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    ["CFM"] = PdfName.Get("AESV3"),
                    ["AuthEvent"] = PdfName.Get("DocOpen"),
                    ["Length"] = new PdfInteger(32)
                })
            })
        });

        var context = new PdfEncryptionContext(fileKey, PdfEncryptionAlgorithm.Aes256);
        return (context, encryptDict);
    }

    // ── AES-256 (V=5) key derivation ─────────────────────────────────────────

    private static byte[]? DeriveKeyV5(
        string password,
        byte[] uBytes,
        byte[] ueBytes,
        byte[] oBytes,
        byte[] oeBytes,
        bool isOwner
    )
    {
        if (uBytes.Length < 48 || ueBytes.Length < 32)
            return null;

        if (isOwner && (oBytes.Length < 48 || oeBytes.Length < 32))
            return null;

        var pw = NormalizePasswordV5(password);

        if (isOwner)
        {
            // Test: SHA-256(pw + O[32..39] + U)
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash([.. pw, .. oBytes[32..40], .. uBytes]);
            if (!hash.AsSpan().SequenceEqual(oBytes.AsSpan(0, 32)))
                return null;

            // Derive: file_key = AES-Decrypt(SHA-256(pw + O[40..47] + U), iv=0, OE)
            var kk = sha.ComputeHash([.. pw, .. oBytes[40..48], .. uBytes]);
            return AesDecryptBlock(kk, new byte[16], oeBytes);
        }
        else
        {
            // Test: SHA-256(pw + U[32..39])
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash([.. pw, .. uBytes[32..40]]);
            if (!hash.AsSpan().SequenceEqual(uBytes.AsSpan(0, 32))) return null;

            // Derive: file_key = AES-Decrypt(SHA-256(pw + U[40..47]), iv=0, UE)
            var kk = sha.ComputeHash([.. pw, .. uBytes[40..48]]);
            return AesDecryptBlock(kk, new byte[16], ueBytes);
        }
    }

    private static (byte[] U, byte[] UE) ComputeUUE_V5(byte[] pw, byte[] fileKey)
    {
        var vs = RandomNumberGenerator.GetBytes(8); // user validation salt
        var ks = RandomNumberGenerator.GetBytes(8); // user key salt

        using var sha = SHA256.Create();
        var uHash = sha.ComputeHash([.. pw, .. vs]); // 32 bytes
        var uBytes = new byte[48];
        uHash.CopyTo(uBytes, 0);
        vs.CopyTo(uBytes, 32);
        ks.CopyTo(uBytes, 40);

        var ueKey = sha.ComputeHash([.. pw, .. ks]);
        var ueBytes = AesEncryptBlock(ueKey, new byte[16], fileKey);

        return (uBytes, ueBytes);
    }

    private static (byte[] O, byte[] OE) ComputeOOE_V5(byte[] ownerPw, byte[] uBytes, byte[] fileKey)
    {
        var vs = RandomNumberGenerator.GetBytes(8);
        var ks = RandomNumberGenerator.GetBytes(8);

        using var sha = SHA256.Create();
        var oHash = sha.ComputeHash([.. ownerPw, .. vs, .. uBytes]); // 32 bytes
        var oBytes = new byte[48];
        oHash.CopyTo(oBytes, 0);
        vs.CopyTo(oBytes, 32);
        ks.CopyTo(oBytes, 40);

        var oeKey = sha.ComputeHash([.. ownerPw, .. ks, .. uBytes]);
        var oeBytes = AesEncryptBlock(oeKey, new byte[16], fileKey);

        return (oBytes, oeBytes);
    }

    private static byte[] ComputePerms_V5(byte[] fileKey, int pFlags)
    {
        // 16-byte plaintext: P(4 LE) + 0xFFFFFFFF + 'T' + 'adb' + 0x00×4
        var plain = new byte[16];
        plain[0] = (byte)(pFlags & 0xFF);
        plain[1] = (byte)((pFlags >> 8) & 0xFF);
        plain[2] = (byte)((pFlags >> 16) & 0xFF);
        plain[3] = (byte)((pFlags >> 24) & 0xFF);
        plain[4] = 0xFF;
        plain[5] = 0xFF;
        plain[6] = 0xFF;
        plain[7] = 0xFF;
        plain[8] = (byte)'T'; // EncryptMetadata = true
        plain[9] = (byte)'a';
        plain[10] = (byte)'d';
        plain[11] = (byte)'b';
        // bytes 12..15 = zeros (already)

        // AES-256-ECB encrypt (single block, no padding)
        using var aes = Aes.Create();
        aes.Key = fileKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        var enc = aes.EncryptEcb(plain, PaddingMode.None);

        return enc[..16];
    }

    // ── RC4/AES-128 (V≤4) key derivation ─────────────────────────────────────

    // ReSharper disable BadListLineBreaks
    private static byte[] DeriveKeyV2V4(
            string password,
            byte[] oValue,
            int pFlags,
            byte[] fileId,
            int r,
            int keyLen
        )
        // ReSharper restore BadListLineBreaks
    {
        using var md5 = MD5.Create();

        // Step 1: pad/truncate password to 32 bytes
        var pwBytes = Encoding.Latin1.GetBytes(password);
        var padded = new byte[32];
        var copyLen = Math.Min(pwBytes.Length, 32);
        pwBytes.AsSpan(0, copyLen).CopyTo(padded);
        if (copyLen < 32)
            PasswordPadding.AsSpan(0, 32 - copyLen).CopyTo(padded.AsSpan(copyLen));

        // Step 2: MD5(padded + O + P(LE 4 bytes) + ID[0])
        var pBytes = new byte[4];
        pBytes[0] = (byte)(pFlags & 0xFF);
        pBytes[1] = (byte)((pFlags >> 8) & 0xFF);
        pBytes[2] = (byte)((pFlags >> 16) & 0xFF);
        pBytes[3] = (byte)((pFlags >> 24) & 0xFF);

        var id0 = fileId.Length >= 16 ? fileId[..16] : fileId.Concat(new byte[16 - fileId.Length]).ToArray();

        var hash = md5.ComputeHash([.. padded, .. oValue, .. pBytes, .. id0]);

        // Step 3: for R≥3, repeat MD5 50 times
        if (r < 3) return hash[..keyLen];

        for (var i = 0; i < 50; i++)
            hash = md5.ComputeHash(hash[..keyLen]);

        return hash[..keyLen];
    }

    // ReSharper disable once BadListLineBreaks
    private static bool ValidateUserPasswordV2V4(
        byte[] key,
        byte[] uValue,
        byte[] fileId,
        int r
    )
    {
        if (r == 2)
        {
            // RC4(padding_string, key) should == U[0..31]
            var expected = Rc4(key, PasswordPadding);
            return expected.AsSpan().SequenceEqual(uValue.AsSpan(0, Math.Min(32, uValue.Length)));
        }

        // R=3 or R=4
        using var md5 = MD5.Create();
        var id0 = fileId.Length >= 16 ? fileId[..16] : fileId.Concat(new byte[16 - fileId.Length]).ToArray();
        var hash = md5.ComputeHash([.. PasswordPadding, .. id0]);

        // Apply RC4 20 times with key XOR'd by iteration number
        var result = Rc4(key, hash);
        for (var i = 1; i <= 19; i++)
        {
            var keyI = key.Select(b => (byte)(b ^ i)).ToArray();
            result = Rc4(keyI, result);
        }

        return result.AsSpan().SequenceEqual(uValue.AsSpan(0, 16));
    }

    // ── Per-object key (V≤4) ─────────────────────────────────────────────────

    /// <summary>Derives the per-object encryption key for V≤4.</summary>
    // ReSharper disable once BadListLineBreaks
    internal static byte[] DeriveObjectKey(
        byte[] fileKey,
        int objNum,
        int genNum,
        bool isAes
    )
    {
        using var md5 = MD5.Create();
        var salt = isAes ? "sAlT"u8.ToArray() : [];
        var input = new byte[fileKey.Length + 3 + 2 + salt.Length];
        fileKey.CopyTo(input, 0);
        var off = fileKey.Length;
        input[off++] = (byte)(objNum & 0xFF);
        input[off++] = (byte)((objNum >> 8) & 0xFF);
        input[off++] = (byte)((objNum >> 16) & 0xFF);
        input[off++] = (byte)(genNum & 0xFF);
        input[off++] = (byte)((genNum >> 8) & 0xFF);
        salt.CopyTo(input, off);
        var hash = md5.ComputeHash(input);

        return hash[..Math.Min(16, fileKey.Length + 5)];
    }

    // ── AES helpers ───────────────────────────────────────────────────────────

    /// <summary>AES-CBC decrypt with the given key and IV. Returns plaintext with PKCS#7 removed.</summary>
    internal static byte[] AesDecryptCbc(byte[] key, byte[] iv, byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        return aes.DecryptCbc(ciphertext, iv, PaddingMode.PKCS7);
    }

    /// <summary>AES-CBC encrypt with the given key and IV. Returns IV + ciphertext (PKCS#7 padded).</summary>
    internal static byte[] AesEncryptCbcWithIv(byte[] key, byte[] data)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var cipher = aes.EncryptCbc(data, iv, PaddingMode.PKCS7);

        return [.. iv, .. cipher];
    }

    private static byte[] AesEncryptBlock(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        // Pad to 16-byte block
        var len = (data.Length + 15) / 16 * 16;
        var padded = new byte[len];
        data.CopyTo(padded, 0);

        return aes.EncryptCbc(padded, iv, PaddingMode.None);
    }

    private static byte[] AesDecryptBlock(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        return aes.DecryptCbc(data, iv, PaddingMode.None);
    }

    // ── RC4 ───────────────────────────────────────────────────────────────────

    internal static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = new byte[256];
        for (var i = 0; i < 256; i++) s[i] = (byte)i;
        var j = 0;

        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var result = new byte[data.Length];
        int x = 0, y = 0;
        for (var i = 0; i < data.Length; i++)
        {
            x = (x + 1) & 0xFF;
            y = (y + s[x]) & 0xFF;
            (s[x], s[y]) = (s[y], s[x]);
            result[i] = (byte)(data[i] ^ s[(s[x] + s[y]) & 0xFF]);
        }

        return result;
    }

    // ── Misc helpers ──────────────────────────────────────────────────────────

    private static byte[] NormalizePasswordV5(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);

        return bytes.Length <= 127 ? bytes : bytes[..127];
    }

    private static int EncodePermissions(PdfPermissions perms)
    {
        // PDF permission int: upper bits 1..30 = reserved as 1, lower 32 bits encode permissions.
        // The standard value has bits 1-2 set to 1, and bits 7-8 set to 1 (reserved).
        const int reservedBits = unchecked((int)0xFFFFF0C0); // bits 7-8 and 13-32 = 1
        return reservedBits | (int)perms;
    }

    // Extract only the bits defined in PdfPermissions from the raw /P integer.
    private static PdfPermissions DecodePermissions(int pFlags) =>
        (PdfPermissions)(pFlags & (int)PdfPermissions.All);

    private static byte[] GetStringBytes(PdfDictionary dict, string key)
    {
        if (dict.Get<PdfString>(key) is not { } s) return [];

        // PdfString.IsHex has two distinct origins:
        //   (a) Parsed from file: Bytes holds the raw hex ASCII chars → must decode.
        //   (b) Created in memory (CreateWriteContext): Bytes holds actual binary → no decoding.
        // Discriminate by checking whether ALL bytes are valid hex chars; binary data essentially
        // never satisfies this constraint (probability ≈ 10^-99 for 32+ random bytes).
        if (s is not { IsHex: true, Bytes.Length: > 0 } || !AllHexChars(s.Bytes.Span))
            return s.Bytes.ToArray();

        var hex = s.Bytes.Span;
        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = (byte)((HexNibble(hex[i * 2]) << 4) | HexNibble(hex[(i * 2) + 1]));

        return result;
    }

    private static bool AllHexChars(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (b is not (
                >= (byte)'0' and <= (byte)'9' or
                >= (byte)'A' and <= (byte)'F' or
                >= (byte)'a' and <= (byte)'f'))
                return false;
        }

        return true;
    }

    private static int HexNibble(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => 0
    };
}
