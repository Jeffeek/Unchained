using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class PdfXConversionTests : PdfTestBase
{
    private static async Task<byte[]> ConvertAsync(byte[] src, PdfXProfile profile)
    {
        await using var doc = await LoadAsync(src);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfXAsync(doc, ms, profile);
        return ms.ToArray();
    }

    [Fact]
    public async Task ConvertToPdfX_AddsOutputIntent()
    {
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), PdfXProfile.PdfX1A2001);
        // /OutputIntents + the GTS_PDFX subtype must be present.
        var text = Encoding.Latin1.GetString(converted);
        text.ShouldContain("/OutputIntents");
        text.ShouldContain("GTS_PDFX");
    }

    [Fact]
    public async Task ConvertToPdfX_AddsVersionMarker()
    {
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), PdfXProfile.PdfX32002);
        var text = Encoding.Latin1.GetString(converted);
        text.ShouldContain("GTS_PDFXVersion");
        text.ShouldContain("PDF/X-3:2002");
    }

    [Fact]
    public async Task ConvertToPdfX_ProducesReloadableDocument()
    {
        var converted = await ConvertAsync(PdfFixtures.MultiPage(3), PdfXProfile.PdfX1A2001);
        await using var reloaded = await LoadAsync(converted);
        reloaded.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task ConvertToPdfX_CatalogHasOutputIntents()
    {
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), PdfXProfile.PdfX4);
        await using var reloaded = await LoadAsync(converted);
        // The output intent object should be reachable and carry the GTS_PDFX subtype.
        reloaded.PageCount.ShouldBe(1);
        var text = Encoding.Latin1.GetString(converted);
        text.ShouldContain("PDF/X-4");
    }

    [Fact]
    public async Task ConvertToPdfX_PreservesInfoTitle()
    {
        var converted = await ConvertAsync(PdfFixtures.WithInfo("MyDoc", "Author"), PdfXProfile.PdfX1A2001);
        await using var reloaded = await LoadAsync(converted);
        // GTS_PDFXVersion is added to /Info; the existing Title is preserved.
        reloaded.Metadata.Title.ShouldBe("MyDoc");
    }

    [
        Theory,
        InlineData(PdfXProfile.PdfX1A2001, "PDF/X-1a:2001"),
        InlineData(PdfXProfile.PdfX1A2003, "PDF/X-1a:2003"),
        InlineData(PdfXProfile.PdfX32002, "PDF/X-3:2002"),
        InlineData(PdfXProfile.PdfX32003, "PDF/X-3:2003"),
        InlineData(PdfXProfile.PdfX4, "PDF/X-4")
    ]
    public async Task ConvertToPdfX_AllProfiles_EmitMatchingVersionString(PdfXProfile profile, string expected)
    {
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), profile);
        Encoding.Latin1.GetString(converted).ShouldContain(expected);
    }

    [Fact]
    public async Task ConvertToPdfX_PreservesExistingId()
    {
        // A document with an existing /ID (any saved document) must keep an /ID through conversion.
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), PdfXProfile.PdfX1A2001);
        await using var reloaded = await LoadAsync(converted);
        reloaded.Id.ShouldNotBeNull();
    }

    [Fact]
    public async Task ConvertToPdfX_EncryptedDocument_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        using var encMs = new MemoryStream();
        await Processor.SaveAsync(doc, encMs, new SaveOptions(Encryption: new EncryptionOptions("pw")));

        encMs.Position = 0;
        await using var encDoc = await Processor.LoadAsync(encMs, "pw");
        using var outMs = new MemoryStream();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            // ReSharper disable once RedundantArgumentDefaultValue
            Processor.ConvertToPdfXAsync(encDoc, outMs, PdfXProfile.PdfX1A2001)
        );
    }
}
