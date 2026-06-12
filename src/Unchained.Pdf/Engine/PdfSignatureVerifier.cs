using System.Globalization;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Reads and cryptographically verifies digital signatures embedded in a PDF document.
///     Supports <c>adbe.pkcs7.detached</c> signatures per ISO 32000-1 §12.8.3.
/// </summary>
internal static class PdfSignatureVerifier
{
    internal static IReadOnlyList<PdfSignatureInfo> Verify(byte[] pdfBytes, PdfDocumentCore core)
    {
        var results = new List<PdfSignatureInfo>();

        // Find all signature fields in the AcroForm
        var acroForm = ResolveAcroForm(core);
        if (acroForm is null) return results;

        var fields = acroForm.Get<PdfArray>("Fields");
        if (fields is null) return results;

        foreach (var fieldDict in fields.Elements
                     .Select(fieldElement => Resolve<PdfDictionary>(fieldElement, core))
                     .OfType<PdfDictionary>()
                     .Where(static fieldDict => fieldDict.GetName("FT") == "Sig"))
        {
            var fieldName = fieldDict.Get<PdfString>("T") is { } t
                ? Encoding.Latin1.GetString(t.Bytes.Span)
                : string.Empty;

            var sigValue = Resolve<PdfDictionary>(fieldDict["V"], core);
            if (sigValue is null)
                continue;

            results.Add(VerifySignature(pdfBytes, sigValue, fieldName));
        }

        return results;
    }

    private static PdfSignatureInfo VerifySignature(byte[] pdfBytes, PdfDictionary sigValue, string fieldName)
    {
        // Extract /ByteRange: [off0 len0 off1 len1]
        var byteRange = sigValue.Get<PdfArray>("ByteRange");
        if (byteRange is null || byteRange.Count < 4)
            return Invalid(fieldName, "Missing or malformed /ByteRange.");

        var off0 = ReadLong(byteRange[0]);
        var len0 = ReadLong(byteRange[1]);
        var off1 = ReadLong(byteRange[2]);
        var len1 = ReadLong(byteRange[3]);

        if (off0 < 0 || len0 <= 0 || off1 < 0 || len1 <= 0 ||
            off0 + len0 > pdfBytes.Length || off1 + len1 > pdfBytes.Length)
            return Invalid(fieldName, "ByteRange values are out of bounds.");

        // Extract /Contents (the PKCS#7 DER blob stored as a hex string)
        var contentsStr = sigValue.Get<PdfString>("Contents");
        if (contentsStr is null)
            return Invalid(fieldName, "Missing /Contents.");

        var contentsBytes = DecodeContentsBytes(contentsStr);
        if (contentsBytes is null)
            return Invalid(fieldName, "Cannot decode /Contents hex string.");

        // Reconstruct the signed byte ranges (everything except the /Contents hex literal)
        var signedContent = new byte[len0 + len1];
        pdfBytes.AsSpan((int)off0, (int)len0).CopyTo(signedContent);
        pdfBytes.AsSpan((int)off1, (int)len1).CopyTo(signedContent.AsSpan((int)len0));

        // Extract metadata
        var reason = ReadString(sigValue, "Reason");
        var location = ReadString(sigValue, "Location");
        var signingTime = ReadPdfDate(ReadString(sigValue, "M"));

        // Cryptographic verification
        return VerifyCms(
            signedContent,
            contentsBytes,
            fieldName,
            reason,
            location,
            signingTime
        );
    }

