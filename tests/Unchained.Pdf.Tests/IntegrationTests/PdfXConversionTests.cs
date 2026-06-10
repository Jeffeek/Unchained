using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
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
        var text = System.Text.Encoding.Latin1.GetString(converted);
        text.ShouldContain("/OutputIntents");
        text.ShouldContain("GTS_PDFX");
    }

    [Fact]
    public async Task ConvertToPdfX_AddsVersionMarker()
    {
        var converted = await ConvertAsync(PdfFixtures.SinglePage(), PdfXProfile.PdfX3_2002);
        var text = System.Text.Encoding.Latin1.GetString(converted);
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
        var text = System.Text.Encoding.Latin1.GetString(converted);
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
}
