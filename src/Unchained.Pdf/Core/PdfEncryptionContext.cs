using Unchained.Pdf.Models;

namespace Unchained.Pdf.Core;

/// <summary>
/// Holds the active encryption key and algorithm for a PDF document.
/// Used both during reading (transparent decryption) and writing (transparent encryption).
/// </summary>
internal sealed class PdfEncryptionContext
{
    private readonly byte[] _fileKey;
    private readonly PdfEncryptionAlgorithm _algorithm;

    internal PdfEncryptionContext(byte[] fileKey, PdfEncryptionAlgorithm algorithm, PdfPermissions permissions = PdfPermissions.All)
    {
        _fileKey = fileKey;
        _algorithm = algorithm;
        Permissions = permissions;
        Algorithm = algorithm;
    }

    /// <summary>The encryption algorithm used to protect the document.</summary>
    internal PdfEncryptionAlgorithm Algorithm { get; }

    /// <summary>
    /// Operations permitted when the document is opened with the user password.
    /// Always <see cref="PdfPermissions.All"/> on the write path (owner has full access).
    /// </summary>
    internal PdfPermissions Permissions { get; }

    // ── Stream data encrypt / decrypt ─────────────────────────────────────────

    /// <summary>Encrypts stream data. Returns IV + ciphertext for AES, or RC4 output for RC4.</summary>
    internal byte[] EncryptStream(ReadOnlySpan<byte> data, int objNum, int genNum)
    {
        if (_algorithm == PdfEncryptionAlgorithm.Aes256)
            return PdfEncryption.AesEncryptCbcWithIv(_fileKey, data.ToArray());

        var key = PdfEncryption.DeriveObjectKey(_fileKey, objNum, genNum, isAes: _algorithm == PdfEncryptionAlgorithm.Aes128);

        return _algorithm == PdfEncryptionAlgorithm.Aes128
            ? PdfEncryption.AesEncryptCbcWithIv(key, data.ToArray())
            : PdfEncryption.Rc4(key, data.ToArray());
    }

    /// <summary>Decrypts stream data. Handles IV-prefix for AES; RC4 is self-inverse.</summary>
    internal byte[] DecryptStream(ReadOnlySpan<byte> data, int objNum, int genNum)
    {
        if (data.IsEmpty) return [];

        if (_algorithm == PdfEncryptionAlgorithm.Aes256)
        {
            return data.Length < 16
                ? data.ToArray() // too short to be valid AES
                : PdfEncryption.AesDecryptCbc(_fileKey, data[..16].ToArray(), data[16..].ToArray());
        }

        var key = PdfEncryption.DeriveObjectKey(_fileKey, objNum, genNum, isAes: _algorithm == PdfEncryptionAlgorithm.Aes128);

        return _algorithm != PdfEncryptionAlgorithm.Aes128
            ? PdfEncryption.Rc4(key, data.ToArray())
            : data.Length < 16
                ? data.ToArray()
                : PdfEncryption.AesDecryptCbc(key, data[..16].ToArray(), data[16..].ToArray());
    }

    // ── String encrypt / decrypt ──────────────────────────────────────────────

    /// <summary>Encrypts a raw string byte array.</summary>
    private byte[] EncryptString(ReadOnlySpan<byte> data, int objNum, int genNum)
        => EncryptStream(data, objNum, genNum); // same algorithm, same key derivation

    /// <summary>Decrypts a raw string byte array.</summary>
    private byte[] DecryptString(ReadOnlySpan<byte> data, int objNum, int genNum)
        => DecryptStream(data, objNum, genNum);

    // ── Object-tree walk: decrypt all strings and streams ────────────────────

    /// <summary>
    /// Returns a new <see cref="PdfIndirectObject"/> with all <see cref="PdfString"/> values
    /// and <see cref="PdfStream"/> data decrypted. The object number and generation are used
    /// to derive the per-object key for V≤4 algorithms.
    /// </summary>
    internal PdfIndirectObject DecryptObject(PdfIndirectObject obj)
    {
        var decrypted = DecryptValue(obj.Value, obj.ObjectNumber, obj.Generation);

        return decrypted == obj.Value ? obj : new PdfIndirectObject(obj.ObjectNumber, obj.Generation, decrypted);
    }

