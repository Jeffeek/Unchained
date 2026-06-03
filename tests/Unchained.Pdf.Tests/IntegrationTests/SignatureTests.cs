using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Integration tests for PDF digital signatures.
/// Uses a self-signed RSA certificate generated in-process — no external PKI required.
/// </summary>
public sealed class SignatureTests : PdfTestBase
{
    // ── Fixture: self-signed certificate ─────────────────────────────────────

    private static X509Certificate2 CreateSelfSignedCert(string cn = "Test Signer")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: false));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(1));
        // Export + re-import to ensure the key is properly associated (use X509CertificateLoader — SYSLIB0057)
        // EphemeralKeySet is not supported on macOS — use MachineKeySet there.
        var storageFlags = X509KeyStorageFlags.Exportable |
                           (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                               ? X509KeyStorageFlags.MachineKeySet
                               : X509KeyStorageFlags.EphemeralKeySet);
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null, storageFlags);
    }

    // ── Sign ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sign_SinglePage_ProducesValidPdfBytes()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms);

        ms.ToArray()[..5].ShouldBe("%PDF-"u8.ToArray());
        ms.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Sign_ProducesLoadablePdf()
    {
        using var cert = CreateSelfSignedCert();
        await using var original = await LoadAsync(PdfFixtures.MultiPage(count: 2));

        using var ms = new MemoryStream();
        await Processor.SignAsync(original, cert, ms);

        await using var reloaded = await LoadAsync(ms.ToArray());
        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task Sign_WithOptions_MetadataEmbedded()
    {
        using var cert = CreateSelfSignedCert("Alice Smith");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());

        var opts = new SignatureOptions(
            Reason: "I approve",
            Location: "New York",
            ContactInfo: "alice@example.com");

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, opts);

        // Verify and check metadata came back
        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray());
        signatures.Count.ShouldBe(1);
        signatures[0].Reason.ShouldBe("I approve");
        signatures[0].Location.ShouldBe("New York");
    }

    [Fact]
    public async Task Sign_TableDocument_ProducesLoadablePdf()
    {
        using var cert = CreateSelfSignedCert();
        var gen = new Engine.TableGenerator();
        var data = PdfFixtures.SimpleTableData(rows: 3);
        await using var original = await gen.GenerateAsync(data, TableStyle.Default);

        using var ms = new MemoryStream();
        await Processor.SignAsync(original, cert, ms);

        await using var reloaded = await LoadAsync(ms.ToArray());
        reloaded.PageCount.ShouldBe(original.PageCount);
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_SignedDocument_FindsOneSignature()
    {
        using var cert = CreateSelfSignedCert("Bob");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms);

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray());
        signatures.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Verify_SignedDocument_SignatureIsValid()
    {
        using var cert = CreateSelfSignedCert("Carol");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, new SignatureOptions(Reason: "Test"));

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray());
        signatures[0].IsSignatureValid.ShouldBeTrue("Signature must be cryptographically valid.");
        signatures[0].SignerName.ShouldContain("Carol");
        signatures[0].Reason.ShouldBe("Test");
    }

    [Fact]
    public async Task Verify_UnsignedDocument_ReturnsEmpty()
    {
        var signatures = await Processor.VerifySignaturesAsync(PdfFixtures.SinglePage());
        signatures.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_TamperedDocument_SignatureIsInvalid()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms);

        // Tamper: flip a byte near the start of the file — always within the signed byte range,
        // well before the /Contents placeholder which sits toward the end.
        var bytes = ms.ToArray();
        bytes[20] ^= 0xFF; // safely inside range 0..contentsStart

        var signatures = await Processor.VerifySignaturesAsync(bytes);
        // Either the PDF fails to parse (so 0 signatures) or the sig is invalid
        if (signatures.Count > 0)
            signatures[0].IsSignatureValid.ShouldBeFalse("Tampered document signature must be invalid.");
    }

    [Fact]
    public async Task Sign_SigningTime_RoundTrips()
    {
        using var cert = CreateSelfSignedCert();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        // ReSharper disable BadListLineBreaks
        var when = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        // ReSharper restore BadListLineBreaks

        using var ms = new MemoryStream();
        await Processor.SignAsync(doc, cert, ms, new SignatureOptions(SigningTime: when));

        var signatures = await Processor.VerifySignaturesAsync(ms.ToArray());
        signatures.Count.ShouldBe(1);
        signatures[0].SigningTime?.Year.ShouldBe(2025);
    }
}