    private static PdfSignatureInfo VerifyCms(
        byte[] signedContent,
        byte[] contentsBytes,
        string fieldName,
        string? reason,
        string? location,
        DateTimeOffset? signingTime
    )
    {
        X509Certificate2? signerCert = null;

        try
        {
            var contentInfo = new ContentInfo(signedContent);
            var cms = new SignedCms(contentInfo, true);
            cms.Decode(contentsBytes);

            // GetNameInfo(SimpleName) returns empty on macOS with MachineKeySet certs in .NET 9.
            // Parse CN directly from the SubjectName string as a reliable cross-platform approach.
            signerCert = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
            var signerName = signerCert is null ? string.Empty : ExtractCN(signerCert);

            // Verify the signature. CheckSignature(verifySignatureOnly: true) validates both
            // the message-digest attribute against the provided content AND the RSA/ECDSA
            // signature over the signed attributes — using the same internal hash computation
            // as ComputeSignature. A separate manual hash check would use the raw byte array
            // directly, which can disagree with how ContentInfo encodes the content internally
            // on certain platforms (arm64/net9), causing spurious verification failures.
            cms.CheckSignature(true);

            // Step 3: verify the certificate chain (separately — failure here is not a document integrity failure)
            bool certValid;
            try
            {
                cms.CheckSignature(false);
                certValid = true;
            }
            catch
            {
                certValid = false;
            }

            return new PdfSignatureInfo
            {
                FieldName = fieldName,
                SignerName = signerName,
                Reason = reason,
                Location = location,
                SigningTime = signingTime,
                IsSignatureValid = true,
                IsCertificateValid = certValid,
                Certificate = signerCert
            };
        }
        catch (Exception ex)
        {
            return new PdfSignatureInfo
            {
                FieldName = fieldName,
                Reason = reason,
                Location = location,
                SigningTime = signingTime,
                IsSignatureValid = false,
                IsCertificateValid = false,
                ValidationError = ex.Message,
                Certificate = signerCert
            };
        }
    }

    // Extracts the Common Name from a certificate's SubjectName.
    // Parses the string form of the DN (e.g. "CN=Alice Smith, O=Foo") rather than using
    // GetNameInfo(SimpleName) which is unreliable on macOS with MachineKeySet certs in .NET 9.
    private static string ExtractCN(X509Certificate2 cert)
    {
        var dn = cert.SubjectName.Name;
        foreach (var part in dn.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return trimmed[3..].Trim();
        }

        return cert.GetNameInfo(X509NameType.SimpleName, false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PdfDictionary? ResolveAcroForm(PdfDocumentCore core)
    {
        try
        {
            var acroFormObj = core.Catalog["AcroForm"];
            return Resolve<PdfDictionary>(acroFormObj, core);
        }
        catch (PdfException) { return null; }
    }

    private static T? Resolve<T>(PdfObject? obj, PdfDocumentCore core)
        where T : PdfObject => obj switch
    {
        T direct => direct,
        PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as T,
        _ => null
    };

    private static long ReadLong(PdfObject obj) => obj switch
    {
        PdfInteger i => i.Value,
        PdfReal r => (long)r.Value,
        _ => -1
    };

    private static string? ReadString(PdfDictionary dict, string key) =>
        dict.Get<PdfString>(key) is { } s
            ? Encoding.Latin1.GetString(s.Bytes.Span)
            : null;

    // Decode /Contents — parser stores hex strings as raw hex ASCII chars; decode to binary.
    private static byte[]? DecodeContentsBytes(PdfString str)
    {
        if (!str.IsHex) return str.Bytes.ToArray();

        var hex = str.Bytes.Span;
        if (hex.Length % 2 != 0)
            return null;

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = (byte)((HexNibble(hex[i * 2]) << 4) | HexNibble(hex[(i * 2) + 1]));

        // Strip trailing zero bytes (padding from reserved space)
        var len = result.Length;
        while (len > 0 && result[len - 1] == 0)
            len--;

        return result[..len];
    }

    private static int HexNibble(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        _ => 0
    };

    // ReSharper disable once CommentTypo
    // Parse PDF date string D:YYYYMMDDHHmmSSZ
    private static DateTimeOffset? ReadPdfDate(string? dateStr)
    {
        if (dateStr is null || !dateStr.StartsWith("D:", StringComparison.Ordinal))
            return null;

        var s = dateStr[2..];
        if (s.Length < 14)
            return null;

        if (DateTimeOffset.TryParseExact(
                s[..14],
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var result))
            return result;

        return null;
    }

    private static PdfSignatureInfo Invalid(string fieldName, string reason, X509Certificate2? cert = null) =>
        new()
        {
            FieldName = fieldName,
            IsSignatureValid = false,
            IsCertificateValid = false,
            ValidationError = reason,
            Certificate = cert
        };
}
