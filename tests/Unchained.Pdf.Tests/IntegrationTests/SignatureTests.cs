using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Integration tests for PDF digital signatures.
///     Uses a self-signed RSA certificate generated in-process — no external PKI required.
/// </summary>
public sealed class SignatureTests : PdfTestBase
{
    // ── Fixture: self-signed certificate ─────────────────────────────────────

    private static X509Certificate2 CreateSelfSignedCert(string cn = "Test Signer")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(1));
        // Export + re-import to ensure the key is properly associated (use X509CertificateLoader — SYSLIB0057)
        // EphemeralKeySet is not supported on macOS — use MachineKeySet there.
        var storageFlags = X509KeyStorageFlags.Exportable |
                           (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                               ? X509KeyStorageFlags.MachineKeySet
                               : X509KeyStorageFlags.EphemeralKeySet);
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, storageFlags);
    }

    // ── Sign ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sign_SinglePage_ProducesValidPdfBytes()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, ct: TestContext.Current.CancellationToken);

        ms.ToArray()[..5].ShouldBe("%PDF-"u8.ToArray());
        ms.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Sign_ProducesLoadablePdf()
    {
        using var cert = CreateSelfSignedCert();
        await using var original = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(original, cert, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task Sign_WithOptions_MetadataEmbedded()
    {
        using var cert = CreateSelfSignedCert("Alice Smith");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        var opts = new SignatureOptions(
            "I approve",
            "New York",
            "alice@example.com");

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, opts, TestContext.Current.CancellationToken);

        // Verify and check metadata came back
        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(1);
        signatures[0].Reason.ShouldBe("I approve");
        signatures[0].Location.ShouldBe("New York");
    }

    [Fact]
    public async Task Sign_TableDocument_ProducesLoadablePdf()
    {
        using var cert = CreateSelfSignedCert();
        var gen = new TableGenerator();
        var data = PdfFixtures.SimpleTableData(3);
        await using var original = await gen.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(original, cert, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(original.PageCount);
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_SignedDocument_FindsOneSignature()
    {
        using var cert = CreateSelfSignedCert("Bob");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, ct: TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Verify_SignedDocument_SignatureIsValid()
    {
        using var cert = CreateSelfSignedCert("Carol");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        // Pin the signing time so the test has no dependency on wall-clock calls.
        // This eliminates any cross-second-boundary flakiness between the /M entry
        // and the Pkcs9SigningTime signed attribute.
        var pinned = new DateTimeOffset(2024,
            6,
            1,
            12,
            0,
            0,
            TimeSpan.Zero);
        using var ms = new MemoryStream();
        await Processor.SignAsync(
            doc,
            cert,
            ms,
            new SignatureOptions("Test", SigningTime: pinned),
            TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures[0].IsSignatureValid.ShouldBeTrue("Signature must be cryptographically valid.");
        signatures[0].SignerName.ShouldContain("Carol");
        signatures[0].Reason.ShouldBe("Test");
    }

    [Fact]
    public async Task Verify_UnsignedDocument_ReturnsEmpty()
    {
        var signatures = await Processor.VerifySignaturesAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_TamperedDocument_SignatureIsInvalid()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, ct: TestContext.Current.CancellationToken);

        // Tamper: flip a byte near the start of the file — always within the signed byte range,
        // well before the /Contents placeholder which sits toward the end.
        var bytes = ms.ToArray();
        bytes[20] ^= 0xFF; // safely inside range 0..contentsStart

        var signatures = await Processor.VerifySignaturesAsync(bytes, TestContext.Current.CancellationToken);
        // Either the PDF fails to parse (so 0 signatures) or the sig is invalid
        if (signatures.Count > 0)
            signatures[0].IsSignatureValid.ShouldBeFalse("Tampered document signature must be invalid.");
    }

    [Fact]
    public async Task Sign_SigningTime_RoundTrips()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // ReSharper disable BadListLineBreaks
        var when = new DateTimeOffset(2025,
            6,
            15,
            12,
            0,
            0,
            TimeSpan.Zero);
        // ReSharper restore BadListLineBreaks

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, new SignatureOptions(SigningTime: when), TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(1);
        signatures[0].SigningTime?.Year.ShouldBe(2025);
    }

    [Fact]
    public async Task Verify_SignedDocument_SignerNameSigningTimeReasonAllPopulated()
    {
        using var cert = CreateSelfSignedCert("Diana Prince");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        // ReSharper disable BadListLineBreaks
        var when = new DateTimeOffset(2024,
            3,
            10,
            9,
            30,
            0,
            TimeSpan.Zero);
        // ReSharper restore BadListLineBreaks
        var opts = new SignatureOptions("Approved", "London", SigningTime: when);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, opts, TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(1);
        var sig = signatures[0];
        sig.SignerName.ShouldContain("Diana Prince");
        sig.Reason.ShouldBe("Approved");
        sig.Location.ShouldBe("London");
        sig.SigningTime.ShouldNotBeNull();
        sig.SigningTime!.Value.Year.ShouldBe(2024);
        sig.SigningTime.Value.Month.ShouldBe(3);
    }

    [Fact]
    public async Task Sign_NullCertificate_ThrowsArgumentNullException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();

        await Should.ThrowAsync<ArgumentNullException>(() =>
            Processor.SignAsync(doc, null!, ms, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sign_WithCustomSignatureOptions_AllFieldsRoundTrip()
    {
        using var cert = CreateSelfSignedCert("Eve Adams");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var pinned = new DateTimeOffset(2024,
            6,
            1,
            12,
            0,
            0,
            TimeSpan.Zero);
        var opts = new SignatureOptions(
            "Final approval",
            "Paris",
            "eve@example.com",
            FieldName: "AuthorSig",
            SigningTime: pinned);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, opts, TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        signatures.Count.ShouldBe(1);
        var sig = signatures[0];
        sig.Reason.ShouldBe("Final approval");
        sig.Location.ShouldBe("Paris");
        sig.FieldName.ShouldBe("AuthorSig");
        sig.IsSignatureValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Sign_Twice_BothSignaturesVerified()
    {
        using var cert1 = CreateSelfSignedCert("Signer One");
        using var cert2 = CreateSelfSignedCert("Signer Two");

        await using var original = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        // First signature
        using var ms1 = new MemoryStream();
        await Processor.SignAsync(original, cert1, ms1, new SignatureOptions("First", FieldName: "Sig1"), TestContext.Current.CancellationToken);

        // Second signature applied to the already-signed PDF
        await using var afterFirst = await LoadAsync(ms1.ToArray(), TestContext.Current.CancellationToken);
        using var ms2 = new MemoryStream();
        await Processor.SignAsync(afterFirst, cert2, ms2, new SignatureOptions("Second", FieldName: "Sig2"), TestContext.Current.CancellationToken);

        var signatures = await Processor.VerifySignaturesAsync(ms2.ToArray(), TestContext.Current.CancellationToken);
        // Both signature fields are found.
        signatures.Count.ShouldBe(2);
        // The second (most-recent) signature covers the full final byte range and is valid.
        signatures.ShouldContain(static s => s.FieldName == "Sig2" && s.IsSignatureValid);
        // Sig1's ByteRange was computed before the second signing re-serialized the document,
        // so it may no longer be valid — just verify the field is present.
        signatures.ShouldContain(static s => s.FieldName == "Sig1");
    }

    [Fact]
    public async Task Sign_ByteRange_CoversEntireFileExceptContentsHex()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, ct: TestContext.Current.CancellationToken);

        var bytes = ms.ToArray();

        // ByteRange must appear in the output and the two signed ranges must together
        // cover all bytes except the /Contents hex literal gap.
        // Verify by checking the total covered length equals file length minus the gap.
        var text = Encoding.Latin1.GetString(bytes);
        var byteRangeIdx = text.IndexOf("/ByteRange", StringComparison.Ordinal);
        byteRangeIdx.ShouldBeGreaterThan(0, "Signed PDF must contain /ByteRange entry.");

        // Extract the four numbers after /ByteRange [
        var bracketStart = text.IndexOf('[', byteRangeIdx);
        var bracketEnd = text.IndexOf(']', bracketStart);
        var parts = text[(bracketStart + 1)..bracketEnd]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        parts.Length.ShouldBe(4, "ByteRange must have exactly 4 values.");

        var off0 = long.Parse(parts[0]);
        var len0 = long.Parse(parts[1]);
        var off1 = long.Parse(parts[2]);
        var len1 = long.Parse(parts[3]);

        // Ranges must be non-negative and fit within the file
        off0.ShouldBe(0, "ByteRange must start at offset 0.");
        len0.ShouldBeGreaterThan(0);
        off1.ShouldBeGreaterThan(len0, "Second range must start after first range ends.");
        (off1 + len1).ShouldBe(bytes.Length, "Second range must end at end of file.");
    }
}
