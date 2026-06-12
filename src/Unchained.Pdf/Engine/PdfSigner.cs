using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Writing;

namespace Unchained.Pdf.Engine;

/// <summary>
/// Creates digitally signed PDF documents using PKCS#7 detached signatures
/// per ISO 32000-1 §12.8.3 (SubFilter <c>adbe.pkcs7.detached</c>).
/// <para>
/// Uses a two-pass approach: write the PDF with fixed-size placeholders, locate
/// their byte offsets, compute the signature over the two byte ranges, and patch
/// the placeholders in-place. Document size is preserved byte-for-byte.
/// </para>
/// </summary>
internal static class PdfSigner
{
    // Bytes reserved for the PKCS#7 DER signature blob. 8 KB is sufficient for
    // typical certificate chains (1–3 certs, RSA-2048 or EC-256).
    private const int SignatureReservedBytes = 8192;
    // Hex representation: 2 chars per byte, plus < and >
    private const int ContentsHexLen = SignatureReservedBytes * 2 + 2; // <hex> total

    // Placeholder values written to /ByteRange before the true byte offsets are known;
    // replaced with actual values after the document is serialized.
    private const long ByteRangeSentinel0 = 1111111111L;
    private const long ByteRangeSentinel1 = 2222222222L;
    private const long ByteRangeSentinel2 = 3333333333L;
    private const long ByteRangeSentinel3 = 4444444444L;

    // Sentinel integers for the /ByteRange array placeholder.
    // Four different 10-digit numbers give a unique, easily searchable byte sequence.
    private static readonly PdfArray ByteRangePlaceholder = new([
        new PdfInteger(ByteRangeSentinel0),
        new PdfInteger(ByteRangeSentinel1),
        new PdfInteger(ByteRangeSentinel2),
        new PdfInteger(ByteRangeSentinel3)
    ]);

    // PdfWriter format: [1111111111 2222222222 3333333333 4444444444]
    // = 1 + 10 + 1 + 10 + 1 + 10 + 1 + 10 + 1 = 45 chars
    private const int ByteRangePlaceholderLen = 45;

    // ── Public entry point ────────────────────────────────────────────────────

    internal static byte[] Sign(PdfDocumentCore core, X509Certificate2 certificate, SignatureOptions opts)
    {
        if (core.IsEncrypted)
            throw new InvalidOperationException("Cannot sign an encrypted PDF. Sign before encrypting, or decrypt first.");

        // ── Step 1: collect existing objects ──────────────────────────────────
        var objects = core.CollectObjects().ToList();
        var maxObj = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;

        // ── Step 2: create signature objects ──────────────────────────────────
        var sigValueNum = maxObj + 1;
        var sigFieldNum = maxObj + 2;
        var sigValueRef = new PdfIndirectReference(sigValueNum, 0);
        var sigFieldRef = new PdfIndirectReference(sigFieldNum, 0);

        var now = opts.SigningTime ?? DateTimeOffset.UtcNow;

        var sigValueDict = BuildSignatureValueDict(opts, now);
        var sigFieldDict = BuildSignatureFieldDict(sigValueRef, opts.FieldName);

        // ── Step 3: update catalog (add AcroForm with sig field) ─────────────
        UpdateCatalog(objects, core, sigFieldRef);

        // ── Step 4: assemble all objects and write first pass ─────────────────
        objects.Add(new PdfIndirectObject(sigValueNum, 0, sigValueDict));
        objects.Add(new PdfIndirectObject(sigFieldNum, 0, sigFieldDict));

        var trailer = BuildTrailer(objects, core);
        var buf = WriteObjects(objects, trailer);

        // ── Step 5: locate the /Contents placeholder (<0000…0000>) ───────────
        var contentsStart = FindContentsPlaceholder(buf);
        if (contentsStart < 0)
            throw new InvalidOperationException("Signature /Contents placeholder not found in serialized PDF.");

        // contentsStart = index of '<', contentsEnd = index after '>'
        var contentsEnd = contentsStart + ContentsHexLen;

        // ── Step 6: patch /ByteRange with actual byte offsets ─────────────────
        const long off0 = 0;
        long len0 = contentsStart;
        long off1 = contentsEnd, len1 = buf.Length - contentsEnd;

        PatchByteRange(buf, off0, len0, off1, len1);

        // ── Step 7: hash the two byte ranges ──────────────────────────────────
        var hashInput = new byte[len0 + len1];
        buf.AsSpan(0, (int)len0).CopyTo(hashInput);
        buf.AsSpan((int)off1, (int)len1).CopyTo(hashInput.AsSpan((int)len0));

        // ── Step 8: create PKCS#7 detached signature ──────────────────────────
        // Pass the same `now` used for /M so the Pkcs9SigningTime attribute and the
        // /M dictionary entry are always identical — a single UtcNow call for the
        // whole Sign operation eliminates any cross-second-boundary inconsistency.
        var signatureBytes = CreatePkcs7Signature(hashInput, certificate, opts, now);
        if (signatureBytes.Length > SignatureReservedBytes)
        {
            throw new InvalidOperationException(
                $"PKCS#7 signature ({signatureBytes.Length} bytes) exceeds reserved space ({SignatureReservedBytes} bytes). " +
                "Increase SignatureReservedBytes or reduce certificate chain depth.");
        }

        // ── Step 9: patch /Contents with the actual signature ─────────────────
        PatchContents(buf, contentsStart, signatureBytes);

        return buf;
    }

