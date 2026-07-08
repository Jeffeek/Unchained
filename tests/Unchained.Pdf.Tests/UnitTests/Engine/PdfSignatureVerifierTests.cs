using Shouldly;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for <see cref="PdfSignatureVerifier" /> error branches that the
///     full sign/verify round-trip in <c>SignatureTests</c> does not reach: missing or malformed
///     <c>/ByteRange</c>, missing <c>/Contents</c>, undecodable PKCS#7, and <c>/M</c> date parsing.
///     Crafts signature fields directly via the AcroForm so no real certificate is needed.
/// </summary>
public sealed class PdfSignatureVerifierTests
{
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>";
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>";
    private const string SigField = "<< /FT /Sig /T (Signature1) /V 5 0 R >>";

    private static IReadOnlyList<PdfSignatureInfo> VerifyWith(string sigValueDict)
    {
        var bytes = RawPdfBuilder.Build([Catalog, Pages, Page, SigField, sigValueDict]);
        var core = PdfDocumentCore.Parse(bytes);
        return PdfSignatureVerifier.Verify(bytes, core);
    }

    [Fact]
    public void NoAcroForm_ReturnsEmpty()
    {
        var bytes = PdfFixtures.SinglePage();
        var core = PdfDocumentCore.Parse(bytes);
        PdfSignatureVerifier.Verify(bytes, core).ShouldBeEmpty();
    }

    [Fact]
    public void MissingByteRange_ReportsInvalidWithError()
    {
        var sigs = VerifyWith("<< /Type /Sig /Contents <00> >>");
        sigs.Count.ShouldBe(1);
        sigs[0].IsSignatureValid.ShouldBeFalse();
        sigs[0].ValidationError.ShouldNotBeNull();
        sigs[0].ValidationError!.ShouldContain("ByteRange");
    }

    [Fact]
    public void MalformedByteRange_OutOfBounds_ReportsInvalid()
    {
        // ByteRange points well beyond the file length.
        var sigs = VerifyWith("<< /Type /Sig /ByteRange [0 10 999999 10] /Contents <00> >>");
        sigs.Count.ShouldBe(1);
        sigs[0].IsSignatureValid.ShouldBeFalse();
        sigs[0].ValidationError!.ShouldContain("out of bounds");
    }

    [Fact]
    public void MissingContents_ReportsInvalid()
    {
        var sigs = VerifyWith("<< /Type /Sig /ByteRange [0 10 20 10] >>");
        sigs.Count.ShouldBe(1);
        sigs[0].IsSignatureValid.ShouldBeFalse();
        sigs[0].ValidationError!.ShouldContain("Contents");
    }

    [Fact]
    public void UndecodablePkcs7_ReportsInvalidButFieldNamePopulated()
    {
        // Valid ByteRange within bounds, /Contents is a short non-PKCS7 hex blob.
        var sigs = VerifyWith("<< /Type /Sig /ByteRange [0 10 20 10] /Contents <DEADBEEF> >>");
        sigs.Count.ShouldBe(1);
        sigs[0].FieldName.ShouldBe("Signature1");
        sigs[0].IsSignatureValid.ShouldBeFalse();
    }

    [Fact]
    public void SignatureMetadata_ReasonLocationDate_AreParsed()
    {
        var sigs = VerifyWith(
            "<< /Type /Sig /ByteRange [0 10 20 10] /Contents <00> " +
            "/Reason (I approve) /Location (Berlin) /M (D:20240615120000Z) >>"
        );
        sigs.Count.ShouldBe(1);
        sigs[0].Reason.ShouldBe("I approve");
        sigs[0].Location.ShouldBe("Berlin");
        sigs[0].SigningTime.ShouldNotBeNull();
        sigs[0].SigningTime!.Value.Year.ShouldBe(2024);
        sigs[0].SigningTime!.Value.Month.ShouldBe(6);
    }

    [Fact]
    public void InvalidDateString_LeavesSigningTimeNull()
    {
        var sigs = VerifyWith("<< /Type /Sig /ByteRange [0 10 20 10] /Contents <00> /M (not-a-date) >>");
        sigs[0].SigningTime.ShouldBeNull();
    }

    [Fact]
    public void NoSignatureFields_NonSigFieldIgnored()
    {
        // AcroForm field is a text field, not a signature.
        var bytes = RawPdfBuilder.Build(
            [Catalog, Pages, Page, "<< /FT /Tx /T (Name) >>"]
        );
        var core = PdfDocumentCore.Parse(bytes);
        PdfSignatureVerifier.Verify(bytes, core).ShouldBeEmpty();
    }

    [Fact]
    public void SigFieldWithoutValue_IsSkipped()
    {
        var bytes = RawPdfBuilder.Build([Catalog, Pages, Page, "<< /FT /Sig /T (Empty) >>"]);
        var core = PdfDocumentCore.Parse(bytes);
        PdfSignatureVerifier.Verify(bytes, core).ShouldBeEmpty();
    }
}