    /// <summary>
    /// Returns a new <see cref="PdfIndirectObject"/> with all <see cref="PdfString"/> values
    /// and <see cref="PdfStream"/> data encrypted. Used during the write path.
    /// </summary>
    internal PdfIndirectObject EncryptObject(PdfIndirectObject obj)
    {
        var encrypted = EncryptValue(obj.Value, obj.ObjectNumber, obj.Generation);

        return encrypted == obj.Value ? obj : new PdfIndirectObject(obj.ObjectNumber, obj.Generation, encrypted);
    }

    private PdfObject DecryptValue(PdfObject value, int objNum, int gen) => value switch
    {
        PdfString s => new PdfString(DecryptString(s.Bytes.Span, objNum, gen), s.IsHex),
        PdfStream st => DecryptPdfStream(st, objNum, gen),
        PdfDictionary d => DecryptDictionary(d, objNum, gen),
        PdfArray a => DecryptArray(a, objNum, gen),
        _ => value
    };

    private PdfObject EncryptValue(PdfObject value, int objNum, int gen) => value switch
    {
        // Always use hex encoding for encrypted strings: binary AES output is not safe
        // in literal (parenthesis) strings due to potential null-byte and escape issues.
        PdfString s => new PdfString(EncryptString(s.Bytes.Span, objNum, gen), isHex: true),
        PdfStream st => EncryptPdfStream(st, objNum, gen),
        PdfDictionary d => EncryptDictionary(d, objNum, gen),
        PdfArray a => EncryptArray(a, objNum, gen),
        _ => value
    };

    private PdfStream DecryptPdfStream(PdfStream stream, int objNum, int gen)
    {
        var decrypted = DecryptStream(stream.Data.Span, objNum, gen);
        var newDict = DecryptDictionary(stream.Dictionary, objNum, gen);
        // Update /Length to match decrypted size
        var dictEntries = new Dictionary<string, PdfObject>(newDict.Entries)
        {
            ["Length"] = new PdfInteger(decrypted.Length)
        };

        return new PdfStream(new PdfDictionary(dictEntries), decrypted);
    }

    private PdfStream EncryptPdfStream(PdfStream stream, int objNum, int gen)
    {
        // Don't double-encrypt streams that already have /Filter
        var encrypted = EncryptStream(stream.Data.Span, objNum, gen);
        var newDict = EncryptDictionary(stream.Dictionary, objNum, gen);
        var dictEntries = new Dictionary<string, PdfObject>(newDict.Entries)
        {
            ["Length"] = new PdfInteger(encrypted.Length)
        };

        return new PdfStream(new PdfDictionary(dictEntries), encrypted);
    }

    private PdfDictionary DecryptDictionary(PdfDictionary dict, int objNum, int gen)
    {
        var changed = false;
        var entries = new Dictionary<string, PdfObject>(dict.Entries.Count);
        foreach (var (k, v) in dict.Entries)
        {
            var newV = DecryptValue(v, objNum, gen);
            entries[k] = newV;

            if (!ReferenceEquals(newV, v))
                changed = true;
        }

        return changed ? new PdfDictionary(entries) : dict;
    }

    private PdfDictionary EncryptDictionary(PdfDictionary dict, int objNum, int gen)
    {
        var changed = false;
        var entries = new Dictionary<string, PdfObject>(dict.Entries.Count);
        foreach (var (k, v) in dict.Entries)
        {
            // Don't encrypt /Contents entry in signature dicts or /Encrypt dict keys
            var newV = EncryptValue(v, objNum, gen);
            entries[k] = newV;
            if (!ReferenceEquals(newV, v))
                changed = true;
        }

        return changed ? new PdfDictionary(entries) : dict;
    }

    private PdfArray DecryptArray(PdfArray array, int objNum, int gen)
    {
        var elements = array.Elements.Select(e => DecryptValue(e, objNum, gen)).ToArray();
        return elements.Zip(array.Elements, ReferenceEquals).All(static x => x)
            ? array
            : new PdfArray(elements);
    }

    private PdfArray EncryptArray(PdfArray array, int objNum, int gen)
    {
        var elements = array.Elements.Select(e => EncryptValue(e, objNum, gen)).ToArray();
        return elements.Zip(array.Elements, ReferenceEquals).All(static x => x)
            ? array
            : new PdfArray(elements);
    }
}