    // ── Object builders ───────────────────────────────────────────────────────

    private static PdfDictionary BuildSignatureValueDict(SignatureOptions opts, DateTimeOffset now)
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Sig"),
            ["Filter"] = PdfName.Get("Adobe.PPKLite"),
            // ReSharper disable once StringLiteralTypo
            ["SubFilter"] = PdfName.Get("adbe.pkcs7.detached"),
            // Placeholder array — patched in Step 6
            ["ByteRange"] = ByteRangePlaceholder,
            // Placeholder hex-zero string — patched in Step 9
            ["Contents"] = new PdfString(new byte[SignatureReservedBytes], isHex: true),
            ["M"] = PdfString.FromLatin1(FormatPdfDate(now))
        };

        if (opts.Reason is not null)
            entries["Reason"] = PdfString.FromLatin1(opts.Reason);
        if (opts.Location is not null)
            entries["Location"] = PdfString.FromLatin1(opts.Location);
        if (opts.ContactInfo is not null)
            entries["ContactInfo"] = PdfString.FromLatin1(opts.ContactInfo);

        return new PdfDictionary(entries);
    }

    private static PdfDictionary BuildSignatureFieldDict(PdfObject sigValueRef, string fieldName) =>
        new(new Dictionary<string, PdfObject>
        {
            ["FT"] = PdfName.Get("Sig"),
            ["Type"] = PdfName.Get("Annot"),
            ["Subtype"] = PdfName.Get("Widget"),
            ["T"] = PdfString.FromLatin1(fieldName),
            ["V"] = sigValueRef,
            // Zero-size invisible widget — no visual appearance required
            ["Rect"] = new PdfArray([
                new PdfInteger(0), new PdfInteger(0),
                new PdfInteger(0), new PdfInteger(0)
            ]),
            ["F"] = new PdfInteger(4) // Print flag
        });

    // ── Catalog mutation ──────────────────────────────────────────────────────

    private static void UpdateCatalog(List<PdfIndirectObject> objects, PdfDocumentCore core, PdfObject sigFieldRef)
    {
        if (core.Trailer[PdfName.Root] is not PdfIndirectReference catalogRef)
            return;

        var idx = objects.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (idx < 0)
            return;

        if (objects[idx].Value is not PdfDictionary catalogDict)
            return;

        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        // Build or update /AcroForm
        var acroForm = BuildOrUpdateAcroForm(catalogEntries.GetValueOrDefault("AcroForm"), sigFieldRef, core);
        catalogEntries["AcroForm"] = acroForm;

        objects[idx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(catalogEntries));
    }

    private static PdfDictionary BuildOrUpdateAcroForm(PdfObject? existing, PdfObject sigFieldRef, PdfDocumentCore core)
    {
        // Resolve existing AcroForm if it's an indirect reference
        var existingDict = existing switch
        {
            PdfDictionary d => d,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
            _ => null
        };

        // Start from existing entries, or an empty dict
        var entries = existingDict is not null
            ? new Dictionary<string, PdfObject>(existingDict.Entries)
            : new Dictionary<string, PdfObject>();

        // Append sig field to /Fields
        var fields = entries.GetValueOrDefault("Fields") switch
        {
            PdfArray a => [.. a.Elements, sigFieldRef],
            _ => new[] { sigFieldRef }
        };
        entries["Fields"] = new PdfArray(fields);

        // /SigFlags bit 1 = Append Only, bit 2 = Has Signatures → value 3
        var existingSigFlags = (int)(entries.GetValueOrDefault("SigFlags") is PdfInteger sf ? sf.Value : 0);
        entries["SigFlags"] = new PdfInteger(existingSigFlags | 3);

        return new PdfDictionary(entries);
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private static PdfDictionary BuildTrailer(IReadOnlyCollection<PdfIndirectObject> objects, PdfDocumentCore core)
    {
        var maxObj = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
        var entries = new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(maxObj + 1),
            [PdfName.Root.Value] = core.Trailer[PdfName.Root]!
        };
        if (core.Trailer[PdfName.Info] is { } info)
            entries[PdfName.Info.Value] = info;

        return new PdfDictionary(entries);
    }

    private static byte[] WriteObjects(IReadOnlyList<PdfIndirectObject> objects, PdfDictionary trailer)
    {
        var buf = new ArrayBufferWriter<byte>();
        using var writer = new PdfWriter(buf);
        writer.Write(objects, trailer);

        return buf.WrittenMemory.ToArray();
    }

    // ── Placeholder location ──────────────────────────────────────────────────

    // Searches for '<' followed by (SignatureReservedBytes * 2) hex zero chars followed by '>'.
    // Uses a short prefix scan to avoid full O(n²) search.
    private static int FindContentsPlaceholder(byte[] buf)
    {
        // Search for '<' (0x3C) + at least 8 '0' chars (0x30) — unique enough as an anchor
        var anchor = "<00000000"u8;
        for (var i = 0; i <= buf.Length - ContentsHexLen; i++)
        {
            if (!buf.AsSpan(i, anchor.Length).SequenceEqual(anchor)) continue;

            // Verify full placeholder: '<' + (ReservedBytes*2) zeros + '>'
            if (buf[i] != '<')
                continue;

            var allZeros = true;
            for (var j = 1; j <= SignatureReservedBytes * 2; j++)
            {
                if (buf[i + j] == (byte)'0')
                    continue;

                allZeros = false;
                break;
            }

            if (!allZeros)
                continue;

            if (buf[i + SignatureReservedBytes * 2 + 1] != '>')
                continue;

            return i;
        }

        return -1;
    }

    // ── Patching ──────────────────────────────────────────────────────────────

    // Replaces the sentinel [1111111111 2222222222 3333333333 4444444444] with actual values.
    // ReSharper disable BadListLineBreaks
    private static void PatchByteRange(byte[] buf, long off0, long len0, long off1, long len1)
        // ReSharper restore BadListLineBreaks
    {
        // Build the replacement string, padded to ByteRangePlaceholderLen
        var replacement = $"[{off0} {len0} {off1} {len1}]";
        replacement = replacement.PadRight(ByteRangePlaceholderLen);
        var replacementBytes = Encoding.ASCII.GetBytes(replacement);

        // Sentinel bytes to search for
        var sentinel = $"[{ByteRangeSentinel0} {ByteRangeSentinel1} {ByteRangeSentinel2} {ByteRangeSentinel3}]";
        var sentinelBytes = Encoding.ASCII.GetBytes(sentinel);

        var idx = buf.AsSpan().IndexOf(sentinelBytes);
        if (idx < 0)
            throw new InvalidOperationException("/ByteRange placeholder not found in serialized PDF.");

        replacementBytes.CopyTo(buf, idx);
    }

    // Hex-encodes the signature bytes into the <0000...> placeholder.
    // Remaining zeros pad the rest of the reserved space.
    private static void PatchContents(IList<byte> buf, int contentsStart, IEnumerable<byte> signatureBytes)
    {
        // contentsStart = index of '<'
        // contentsStart + 1 = first hex char
        var pos = contentsStart + 1;

        foreach (var hex in signatureBytes.Select(static t => t.ToString("X2")))
        {
            buf[pos++] = (byte)hex[0];
            buf[pos++] = (byte)hex[1];
        }
        // Remaining bytes are already '0' from the placeholder — no need to clear.
    }

    // ── PKCS#7 / CMS ─────────────────────────────────────────────────────────

    private static byte[] CreatePkcs7Signature(byte[] content, X509Certificate2 certificate, SignatureOptions opts, DateTimeOffset now)
    {
        var contentInfo = new ContentInfo(content);
        var cms = new SignedCms(contentInfo, detached: true);

        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
            IncludeOption = opts.CertificateIncludeOption
        };

        // Use the same timestamp that was written to /M so the Pkcs9SigningTime
        // attribute is always consistent with the /M entry in the sig dictionary.
        signer.SignedAttributes.Add(new Pkcs9SigningTime(now.UtcDateTime));

        cms.ComputeSignature(signer);

        return cms.Encode();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    // ReSharper disable once CommentTypo
    // PDF date format: D:YYYYMMDDHHmmSSZ (UTC)
    private static string FormatPdfDate(DateTimeOffset dt)
    {
        var u = dt.UtcDateTime;
        return $"D:{u:yyyyMMddHHmmss}Z";
    }
}
