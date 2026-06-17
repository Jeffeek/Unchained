using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for the <see cref="Unchained.Pptx.Engine.PresentationDocument" /> container
///     accessors, flags, signature/hyperlink enumeration, find/replace, and disposal — exercised
///     against an in-memory blank presentation.
/// </summary>
public sealed class PresentationDocumentTests
{
    [Fact]
    public void Blank_ExposesNonNullCollections()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.Slides.ShouldNotBeNull();
        doc.Masters.ShouldNotBeNull();
        doc.Media.ShouldNotBeNull();
        doc.Properties.ShouldNotBeNull();
        doc.CommentAuthors.ShouldNotBeNull();
        doc.Sections.ShouldNotBeNull();
        doc.SlideShow.ShouldNotBeNull();
        doc.Protection.ShouldNotBeNull();
    }

    [Fact]
    public void Blank_HasNoMacrosOrSignatures()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.HasMacros.ShouldBeFalse();
        doc.HasDigitalSignatures.ShouldBeFalse();
        doc.GetDigitalSignatures().ShouldBeEmpty();
    }

    [Fact]
    public void SlideSize_IsSettable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var size = new SlideSize(new Emu(100), new Emu(200));
        doc.SlideSize = size;
        doc.SlideSize.Width.Value.ShouldBe(100);
        doc.SlideSize.Height.Value.ShouldBe(200);
    }

    [Fact]
    public void GetHyperlinks_NoLinks_ReturnsEmpty()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.GetHyperlinks().ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceText_AcrossSlides_ReplacesInTextBoxes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "hello world");

        var count = doc.ReplaceText("world", "there");
        count.ShouldBeGreaterThan(0);
        doc.Slides[0]
            .Shapes.OfType<AutoShape>()
            .ShouldContain(static s => s.TextFrame.PlainText.Contains("there"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Dispose();
        Should.NotThrow(() => doc.Dispose());
    }

    [Fact]
    public async Task DisposeAsync_Works()
    {
        var doc = PptxFixtures.BlankPresentation();
        await doc.DisposeAsync();
        Should.NotThrow(doc.Dispose);
    }

    [Fact]
    public void ReplaceText_DefaultsToSkippingNotes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "alpha");
        doc.Slides[0].Notes.NotesText = "alpha in notes";

        var count = doc.ReplaceText("alpha", "beta");
        count.ShouldBe(1);
        doc.Slides[0].Notes.NotesText.ShouldContain("alpha");
    }

    [Fact]
    public void ReplaceText_IncludeNotes_ReplacesInNotes()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "alpha");
        doc.Slides[0].Notes.NotesText = "alpha in notes";

        var count = doc.ReplaceText("alpha", "beta", includeNotes: true);
        count.ShouldBeGreaterThanOrEqualTo(2);
        doc.Slides[0].Notes.NotesText.ShouldNotContain("alpha");
    }

    [Fact]
    public void ReplaceText_CaseInsensitive_Matches()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "Hello");

        var count = doc.ReplaceText("hello", "Howdy", StringComparison.OrdinalIgnoreCase);
        count.ShouldBe(1);
    }

    [Fact]
    public void ReplaceText_NoMatch_ReturnsZero()
    {
        using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "content");
        doc.ReplaceText("absent", "x").ShouldBe(0);
    }

    [Fact]
    public void GetHyperlinks_WithLinks_EnumeratesAcrossSlides()
    {
        using var doc = PptxFixtures.WithSlides(2);
        var box0 = doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "a");
        box0.ClickAction = HyperlinkAction.ToUrl("https://example.com", true);
        var box1 = doc.Slides[1].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1), "b");
        box1.ClickAction = HyperlinkAction.ToSlide(1);

        doc.GetHyperlinks().Count().ShouldBe(2);
    }

    [Fact]
    public async Task ReplaceText_RoundTrips()
    {
        await using var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(3), Emu.FromInches(1), "needle here");
        doc.ReplaceText("needle", "thread");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Slides[0].GetAllText().ShouldContain("thread");
    }

    [Fact]
    public async Task SyncStatistics_RunOnSave_ReflectsSlideAndNotesCounts()
    {
        await using var doc = PptxFixtures.WithSlides(3);
        doc.Slides[1].IsHidden = true;
        doc.Slides[0].Notes.NotesText = "speaker notes";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Properties.SlideCount.ShouldBe(3);
        reloaded.Properties.HiddenSlideCount.ShouldBe(1);
    }

    [Fact]
    public async Task RoundTrip_HasNoMacrosOrSignatures()
    {
        await using var doc = PptxFixtures.WithSlides(1);
        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.HasMacros.ShouldBeFalse();
        reloaded.HasDigitalSignatures.ShouldBeFalse();
        reloaded.GetDigitalSignatures().ShouldBeEmpty();
    }

    [Fact]
    public void SlideShow_IsSettable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        doc.SlideShow.ShouldNotBeNull();
    }

    // ── Digital-signature parsing (ParseSignatureInfo / GetDigitalSignatures) ─────

    private const string SignatureContentType =
        "application/vnd.openxmlformats-officedocument.digital-signature-xmlsignature+xml";

    private static PreservedPart SignaturePart(string xml, string uri = "/_xmlsignatures/sig1.xml") =>
        new()
        {
            Uri = uri,
            ContentType = SignatureContentType,
            Data = System.Text.Encoding.UTF8.GetBytes(xml)
        };

    private static string SelfSignedCertBase64(out string commonName)
    {
        commonName = "Unchained Test Signer";
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={commonName}",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1
        );
        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );
        return Convert.ToBase64String(cert.Export(
            System.Security.Cryptography.X509Certificates.X509ContentType.Cert
        ));
    }

    private static string SignatureXml(string? certBase64, string? signingTime)
    {
        var certEl = certBase64 == null
            ? string.Empty
            : $"<X509Certificate>{certBase64}</X509Certificate>";
        var timeEl = signingTime == null
            ? string.Empty
            : $"<SigningTime>{signingTime}</SigningTime>";
        return "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\">" +
               $"<KeyInfo><X509Data>{certEl}</X509Data></KeyInfo>" +
               $"<Object>{timeEl}</Object></Signature>";
    }

    [Fact]
    public void HasDigitalSignatures_WithSignaturePart_IsTrue()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var preserved = new PreservedContent();
        preserved.Parts.Add(SignaturePart(SignatureXml(null, null)));
        doc.Preserved = preserved;

        doc.HasDigitalSignatures.ShouldBeTrue();
    }

    [Fact]
    public void GetDigitalSignatures_ValidCertAndTime_ParsesSignerAndTime()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var certB64 = SelfSignedCertBase64(out var cn);
        var preserved = new PreservedContent();
        preserved.Parts.Add(SignaturePart(SignatureXml(certB64, "2023-05-06T07:08:09Z")));
        doc.Preserved = preserved;

        var sig = doc.GetDigitalSignatures().ShouldHaveSingleItem();
        sig.IsReadable.ShouldBeTrue();
        sig.SignerName.ShouldContain(cn);
        sig.SigningTime!.Value.UtcDateTime.Year.ShouldBe(2023);
        sig.PartUri.ShouldBe("/_xmlsignatures/sig1.xml");
    }

    [Fact]
    public void GetDigitalSignatures_NoCertNoTime_ReadableWithEmptySigner()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var preserved = new PreservedContent();
        preserved.Parts.Add(SignaturePart(SignatureXml(null, null)));
        doc.Preserved = preserved;

        var sig = doc.GetDigitalSignatures().ShouldHaveSingleItem();
        sig.IsReadable.ShouldBeTrue();
        sig.SignerName.ShouldBeEmpty();
        sig.SigningTime.ShouldBeNull();
    }

    [Fact]
    public void GetDigitalSignatures_InvalidCertBytes_LeavesSignerEmptyButReadable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var preserved = new PreservedContent();
        // Valid base64 but not a real certificate — hits the inner cert-parse catch.
        preserved.Parts.Add(SignaturePart(SignatureXml("bm90QWNlcnQ=", "not a real date")));
        doc.Preserved = preserved;

        var sig = doc.GetDigitalSignatures().ShouldHaveSingleItem();
        sig.IsReadable.ShouldBeTrue();
        sig.SignerName.ShouldBeEmpty();
        sig.SigningTime.ShouldBeNull();
    }

    [Fact]
    public void GetDigitalSignatures_MalformedXml_MarksUnreadable()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var preserved = new PreservedContent();
        preserved.Parts.Add(SignaturePart("<not-valid-xml unclosed"));
        doc.Preserved = preserved;

        var sig = doc.GetDigitalSignatures().ShouldHaveSingleItem();
        sig.IsReadable.ShouldBeFalse();
        sig.SignerName.ShouldBeEmpty();
    }

    [Fact]
    public void GetDigitalSignatures_NonSignaturePartsIgnored()
    {
        using var doc = PptxFixtures.BlankPresentation();
        var preserved = new PreservedContent();
        preserved.Parts.Add(new PreservedPart
        {
            Uri = "/ppt/vbaProject.bin",
            ContentType = "application/vnd.ms-office.vbaProject",
            Data = [1, 2, 3]
        });
        doc.Preserved = preserved;

        doc.HasDigitalSignatures.ShouldBeFalse();
        doc.GetDigitalSignatures().ShouldBeEmpty();
    }
}
